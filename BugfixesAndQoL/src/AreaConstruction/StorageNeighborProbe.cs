using System;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using R3;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Extensions;
using SHCDESE.Interop;

namespace BugfixesAndQoL.AreaConstruction
{
    // The Extender already owns the native perimeter function. Its synchronous
    // Post event can augment a failed lookup for the one matching draft hover.
    internal sealed class StorageNeighborProbe
    {
        private const string Feature = "area-construction storage neighbor preview";
        private const int NativeMouseStateRva = 0x8EBA98;
        private static readonly int[] CallSites = { 0x789D9, 0x78A59, 0x78AD9 };
        private static readonly byte[][] Calls =
        {
            new byte[] { 0xE8, 0xE2, 0xE8, 0x04, 0x00 },
            new byte[] { 0xE8, 0x62, 0xE8, 0x04, 0x00 },
            new byte[] { 0xE8, 0xE2, 0xE7, 0x04, 0x00 }
        };

        [ThreadStatic] private static DraftNeighbor active;
        private readonly IntPtr image;
        private readonly ManualLogSource log;
        private IDisposable placementSubscription;
        private IDisposable adjacencySubscription;
        private bool intact;

        private StorageNeighborProbe(IntPtr image, ManualLogSource log)
        {
            this.image = image;
            this.log = log;
        }

        // The caller has hash-verified the native binary. The Extender owns the
        // target entry. Only the audited validator call instructions are checked.
        internal static StorageNeighborProbe TryInstall(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            var probe = new StorageNeighborProbe(context.ModuleHandle, log);
            try
            {
                for (int i = 0; i < CallSites.Length; i++)
                {
                    RequireReference(context.Memory, CallSites[i], Calls[i]);
                    probe.RequireLiveCaller(i);
                }

                // The process-lived Extender publishers keep both subscriptions
                // alive independently of BepInEx's startup component teardown.
                probe.placementSubscription = BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(probe.ObservePlacement);
                probe.adjacencySubscription = BuildingR3EventHooks.OnIsBuildingAdjacentToOther.Observable.Subscribe(probe.ObserveNeighbor);
                Volatile.Write(ref probe.intact, true);
                log.LogInfo(Stamp(Feature + ": Extender adjacency events at the three reference validator calls to RVA 0xC72C0."));
                return probe;
            }
            catch (Exception exception)
            {
                log.LogError(Stamp(Feature + ": " + exception.Message));
                return null;
            }
        }

        internal bool IsIntact => Volatile.Read(ref intact) && placementSubscription != null && adjacencySubscription != null;

        internal void Recheck()
        {
            if (!IsIntact) return;
            try
            {
                for (int i = 0; i < CallSites.Length; i++)
                    RequireLiveCaller(i);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref intact, false);
                log.LogError(Stamp(Feature + ": " + exception.Message));
            }
        }

        internal NeighborScope Scope(int actor, int x, int y, int scale, eStructs structure)
        {
            if (!IsIntact || active.Owner != null)
                return default;
            active = new DraftNeighbor(this, actor, x, y, scale, structure);
            return new NeighborScope(true);
        }

        private void ObservePlacement(BuildingPlacementValidationEventArgs args)
        {
            if (active.Owner != this || !IsIntact)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                if (++active.PlacementDepth == 1)
                    active.Placement = args;
            }
            else if (args.Phase == EventHookPhase.Post && active.PlacementDepth > 0)
            {
                if (--active.PlacementDepth == 0)
                {
                    active.Placement = null;
                    active.Adjacency = null;
                }
            }
        }

        private void ObserveNeighbor(IsGameBuildingAdjacentToOtherEventArgs args)
        {
            if (active.Owner != this || active.Used || active.PlacementDepth != 1 || !IsIntact ||
                !Matches(args) || !CurrentPlacementMatches() || Marshal.ReadInt32(image, NativeMouseStateRva) != 0)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                active.Adjacency = args;
                return;
            }

            if (args.Phase != EventHookPhase.Post)
                return;

            // Pre is still mutable while other subscribers run. Recheck its
            // final arguments. A skipped original has no Post in the Extender.
            var original = active.Adjacency;
            active.Used = true;
            active.Adjacency = null;
            if (args.ReturnValue == 0 && original != null && !original.SkipOriginalFunction && Matches(original))
                args.ReturnValue = 1;
            // The surrounding placement wrapper still applies its own vetoes.
        }

        private static bool CurrentPlacementMatches()
        {
            var args = active.Placement;
            return args != null && args.PlayerId == active.Actor && args.TileX == active.X && args.TileY == active.Y &&
                args.Unknown1 == active.Scale && args.Unknown2 == 1 &&
                args.Mappers.ConvertToEStructs() == active.Structure &&
                !(args.CustomValidationRules && args.ForceBlockPlacementState);
        }

        private static bool Matches(IsGameBuildingAdjacentToOtherEventArgs args) =>
            args.PlayerId == active.Actor && args.TileX == active.X && args.TileY == active.Y &&
            args.PotentialRange == active.Scale && args.Building == active.Structure;

        private static void RequireReference(ReadOnlySpan<byte> snapshot, int rva, byte[] expected)
        {
            if (rva > snapshot.Length - expected.Length ||
                !snapshot.Slice(rva, expected.Length).SequenceEqual(expected))
                throw new InvalidOperationException("Load-image bytes differ at RVA 0x" + rva.ToString("X") + ".");
        }

        private void RequireLiveCaller(int index)
        {
            var expected = Calls[index];
            var current = new byte[expected.Length];
            Marshal.Copy(IntPtr.Add(image, CallSites[index]), current, 0, current.Length);
            if (!new ReadOnlySpan<byte>(current).SequenceEqual(expected))
                throw new InvalidOperationException("Storage caller RVA 0x" + CallSites[index].ToString("X") +
                    " expected " + BitConverter.ToString(expected) + ", found " + BitConverter.ToString(current) + ".");
        }

        private static string Stamp(string message) => $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        internal readonly struct NeighborScope : IDisposable
        {
            private readonly bool ownsScope;
            internal NeighborScope(bool ownsScope) { this.ownsScope = ownsScope; }
            public void Dispose()
            {
                if (ownsScope) active = default;
            }
        }

        private struct DraftNeighbor
        {
            internal readonly StorageNeighborProbe Owner;
            internal readonly int Actor, X, Y, Scale;
            internal readonly eStructs Structure;
            internal int PlacementDepth;
            internal bool Used;
            internal BuildingPlacementValidationEventArgs Placement;
            internal IsGameBuildingAdjacentToOtherEventArgs Adjacency;

            internal DraftNeighbor(StorageNeighborProbe owner, int actor, int x, int y, int scale, eStructs structure) : this()
            {
                Owner = owner;
                Actor = actor;
                X = x;
                Y = y;
                Scale = scale;
                Structure = structure;
            }
        }
    }
}
