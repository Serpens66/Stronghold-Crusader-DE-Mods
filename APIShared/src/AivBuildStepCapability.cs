using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace APIShared
{
    internal sealed class AivBuildStepService
    {
        internal const int FunctionRva = 0x51790;
        internal const int FunctionSize = 2774;
        internal const string FunctionHash = "69731F77776995C9FC452A7A9A41408385B757B461F0E7FAB76E291BE64C3ECF";
        private const string FunctionPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private static readonly CompiledBytePattern CompiledFunctionPattern =
            CompiledBytePattern.Parse(FunctionPattern);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteBuildStepDelegate(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced);

        private readonly object sync = new object();
        private readonly List<Registration> registrations = new List<Registration>();
        private readonly HashSet<string> loggedFailures = new HashSet<string>(StringComparer.Ordinal);
        private readonly string binaryHash;
        private readonly ManualLogSource log;
        private readonly DetourHandle<ExecuteBuildStepDelegate> hook = new DetourHandle<ExecuteBuildStepDelegate>();
        private HookTransaction transaction;

        internal AivBuildStepService(string hash, ManualLogSource logger)
        {
            binaryHash = hash;
            log = logger;
        }

        internal static bool TryCreate(
            string hash,
            long moduleBase,
            ReadOnlySpan<byte> memory,
            ScanRegion region,
            ManualLogSource log,
            out AivBuildStepService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            if (!string.Equals(hash, ApiSharedRuntime.SupportedHash, StringComparison.OrdinalIgnoreCase))
            {
                diagnostic = Diagnostic(hash, NativeCapabilityState.UnsupportedBuild,
                    "The installed CrusaderDE.dll is not catalogued for the AIV build-step capability.");
                return false;
            }

            HookTransaction pending = null;
            try
            {
                if (moduleBase == 0 || region == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed,
                        "The native module or RedBird scan region is unavailable.");
                NativePeImage image = NativePeImage.Parse(memory);
                image.RequireExecutableRange(FunctionRva, FunctionSize, "AIV ExecuteBuildStep");
                int match = CompiledFunctionPattern.FindUnique(memory);
                if (match < 0)
                    throw new NativeResolutionException(
                        match == -2 ? NativeCapabilityState.Ambiguous : NativeCapabilityState.PatternMissing,
                        match == -2 ? "The AIV ExecuteBuildStep signature is ambiguous." : "The AIV ExecuteBuildStep signature is missing.");
                if (match != FunctionRva)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed,
                        $"AIV ExecuteBuildStep resolved at 0x{match:X}, expected 0x{FunctionRva:X}.");
                string actualHash = ApiSharedRuntime.ComputeSha256(memory.Slice(FunctionRva, FunctionSize));
                if (!string.Equals(actualHash, FunctionHash, StringComparison.OrdinalIgnoreCase))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed,
                        $"The AIV ExecuteBuildStep function hash changed: expected={FunctionHash}, actual={actualHash}.");

                var candidate = new AivBuildStepService(hash, log);
                pending = new HookTransaction(
                    region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = false
                    });
                pending.AddDetour(
                    candidate.hook,
                    HookTarget.FromAddress(unchecked((ulong)moduleBase) + FunctionRva),
                    candidate.ExecuteBuildStep);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !candidate.hook.Success)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed,
                        "The AIV ExecuteBuildStep detour was not installed atomically.");

                candidate.transaction = pending;
                pending = null;
                service = candidate;
                diagnostic = Diagnostic(hash, NativeCapabilityState.Available,
                    $"AIV ExecuteBuildStep is owned by APIShared at RVA 0x{FunctionRva:X}; function SHA-256={FunctionHash}.");
                NativeApiLog.Info(log, $"capability={NativeCapabilityIds.AivBuildStep}, build={hash}, rva=0x{FunctionRva:X}, status=installed");
                return true;
            }
            catch (NativeResolutionException ex)
            {
                pending?.Dispose();
                diagnostic = Diagnostic(hash, ex.State, ex.Message);
                NativeApiLog.Error(log, $"capability={NativeCapabilityIds.AivBuildStep}, build={hash}, status=unavailable, error={ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                pending?.Dispose();
                diagnostic = Diagnostic(hash, NativeCapabilityState.Faulted, ex.Message);
                NativeApiLog.Error(log, $"capability={NativeCapabilityIds.AivBuildStep}, build={hash}, status=faulted, error={ex}");
                return false;
            }
        }

        internal IAivBuildStepCapability Bind(string ownerGuid) => new Binding(this, ownerGuid);

        private bool Register(
            string ownerGuid,
            string registrationId,
            IAivBuildStepObserver observer,
            out NativeCapabilityDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(registrationId) || observer == null)
            {
                diagnostic = Diagnostic(binaryHash, NativeCapabilityState.ValidationFailed,
                    "A non-empty registration ID and observer are required.");
                return false;
            }

            lock (sync)
            {
                Registration existing = registrations.Find(x =>
                    string.Equals(x.OwnerGuid, ownerGuid, StringComparison.Ordinal) &&
                    string.Equals(x.RegistrationId, registrationId, StringComparison.Ordinal));
                if (existing != null)
                {
                    if (ReferenceEquals(existing.Observer, observer))
                    {
                        diagnostic = Available("The identical AIV build-step observer is already registered.");
                        return true;
                    }
                    diagnostic = Diagnostic(binaryHash, NativeCapabilityState.Conflict,
                        "The owner already uses this AIV build-step registration ID.", ownerGuid);
                    return false;
                }

                registrations.Add(new Registration(ownerGuid, registrationId, observer));
                registrations.Sort(Registration.Compare);
            }
            diagnostic = Available("The AIV build-step observer was registered for the process lifetime.");
            return true;
        }

        private int ExecuteBuildStep(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced)
        {
            var context = new AivBuildStepContext(
                aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            return Dispatch(context, () =>
                hook.Original(aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced));
        }

        internal int DispatchForTest(AivBuildStepContext context, Func<int> original) =>
            Dispatch(context, original);

        private int Dispatch(AivBuildStepContext context, Func<int> original)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (original == null)
                throw new ArgumentNullException(nameof(original));
            Registration[] copy;
            lock (sync)
                copy = registrations.ToArray();
            var active = new List<ActiveInvocation>(copy.Length);
            foreach (Registration registration in copy)
            {
                try
                {
                    IAivBuildStepInvocation invocation = registration.Observer.TryBegin(context);
                    if (invocation != null)
                        active.Add(new ActiveInvocation(registration, invocation));
                }
                catch (Exception ex)
                {
                    LogCallbackFailure(registration, "begin", ex);
                }
            }

            int result = 0;
            bool completed = false;
            try
            {
                result = original();
                completed = true;
                return result;
            }
            finally
            {
                var completion = new AivBuildStepCompletion(context, completed, result);
                for (int index = active.Count - 1; index >= 0; index--)
                {
                    try { active[index].Invocation.Complete(completion); }
                    catch (Exception ex) { LogCallbackFailure(active[index].Registration, "completion", ex); }
                }
            }
        }

        private void LogCallbackFailure(Registration registration, string phase, Exception ex)
        {
            string key = registration.OwnerGuid + ":" + registration.RegistrationId + ":" + phase;
            lock (sync)
            {
                if (!loggedFailures.Add(key))
                    return;
            }
            NativeApiLog.Error(log,
                $"AIV build-step observer {key} failed and was isolated; Vanilla and other observers continue: {ex}");
        }

        private NativeCapabilityDiagnostic Available(string reason) =>
            Diagnostic(binaryHash, NativeCapabilityState.Available, reason);

        private static NativeCapabilityDiagnostic Diagnostic(
            string hash,
            NativeCapabilityState state,
            string reason,
            string conflictOwner = null) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.AivBuildStep, state, hash, reason, conflictOwner);

        private sealed class Binding : IAivBuildStepCapability
        {
            private readonly AivBuildStepService service;
            private readonly string ownerGuid;
            internal Binding(AivBuildStepService service, string ownerGuid)
            {
                this.service = service;
                this.ownerGuid = ownerGuid;
            }

            public bool TryRegisterObserver(
                string registrationId,
                IAivBuildStepObserver observer,
                out NativeCapabilityDiagnostic diagnostic) =>
                service.Register(ownerGuid, registrationId, observer, out diagnostic);
        }

        private sealed class Registration
        {
            internal Registration(string ownerGuid, string registrationId, IAivBuildStepObserver observer)
            {
                OwnerGuid = ownerGuid;
                RegistrationId = registrationId;
                Observer = observer;
            }

            internal string OwnerGuid { get; }
            internal string RegistrationId { get; }
            internal IAivBuildStepObserver Observer { get; }
            internal static int Compare(Registration left, Registration right)
            {
                int owner = string.CompareOrdinal(left.OwnerGuid, right.OwnerGuid);
                return owner != 0 ? owner : string.CompareOrdinal(left.RegistrationId, right.RegistrationId);
            }
        }

        private sealed class ActiveInvocation
        {
            internal ActiveInvocation(Registration registration, IAivBuildStepInvocation invocation)
            {
                Registration = registration;
                Invocation = invocation;
            }

            internal Registration Registration { get; }
            internal IAivBuildStepInvocation Invocation { get; }
        }
    }
}
