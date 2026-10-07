using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace APIShared
{
    /// <summary>A synchronous Assassin distance-field builder, using Vanilla's complete native arguments.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate int AssassinPathBuilder(IntPtr context, int startX, int startY,
        int targetX, int targetY, int maximumNodes, int continuation);

    /// <summary>
    /// Process-wide owner of Assassin distance-field and reconstruction hooks.
    /// Consumers register behavior; no consumer installs a second hook at these sites.
    /// </summary>
    public static unsafe class AssassinPathAPI
    {
        internal const string ReferenceHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int BuildingGridRva = 0x4B6AA50;
        internal const int TileFlagsRva = 0x48F71B0;
        internal const int BuildingTypeBaseRva = 0x64CCCDE;
        internal const int BuildingAliveBaseRva = 0x64CCCDC;
        internal const int TileCount = 320800;
        internal const int BuildingStride = 0x32C;
        private static readonly object Sync = new object();
        private static IntPtr module;
        private static int memoryLength;
        private static ScanRegion region;
        private static ManualLogSource log;
        private static bool supported;
        private static HookTransaction transaction;
        private static DetourHandle<AssassinPathBuilder> builderHook;
        private static HookHandle<X64InlineHook>[] endpointHooks;
        private static readonly IntPtr policyFlags = Marshal.AllocHGlobal(sizeof(int));
        private static AssassinPathBuilder weightedBuilder;
        private static string weightedOwner;
        private static string gateOwner;
        private static int flags;
        private static int callbackLogged;
        internal static readonly NativeDetourBackend Backend = new NativeDetourBackend(
            new NativeDetourOptions { AllowedSchemes = DetourScheme.Indirect, FollowJumps = false });

        static AssassinPathAPI() { Marshal.WriteInt32(policyFlags, 0); }

        /// <summary>Whether the sole shared hook transaction has been published successfully.</summary>
        public static bool IsInstalled => builderHook != null && builderHook.Success && builderHook.IsInstalled;
        /// <summary>Whether a test consumer currently enables direct gatehouse climb eligibility.</summary>
        public static bool DirectGatehouseClimbingEnabled => (Volatile.Read(ref flags) & 2) != 0;

        internal static void Initialize(IntPtr nativeModule, ReadOnlySpan<byte> memory,
            ScanRegion nativeRegion, string hash, ManualLogSource logger)
        {
            lock (Sync)
            {
                module = nativeModule;
                memoryLength = memory.Length;
                region = nativeRegion;
                supported = string.Equals(hash, ReferenceHash, StringComparison.OrdinalIgnoreCase);
                log = logger;
            }
        }

        /// <summary>
        /// Registers the sole weighted builder. The handler must call RunVanillaBuilder exactly
        /// once before any optional replacement, and handle its own correction/diagnostic errors.
        /// The delegate remains rooted until process exit.
        /// </summary>
        public static void RegisterWeightedBuilder(string ownerGuid, AssassinPathBuilder handler)
        {
            if (string.IsNullOrEmpty(ownerGuid)) throw new ArgumentException("Owner GUID required.", nameof(ownerGuid));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (Sync)
            {
                if (weightedOwner != null && !string.Equals(weightedOwner, ownerGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("Assassin weighted builder already belongs to " + weightedOwner);
                EnsureInstalled();
                weightedOwner = ownerGuid;
                Volatile.Write(ref weightedBuilder, handler);
            }
        }

        /// <summary>Runs Vanilla through the shared original trampoline; it never calls a registered replacement.</summary>
        public static int RunVanillaBuilder(IntPtr context, int startX, int startY,
            int targetX, int targetY, int maximumNodes, int continuation)
        {
            if (!IsInstalled) throw new InvalidOperationException("Shared Assassin path hooks are unavailable.");
            return builderHook.Original(context, startX, startY, targetX, targetY, maximumNodes, continuation);
        }

        /// <summary>Publishes the weighted builder's existing reconstruction relaxation as logical data only.</summary>
        public static void SetWeightedReconstructionEnabled(string ownerGuid, bool enabled)
        {
            lock (Sync)
            {
                if (!string.Equals(weightedOwner, ownerGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("Only the registered weighted builder can change its reconstruction policy.");
                PublishFlags(enabled ? flags | 1 : flags & ~1);
            }
        }

        /// <summary>Enables or disables the registered experiment's gatehouse endpoint rule without repatching code.</summary>
        public static void SetDirectGatehouseClimbing(string ownerGuid, bool enabled)
        {
            if (string.IsNullOrEmpty(ownerGuid)) throw new ArgumentException("Owner GUID required.", nameof(ownerGuid));
            lock (Sync)
            {
                if (gateOwner != null && !string.Equals(gateOwner, ownerGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("Assassin gatehouse rule already belongs to " + gateOwner);
                EnsureInstalled();
                gateOwner = ownerGuid;
                PublishFlags(enabled ? flags | 2 : flags & ~2);
            }
        }

        /// <summary>
        /// Checks only the optional building-ID exception for one tile. Callers must still enforce
        /// Vanilla's direction, surface and wall requirements and their own player policy.
        /// </summary>
        public static bool IsDirectGatehouseClimbEndpoint(int tileId)
        {
            if (!IsInstalled || !DirectGatehouseClimbingEnabled || (uint)tileId >= TileCount) return false;
            ushort buildingId = ((ushort*)((byte*)module + BuildingGridRva))[tileId];
            if (buildingId == 0 || buildingId > 3999) return false;
            if ((((uint*)((byte*)module + TileFlagsRva))[tileId] & (uint)TilePropertyFlag.IsWall) == 0) return false;
            int record = buildingId * BuildingStride;
            short alive = *(short*)((byte*)module + BuildingAliveBaseRva + record);
            if (alive == (short)AliveState.None || alive == (short)AliveState.MarkedForDeletion) return false;
            eStructs type = *(eStructs*)((byte*)module + BuildingTypeBaseRva + record);
            return type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER;
        }

        private static void PublishFlags(int value)
        {
            Marshal.WriteInt32(policyFlags, value); // Aligned data publication; executable memory never changes.
            Volatile.Write(ref flags, value);
        }

        private static int Build(IntPtr context, int startX, int startY, int targetX,
            int targetY, int maximumNodes, int continuation)
        {
            if (Interlocked.Exchange(ref callbackLogged, 1) == 0)
                NativeApiLog.Info(log, "Assassin shared runtime hook confirmed after startup; " +
                    $"source=({startX},{startY}), target=({targetX},{targetY}), limit={maximumNodes}, continuation={continuation}.");
            AssassinPathBuilder handler = Volatile.Read(ref weightedBuilder);
            return handler == null
                ? RunVanillaBuilder(context, startX, startY, targetX, targetY, maximumNodes, continuation)
                : handler(context, startX, startY, targetX, targetY, maximumNodes, continuation);
        }

        private static void EnsureInstalled()
        {
            if (IsInstalled) return;
            if (!supported || module == IntPtr.Zero || region == null)
                throw new InvalidOperationException("Shared Assassin hooks require the audited native hash and loaded library.");
            var memory = new ReadOnlySpan<byte>(module.ToPointer(), memoryLength);
            AssassinPathNativeDefinition.Validate(memory, unchecked((ulong)module.ToInt64()), log);
            var pendingBuilder = new DetourHandle<AssassinPathBuilder>();
            var pendingEndpoints = new HookHandle<X64InlineHook>[4];
            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true, Backend = Backend });
                ulong nativeBase = unchecked((ulong)module.ToInt64());
                for (int index = 0; index < pendingEndpoints.Length; index++)
                {
                    int site = index;
                    pendingEndpoints[index] = new HookHandle<X64InlineHook>();
                    pending.AddInline(pendingEndpoints[index],
                        HookTarget.FromAddress(nativeBase + (uint)AssassinPathNativeDefinition.Sites[index]),
                        (assembler, instructions, returnAddress) => AssassinEndpointEmitter.Emit(
                            assembler, instructions, nativeBase, unchecked((ulong)policyFlags.ToInt64()), site),
                        hookSize: 14);
                }
                pending.AddDetour(pendingBuilder, HookTarget.FromAddress(nativeBase + 0xD9C40), (AssassinPathBuilder)Build);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !pendingBuilder.Success)
                    throw new InvalidOperationException("Shared Assassin hook transaction failed.");
                AssassinPathNativeDefinition.ValidateInstalledDetour(pendingBuilder.Hook, nativeBase);
                for (int index = 0; index < pendingEndpoints.Length; index++)
                    if (!pendingEndpoints[index].Success || !pendingEndpoints[index].IsInstalled ||
                        pendingEndpoints[index].Hook.DisplacedByteCount != AssassinPathNativeDefinition.Lengths[index])
                        throw new InvalidOperationException("Shared Assassin endpoint displacement mismatch at site " + index);
                transaction = pending;
                endpointHooks = pendingEndpoints;
                builderHook = pendingBuilder; // Publication; no cleanup path can dispose this transaction.
                NativeApiLog.Info(log, "Assassin shared hooks installed; owner=APIShared, builder=0xD9C40, " +
                    "scheme=Indirect/displaced=10, endpoints=0xD9F0C/16,0xD9F1C/15,0xE19D8/18,0xE19F9/23.");
            }
            catch (Exception ex)
            {
                if (builderHook == pendingBuilder) throw; // Published hooks must never be rolled back.
                pending?.Dispose(); // Unpublished installation candidate only.
                NativeApiLog.Error(log, "Shared Assassin hooks unavailable; Vanilla retained: " + ex);
                throw;
            }
        }
    }
}
