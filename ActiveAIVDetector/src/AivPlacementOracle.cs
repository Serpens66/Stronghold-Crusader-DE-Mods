using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ActiveAIVDetector
{
    internal sealed unsafe class AivPlacementOracle
    {
        private const string SelectBestFitPattern =
            "44 88 44 24 18 89 54 24 10 55 56 41 54 41 55 41 56 41 57 48 83 EC 58";
        private const string TestSpecificCandidatePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 " +
            "48 89 7C 24 20 41 56 48 83 EC 20 41 8B F0 48 63 EA " +
            "48 8B F9 4C 8D 89 44 98 1B 00";
        private const string LoadCandidatePattern =
            "40 53 56 57 41 55 48 83 EC 38 8B 05 C0 AC 27 00 " +
            "48 8D 0D C5 13 49 03 41 8B D8 48 63 FA 85 C0";
        private const string ApplyRotationPattern =
            "85 D2 0F 84 01 0A 00 00 53 48 83 EC 20 48 89 74 24 30 " +
            "48 8B D9 48 89 7C 24 38 83 FA 06";
        private const string EvaluateCandidateFitPattern =
            "89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 48 45 33 C9 48 8D 81 44 98 1B 00";

        private const int AivSpecStride = 0x6D98;
        private const int PlayerIdOffset = 0x04;
        private const int OrientationOffset = 0x0C;
        private const int CandidateIdOffset = 0x10;
        private const int PlacementStateOffset = 0x14;
        private const int OriginXOffset = 0x28;
        private const int OriginYOffset = 0x2C;
        private const int KeepXOffset = 0x30;
        private const int KeepYOffset = 0x34;
        private const int EvaluatedCellCountOffset = 0x5B4F8;
        private const int BlockedCellCountOffset = 0x5B4FC;
        private const int CompleteFitScore = 999999;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SelectBestFitDelegate(
            ulong aivStateAddress,
            int aivSpecIndex,
            byte tryOtherRotations);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint TestSpecificCandidateDelegate(
            ulong aivStateAddress,
            int aivSpecIndex,
            int candidateId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LoadCandidateDelegate(
            ulong aivStateAddress,
            int zeroBasedPlayerId,
            int candidateId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ApplyRotationDelegate(ulong aivStateAddress, int orientation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EvaluateCandidateFitDelegate(ulong aivStateAddress, int aivSpecIndex);

        private readonly ManualLogSource log;
        private readonly Action<OracleSelectionSnapshot> onSelectionCompleted;
        private HookRef<X64ManagedFunctionDetourAOB<SelectBestFitDelegate>> selectBestFitHook =
            new HookRef<X64ManagedFunctionDetourAOB<SelectBestFitDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<TestSpecificCandidateDelegate>> testSpecificCandidateHook =
            new HookRef<X64ManagedFunctionDetourAOB<TestSpecificCandidateDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<LoadCandidateDelegate>> loadCandidateHook =
            new HookRef<X64ManagedFunctionDetourAOB<LoadCandidateDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<ApplyRotationDelegate>> applyRotationHook =
            new HookRef<X64ManagedFunctionDetourAOB<ApplyRotationDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<EvaluateCandidateFitDelegate>> evaluateCandidateFitHook =
            new HookRef<X64ManagedFunctionDetourAOB<EvaluateCandidateFitDelegate>>();

        private OracleSelectionSession activeSession;
        private long nextSequence;
        private bool callbackFailureLogged;

        public AivPlacementOracle(
            ManualLogSource log,
            Action<OracleSelectionSnapshot> onSelectionCompleted)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.onSelectionCompleted = onSelectionCompleted ??
                throw new ArgumentNullException(nameof(onSelectionCompleted));
        }

        public void RegisterHooks(HookTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            transaction.AddDetour(ref selectBestFitHook, SelectBestFitPattern, SelectBestFit);
            transaction.AddDetour(
                ref testSpecificCandidateHook,
                TestSpecificCandidatePattern,
                TestSpecificCandidate);
            transaction.AddDetour(ref loadCandidateHook, LoadCandidatePattern, LoadCandidate);
            transaction.AddDetour(ref applyRotationHook, ApplyRotationPattern, ApplyRotation);
            transaction.AddDetour(
                ref evaluateCandidateFitHook,
                EvaluateCandidateFitPattern,
                EvaluateCandidateFit);
        }

        public void ValidateHooks()
        {
            List<string> missing = new List<string>();
            AddMissing(missing, selectBestFitHook.Success, "c_game_aiv_select_best_fit");
            AddMissing(missing, testSpecificCandidateHook.Success, "c_game_aiv_test_specific_candidate");
            AddMissing(missing, loadCandidateHook.Success, "c_game_aiv_load_candidate");
            AddMissing(missing, applyRotationHook.Success, "c_game_aiv_apply_rotation");
            AddMissing(missing, evaluateCandidateFitHook.Success, "c_game_aiv_evaluate_candidate_fit");

            if (missing.Count != 0)
            {
                throw new InvalidOperationException(
                    "The native AIV placement oracle signatures were not found: " +
                    string.Join(", ", missing) + ".");
            }
        }

        private void SelectBestFit(
            ulong aivStateAddress,
            int aivSpecIndex,
            byte tryOtherRotations)
        {
            OracleSelectionSession session = TryBeginSession(
                "SelectBestFit",
                aivStateAddress,
                aivSpecIndex,
                tryOtherRotations != 0,
                null);
            try
            {
                selectBestFitHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    aivSpecIndex,
                    tryOtherRotations);
            }
            finally
            {
                CompleteSession(session, null);
            }
        }

        private uint TestSpecificCandidate(
            ulong aivStateAddress,
            int aivSpecIndex,
            int candidateId)
        {
            OracleSelectionSession session = TryBeginSession(
                "TestSpecificCandidate",
                aivStateAddress,
                aivSpecIndex,
                false,
                candidateId);
            uint result = 0;
            bool returned = false;
            try
            {
                result = testSpecificCandidateHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    aivSpecIndex,
                    candidateId);
                returned = true;
                return result;
            }
            finally
            {
                CompleteSession(session, returned ? unchecked((int)result) : (int?)null);
            }
        }

        private void LoadCandidate(
            ulong aivStateAddress,
            int zeroBasedPlayerId,
            int candidateId)
        {
            loadCandidateHook.Value.Hook.Trampoline(
                aivStateAddress,
                zeroBasedPlayerId,
                candidateId);

            OracleSelectionSession session = activeSession;
            if (session != null && session.AivStateAddress == aivStateAddress)
                session.CurrentCandidateId = candidateId;
        }

        private void ApplyRotation(ulong aivStateAddress, int orientation)
        {
            applyRotationHook.Value.Hook.Trampoline(aivStateAddress, orientation);

            OracleSelectionSession session = activeSession;
            if (session != null && session.AivStateAddress == aivStateAddress)
                session.CurrentOrientation = orientation;
        }

        private int EvaluateCandidateFit(ulong aivStateAddress, int aivSpecIndex)
        {
            int rawFitScore = evaluateCandidateFitHook.Value.Hook.Trampoline(
                aivStateAddress,
                aivSpecIndex);

            try
            {
                OracleSelectionSession session = activeSession;
                if (session == null ||
                    session.AivStateAddress != aivStateAddress ||
                    session.AivSpecIndex != aivSpecIndex)
                {
                    return rawFitScore;
                }

                int evaluatedCells = ReadInt32(aivStateAddress, EvaluatedCellCountOffset);
                int blockedCells = ReadInt32(aivStateAddress, BlockedCellCountOffset);
                int fitPercent = evaluatedCells == 0
                    ? 100
                    : ((evaluatedCells - blockedCells) * 100) / evaluatedCells;
                byte* spec = GetSpec(aivStateAddress, aivSpecIndex);

                session.Attempts.Add(new OracleAttemptSnapshot(
                    session.Attempts.Count + 1,
                    session.CurrentCandidateId,
                    session.CurrentOrientation,
                    rawFitScore,
                    fitPercent,
                    evaluatedCells,
                    blockedCells,
                    *(int*)(spec + OriginXOffset),
                    *(int*)(spec + OriginYOffset),
                    *(int*)(spec + KeepXOffset),
                    *(int*)(spec + KeepYOffset)));
            }
            catch (Exception ex)
            {
                LogCallbackFailure("fit result capture", ex);
            }

            // The oracle observes Vanilla's return value without changing it.
            return rawFitScore;
        }

        private OracleSelectionSession TryBeginSession(
            string method,
            ulong aivStateAddress,
            int aivSpecIndex,
            bool tryOtherRotations,
            int? requestedCandidateId)
        {
            try
            {
                if (activeSession != null)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"AIV placement oracle ignored a nested {method} call while " +
                        $"{activeSession.Method} was active.");
                    return null;
                }
                if (aivStateAddress == 0)
                    throw new InvalidOperationException("The native AIV state pointer is null.");
                if (aivSpecIndex < 0 || aivSpecIndex > 8)
                    throw new ArgumentOutOfRangeException(nameof(aivSpecIndex));

                byte* spec = GetSpec(aivStateAddress, aivSpecIndex);
                OracleSelectionSession session = new OracleSelectionSession(
                    ++nextSequence,
                    method,
                    aivStateAddress,
                    aivSpecIndex,
                    *(int*)(spec + PlayerIdOffset),
                    tryOtherRotations,
                    requestedCandidateId ?? *(int*)(spec + CandidateIdOffset),
                    *(int*)(spec + OrientationOffset));
                activeSession = session;
                return session;
            }
            catch (Exception ex)
            {
                LogCallbackFailure($"{method} start", ex);
                return null;
            }
        }

        private void CompleteSession(OracleSelectionSession session, int? directReturnSigned)
        {
            if (session == null)
                return;

            if (ReferenceEquals(activeSession, session))
                activeSession = null;

            try
            {
                byte* spec = GetSpec(session.AivStateAddress, session.AivSpecIndex);
                onSelectionCompleted(new OracleSelectionSnapshot(
                    session.Sequence,
                    session.Method,
                    session.PlayerId,
                    session.AivSpecIndex,
                    session.TryOtherRotations,
                    directReturnSigned,
                    *(int*)(spec + CandidateIdOffset),
                    *(int*)(spec + OrientationOffset),
                    *(int*)(spec + PlacementStateOffset),
                    session.Attempts));
            }
            catch (Exception ex)
            {
                LogCallbackFailure($"{session.Method} completion", ex);
            }
        }

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;

            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"AIV placement oracle {operation} failed; further capture errors are suppressed " +
                $"and Vanilla behavior remains unchanged: {ex}");
        }

        private static byte* GetSpec(ulong aivStateAddress, int aivSpecIndex)
        {
            return (byte*)aivStateAddress + checked(aivSpecIndex * AivSpecStride);
        }

        private static int ReadInt32(ulong address, int offset)
        {
            return *(int*)((byte*)address + offset);
        }

        private static void AddMissing(List<string> missing, bool success, string name)
        {
            if (!success)
                missing.Add(name);
        }

        private sealed class OracleSelectionSession
        {
            public OracleSelectionSession(
                long sequence,
                string method,
                ulong aivStateAddress,
                int aivSpecIndex,
                int playerId,
                bool tryOtherRotations,
                int currentCandidateId,
                int currentOrientation)
            {
                Sequence = sequence;
                Method = method;
                AivStateAddress = aivStateAddress;
                AivSpecIndex = aivSpecIndex;
                PlayerId = playerId;
                TryOtherRotations = tryOtherRotations;
                CurrentCandidateId = currentCandidateId;
                CurrentOrientation = currentOrientation;
                Attempts = new List<OracleAttemptSnapshot>();
            }

            public long Sequence { get; }
            public string Method { get; }
            public ulong AivStateAddress { get; }
            public int AivSpecIndex { get; }
            public int PlayerId { get; }
            public bool TryOtherRotations { get; }
            public int CurrentCandidateId { get; set; }
            public int CurrentOrientation { get; set; }
            public List<OracleAttemptSnapshot> Attempts { get; }
        }
    }

    internal sealed class OracleSelectionSnapshot
    {
        public OracleSelectionSnapshot(
            long sequence,
            string method,
            int playerId,
            int aivSpecIndex,
            bool tryOtherRotations,
            int? directReturnSigned,
            int finalCandidateId,
            int finalOrientation,
            int placementState,
            IList<OracleAttemptSnapshot> attempts)
        {
            Sequence = sequence;
            Method = method;
            PlayerId = playerId;
            AivSpecIndex = aivSpecIndex;
            TryOtherRotations = tryOtherRotations;
            DirectReturnSigned = directReturnSigned;
            FinalCandidateId = finalCandidateId;
            FinalOrientation = finalOrientation;
            PlacementState = placementState;
            Attempts = new List<OracleAttemptSnapshot>(attempts).AsReadOnly();
        }

        public long Sequence { get; }
        public string Method { get; }
        public int PlayerId { get; }
        public int AivSpecIndex { get; }
        public bool TryOtherRotations { get; }
        public int? DirectReturnSigned { get; }
        public int FinalCandidateId { get; }
        public int FinalOrientation { get; }
        public int PlacementState { get; }
        public IReadOnlyList<OracleAttemptSnapshot> Attempts { get; }
    }

    internal readonly struct OracleAttemptSnapshot
    {
        public OracleAttemptSnapshot(
            int attemptNumber,
            int candidateId,
            int orientation,
            int rawFitScore,
            int fitPercent,
            int evaluatedCells,
            int blockedCells,
            int originX,
            int originY,
            int keepX,
            int keepY)
        {
            AttemptNumber = attemptNumber;
            CandidateId = candidateId;
            Orientation = orientation;
            RawFitScore = rawFitScore;
            FitPercent = fitPercent;
            EvaluatedCells = evaluatedCells;
            BlockedCells = blockedCells;
            OriginX = originX;
            OriginY = originY;
            KeepX = keepX;
            KeepY = keepY;
        }

        public int AttemptNumber { get; }
        public int CandidateId { get; }
        public int Orientation { get; }
        public int RawFitScore { get; }
        public int FitPercent { get; }
        public int EvaluatedCells { get; }
        public int BlockedCells { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int KeepX { get; }
        public int KeepY { get; }

        public string ResultKind => RawFitScore == 999999
            ? "Complete"
            : RawFitScore > 0
                ? "Partial"
                : "Rejected";
    }
}
