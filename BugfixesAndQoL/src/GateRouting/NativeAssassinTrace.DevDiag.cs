// TEMP_GATE_MIGRATION_DIAGNOSTICS: retained for old/new acceptance; see removal inventory.
// TEMP_GATE_ROUTE_ACCEPTANCE: synchronous evidence only; no game queries or persistent unit history.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.GateRouting
{
    internal enum NativeTraceMovement { Unknown, Ground, ClimbUp, ClimbDown }
    internal struct NativeTraceEdge
    {
        internal int From, To, Direction;
        internal int ConnectionBits;
        internal uint SourceFlags, TargetFlags;
        internal NativeTraceMovement Movement;
    }
    internal sealed class NativeAssassinTrace
    {
        internal NativeAssassinTrace Previous;
        internal object Publication;
        internal long Sequence, Epoch;
        internal ulong Manager, Stack, Buffer, ReconstructionStack;
        internal int X, Y, TX, TY, Player, Unit, Result, Mode;
        internal bool CollectEdges = true;
        internal bool Closed, Complete, Invalid, Secondary, SecondaryFallback, ReconstructionFailed;
        internal long Candidates, Improvements, Reconstructions, Superseded, SelectedTotal, FailedReconstructions;
        internal string BufferKind = "unclassified";
        internal readonly List<NativeTraceEdge> Selected = new List<NativeTraceEdge>();
        private NativeTraceEdge best;
        private bool hasBest;

        internal void BeginReconstruction(int mode)
        {
            if (Reconstructions++ != 0) Superseded++;
            Mode = mode; Complete = false; ReconstructionFailed = false; Selected.Clear(); hasBest = false;
        }
        internal void Candidate() { Candidates++; }
        internal void Improve(int current, int neighbor, int reverseDirection, int connectionBits,
            uint forwardSourceFlags, uint forwardTargetFlags)
        {
            Improvements++;
            NativeTraceMovement movement = NativeTraceMovement.Unknown;
            if (Mode == 1 || (Mode == 3 && (connectionBits & (1 << reverseDirection)) != 0))
                movement = NativeTraceMovement.Ground;
            else if (Mode == 3 && (reverseDirection & 1) == 0)
            {
                // This notification is AFTER the actual accepted native candidate branch.
                // It is not a post-hoc attempt to predict that branch from live surfaces.
                bool sourceWall = (forwardSourceFlags & 0x100) != 0;
                bool targetWall = (forwardTargetFlags & 0x100) != 0;
                if (!sourceWall && targetWall) movement = NativeTraceMovement.ClimbUp;
                else if (sourceWall && !targetWall) movement = NativeTraceMovement.ClimbDown;
            }
            best = new NativeTraceEdge { From = neighbor, To = current,
                Direction = (reverseDirection + 4) & 7, Movement = movement, ConnectionBits = connectionBits,
                SourceFlags = forwardSourceFlags, TargetFlags = forwardTargetFlags };
            hasBest = true;
        }
        internal void Select(int current, int neighbor, int reverseDirection)
        {
            SelectedTotal++;
            if (!hasBest || best.From != neighbor || best.To != current || best.Direction != ((reverseDirection + 4) & 7))
            { Invalid = true; return; }
            // Native E1640 writes at most 2000 directions, not a diagnostic event limit.
            if (Selected.Count >= 2000) { Invalid = true; return; }
            Selected.Add(best); hasBest = false;
        }
        internal void FinishReconstruction() { Complete = !Invalid && !ReconstructionFailed; }
        internal void FailReconstruction() { FailedReconstructions++; ReconstructionFailed = true; Complete = false; }
        internal bool Match(int index, int from, int to, int direction, out NativeTraceMovement movement)
        {
            movement = NativeTraceMovement.Unknown;
            if (!Closed || !Complete || Invalid || Result != Selected.Count || (uint)index >= (uint)Selected.Count) return false;
            NativeTraceEdge edge = Selected[Selected.Count - 1 - index];
            if (edge.From != from || edge.To != to || edge.Direction != direction) return false;
            movement = edge.Movement; return true;
        }
        internal string Summary => "nativeTrace=" + Sequence + ",bufferKind=" + BufferKind +
            ",secondaryGroundPassExecution=" + (Invalid || !Closed ? "incomplete" : Secondary ? "executed" : "not-executed") +
            ",secondaryFallback=" + SecondaryFallback + ",reconstructions=" + Reconstructions +
            ",supersededReconstructions=" + Superseded + ",finalReconstructionMode=" + Mode +
            ",failedReconstructions=" + FailedReconstructions +
            ",candidateChecks=" + Candidates + ",candidateImprovements=" + Improvements +
            ",selectedTotal=" + SelectedTotal + ",finalSelected=" + Selected.Count +
            ",nativeResult=" + Result + ",complete=" + Complete + ",invalid=" + Invalid;
    }
}
