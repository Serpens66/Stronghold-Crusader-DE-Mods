#if SERP_DEVDIAG
using APIShared.NativeSites;
using APIShared.Simulation;
using APIShared;
using R3;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using static BugfixesAndQoL.AiOutpostRetargetNative;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class AiOutpostRetargetFix
    {
        private OutpostTrace diagnostic;

        partial void InstallDiagnostic(CrusaderLibraryLoadContext context)
        {
            try
            {
                diagnostic = new OutpostTrace(this);
                diagnostic.Install(context);
            }
            catch (Exception error)
            {
                diagnostic?.Fail(error);
            }
        }

        partial void ObserveRetarget(int tribeId, uint globalId, int oldTarget, int newTarget) =>
            diagnostic?.Retarget(tribeId, globalId, oldTarget, newTarget);

        private sealed class OutpostTrace
        {
            private const string Name = "AI.OutpostTrace";
            private const string Owner = BugfixesAndQoLPlugin.PluginGuid;
            private const int GateRva = 0x30651;
            private const int MaxWaves = 256, MaxMembersPerWave = 256, MaxOrderEvents = 48;
            private static readonly byte[] GateBytes = {
                0x48, 0x8D, 0x3D, 0xC8, 0x60, 0xC9, 0x07, 0x85, 0xC0, 0x74, 0x19, 0x45, 0x33, 0xFF
            };
            private static readonly int[] SampleDelays = { 0, 10, 40, 120, 400, 1000, 1600 };
            private readonly AiOutpostRetargetFix runtime;
            private readonly object stateLock = new object();
            private readonly Dictionary<(int, uint), Wave> waves = new Dictionary<(int, uint), Wave>();
            private readonly (int Slot, uint Global, int X, int Y)[] lords =
                new (int, uint, int, int)[GamePlayerManagerAPI.MAX_PLAYERS + 1];
            private readonly int?[] lordSampleTicks = new int?[GamePlayerManagerAPI.MAX_PLAYERS + 1];
            private readonly List<int> members = new List<int>();
            private readonly HookHandle<X64InlineHook> gate = new HookHandle<X64InlineHook>();
            private HookTransaction hooks;
            private IDisposable lifecycle, moveOrders, attackOrders, meleeKills, projectileKills;
            private bool failed, waveCapLogged;
            private volatile bool active;
            private long session;
            private int lastTick, unitRows, tickFallbacks;

            internal OutpostTrace(AiOutpostRetargetFix runtime) { this.runtime = runtime; }

            internal void Install(CrusaderLibraryLoadContext context)
            {
                // EAX already contains 0x2DC30's result. The displaced TEST overwrites
                // flags before the original JE to the production hook's entry at 0x30675.
                runtime.VerifyCode(context, GateRva, GateBytes);
                runtime.VerifyCode(context, 0x3064C, new byte[] { 0xE8, 0xDF, 0xD5, 0xFF, 0xFF });
                runtime.VerifyCode(context, 0x3065F, new byte[] { 0x66, 0x45, 0x89, 0xBC, 0x3E, 0x7E, 0x06, 0, 0 });
                var claim = NativeSiteRegistry.Register(Owner, Name,
                    new[] { new NativeSiteSpan(GateRva, GateBytes.Length, NativeSiteMode.Exclusive) });
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);

                try
                {
                    ulong entry = Address(runtime.image + GateRva);
                    using (var probe = new X64InlineHook(entry, GateBytes.Length))
                        if (probe.DisplacedByteCount != GateBytes.Length)
                            throw new InvalidOperationException("Unexpected diagnostic gate span.");

                    hooks = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
                    BugfixesHookInfrastructure.AddContextHook(hooks, gate, entry, OnGate,
                        X64SmartCPUContextRegs.All, hookSize: GateBytes.Length);
                    runtime.VerifyCode(context, GateRva, GateBytes);
                    var result = hooks.Commit();
                    if (!result.IsCompleteSuccess || !gate.Success || !gate.IsInstalled ||
                        gate.ResolvedAddress != entry || gate.Require().DisplacedByteCount != GateBytes.Length)
                        throw new InvalidOperationException("Diagnostic gate commit failed: " + result);
                }
                catch
                {
                    hooks?.Dispose();
                    if (runtime.LiveMatches(GateRva, GateBytes)) NativeSiteRegistry.Unregister(Owner, Name);
                    throw;
                }

                Shared.DevDiag.Attach(runtime.log);
                if (!SimulationTickEvents.TryRegister(Owner, Name, OnTick, out string reason))
                    throw new InvalidOperationException(reason);
                moveOrders = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(args =>
                    ObserveOrder(args.TribeId, args.Phase == EventHookPhase.Post, "move",
                        args.TileX, args.TileY, (int)args.MoveType, args.SkipOriginalFunction, args.ReturnValue));
                attackOrders = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(args =>
                    ObserveOrder(args.TribeId, args.Phase == EventHookPhase.Post, "target",
                        args.TargetValue1, args.TargetValue2, (int)args.AICommand, args.SkipOriginalFunction, args.ReturnValue));
                meleeKills = UnitR3EventHooks.OnUnitKilledByMelee.Observable.Subscribe(args =>
                    ObserveKill(args.AttackedUnitId, args.AttackingUnitId, 0));
                projectileKills = UnitR3EventHooks.OnUnitKilledByProjectile.Observable.Subscribe(args =>
                    ObserveKill(args.AttackedUnitId, 0, args.ProjectileId));
                lifecycle = Shared.GameplaySessionLifecycle.SubscribeStarted(runtime.log, Start, End);
                Shared.DevDiag.Log(() => "OUTPOST_TRACE_READY gate=0x30651 span=14 continuation=0x3065F readyBranch=0x30675");
            }

            private void Start(Shared.GameplaySessionStartedContext context)
            {
                lock (stateLock)
                {
                    string map = Path.GetFileNameWithoutExtension(context.SaveFileName ?? "");
                    string name = context.Notification.Context.MapName ?? "";
                    if (failed || context.IsEditor ||
                        (!IsTestMap(map) && !IsTestMap(name))) return;
                    session = context.SessionId;
                    active = true;
                    Shared.DevDiag.Log(() => $"OUTPOST_TRACE_START session={session} map={map} name={name} {context.Mode.ToDiagnosticString()}");
                }
            }

            private static bool IsTestMap(string map) =>
                string.Equals(map.Replace(" ", ""), "CenterOfPower", StringComparison.OrdinalIgnoreCase);

            private void End()
            {
                lock (stateLock)
                {
                    if (active)
                        Shared.DevDiag.Log(() => $"OUTPOST_TRACE_END session={session} tick={lastTick} waves={waves.Count} tickFallbacks={tickFallbacks}");
                    active = false;
                    waves.Clear();
                    Array.Clear(lords, 0, lords.Length);
                    Array.Clear(lordSampleTicks, 0, lordSampleTicks.Length);
                    unitRows = 0;
                    tickFallbacks = 0;
                    waveCapLogged = false;
                    lastTick = 0;
                }
            }

            internal void Fail(Exception error)
            {
                lock (stateLock)
                {
                    if (failed) return;
                    failed = true;
                    active = false;
                    Shared.DevDiag.Attach(runtime.log);
                    Shared.DevDiag.Log(() => "OUTPOST_TRACE_ERROR " + error);
                }
            }

            private void OnGate(NativePointer<X64SmartCPUContext> context)
            {
                if (!active) return;
                try
                {
                    lock (stateLock)
                    {
                        if (!active) return;
                        var regs = context.Pointer;
                        if (regs == null || regs->R13 != Address(runtime.image + AiManagerRva))
                            throw new InvalidOperationException("Unexpected native outpost caller context.");
                        int id = unchecked((int)regs->RSI);
                        if (!TryTribe(id, out var tribe)) return;
                        int tick = ReadTick();
                        int target = *(short*)((byte*)tribe + TargetPlayerOffset);
                        var key = (id, tribe->r_GlobalId);
                        if (!waves.TryGetValue(key, out Wave wave))
                        {
                            if (waves.Count == MaxWaves)
                            {
                                if (!waveCapLogged)
                                {
                                    waveCapLogged = true;
                                    Shared.DevDiag.Log(() => $"OUTPOST_TRACE_LIMIT session={session} tick={tick} waves={MaxWaves}");
                                }
                                return;
                            }
                            wave = new Wave(id, tribe->r_GlobalId, tribe->r_PlayerIdOwner, target);
                            waves.Add(key, wave);
                            Shared.DevDiag.Log(() => $"OUTPOST_TRACE_WAVE {Prefix(wave, tick)} target={target} members={tribe->r_UnitsInGroup}");
                        }
                        wave.Target = target;
                        wave.LastGateTick = tick;
                        if (!wave.Complete) RememberMembers(wave, tick);
                        ObserveLord(target, tick);

                        int busy = unchecked((int)regs->RAX);
                        if (wave.StartTick.HasValue && !wave.Complete && wave.LastGate != busy)
                        {
                            wave.LastGate = busy;
                            Shared.DevDiag.Log(() => $"OUTPOST_TRACE_GATE {Prefix(wave, tick)} target={target} gateResult={busy} members={tribe->r_UnitsInGroup}");
                        }
                    }
                }
                catch (Exception error) { Fail(error); }
            }

            private void ObserveLord(int player, int tick)
            {
                if (player <= 0 || player >= lords.Length || lordSampleTicks[player] == tick) return;
                lordSampleTicks[player] = tick;
                int slot = *(short*)((byte*)runtime.image + LordSlotsRva + player * sizeof(short));
                if (slot < 0 || slot > GameUnitManager.LastLiveUnitId) return;
                int nativeLord = (int)runtime.findLord(runtime.image + UnitManagerRva, player);
                if (nativeLord != 0 && UnitAccess.TryGetById(nativeLord, out var unit, out _))
                {
                    lords[player] = (nativeLord, unit->r_GlobalId, unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                    return;
                }
                if (lords[player].Global != 0) BeginCapture(player, tick, "native-lord-lost");
            }

            private void ObserveKill(long victim, int attacker, long projectile)
            {
                if (!active || victim <= 0 || victim > GameUnitManager.LastLiveUnitId) return;
                try
                {
                    lock (stateLock)
                    {
                        if (!active || !UnitAccess.TryGetById((int)victim, out var killed, out _)) return;
                        for (int player = 1; player < lords.Length; player++)
                        {
                            var lord = lords[player];
                            if (lord.Slot != victim || lord.Global == 0 || lord.Global != killed->r_GlobalId) continue;
                            int tick = ReadTick();
                            uint attackerGlobal = 0;
                            int attackerTribe = 0;
                            if (attacker > 0 && UnitAccess.TryGetById(attacker, out var unit, out _))
                            {
                                attackerGlobal = unit->r_GlobalId;
                                attackerTribe = unit->r_TribeId;
                            }
                            Shared.DevDiag.Log(() => $"OUTPOST_TRACE_LORD_KILL session={session} tick={tick} player={player} lord={victim}/{lord.Global} attacker={attacker}/{attackerGlobal} attackerTribe={attackerTribe} projectile={projectile}");
                            BeginCapture(player, tick, "extender-kill");
                            return;
                        }
                    }
                }
                catch (Exception error) { Fail(error); }
            }

            private void BeginCapture(int player, int tick, string cause)
            {
                bool announced = false;
                foreach (Wave wave in waves.Values)
                {
                    if (wave.Target != player || wave.StartTick.HasValue) continue;
                    wave.StartTick = tick;
                    if (!announced)
                    {
                        announced = true;
                        var lord = lords[player];
                        Shared.DevDiag.Log(() => $"OUTPOST_TRACE_LOSS session={session} tick={tick} player={player} cause={cause} lastLord={lord.Slot}/{lord.Global} tile={lord.X},{lord.Y}");
                    }
                    Sample(wave, tick, "at-loss");
                }
            }

            internal void Retarget(int id, uint globalId, int oldTarget, int newTarget)
            {
                if (!active) return;
                try
                {
                    lock (stateLock)
                    {
                        if (!active || !waves.TryGetValue((id, globalId), out Wave wave)) return;
                        int tick = ReadTick();
                        if (!wave.StartTick.HasValue) BeginCapture(oldTarget, tick, "vanilla-reselection");
                        wave.Target = newTarget;
                        Shared.DevDiag.Log(() => $"OUTPOST_TRACE_RETARGET {Prefix(wave, tick)} old={oldTarget} new={newTarget}");
                    }
                }
                catch (Exception error) { Fail(error); }
            }

            private void ObserveOrder(int id, bool post, string kind, int a, int b, int command, bool skipped, long result)
            {
                if (!active) return;
                try
                {
                    lock (stateLock)
                    {
                        if (!active || !TryTribe(id, out var tribe) ||
                            !waves.TryGetValue((id, tribe->r_GlobalId), out Wave wave) ||
                            !wave.StartTick.HasValue || wave.Complete || wave.OrderEvents >= MaxOrderEvents) return;
                        wave.OrderEvents++;
                        int tick = ReadTick();
                        Shared.DevDiag.Log(() => $"OUTPOST_TRACE_ORDER_REQUEST {Prefix(wave, tick)} phase={(post ? "post" : "pre")} kind={kind} command={command} a={a} b={b} skipSeen={skipped} result={result}");
                        if (post && wave.OrderEvents <= 8) Sample(wave, tick, kind + "-post");
                    }
                }
                catch (Exception error) { Fail(error); }
            }

            private void OnTick(int tick)
            {
                if (!active) return;
                try
                {
                    lock (stateLock)
                    {
                        if (!active) return;
                        lastTick = tick;
                        // Check Lord loss before sampling even if no group got the normal update this pass.
                        if ((tick & 7) == 0)
                            for (int player = 1; player < lords.Length; player++)
                                if (lords[player].Global != 0) ObserveLord(player, tick);
                        foreach (Wave wave in waves.Values)
                        {
                            if (!wave.StartTick.HasValue || wave.Complete ||
                                unchecked(tick - wave.StartTick.Value) < SampleDelays[wave.SampleIndex]) continue;
                            Sample(wave, tick, "checkpoint");
                            if (++wave.SampleIndex == SampleDelays.Length)
                                Shared.DevDiag.Log(() => $"OUTPOST_TRACE_DONE {Prefix(wave, tick)} orderEvents={wave.OrderEvents} tracked={wave.Members.Count} capped={wave.MemberCap}");
                        }
                    }
                }
                catch (Exception error) { Fail(error); }
            }

            private int ReadTick()
            {
                if (SimulationTickEvents.TryGetCurrentTick(out int tick)) return tick;
                tickFallbacks++;
                if (tickFallbacks == 1)
                    Shared.DevDiag.Log(() => $"OUTPOST_TRACE_TICK_FALLBACK session={session} lastTick={lastTick}");
                return lastTick;
            }

            private static bool TryTribe(int id, out GameTribe* tribe)
            {
                tribe = null;
                var tribes = GameTribeManagerAPI.Instance;
                return id > 0 && tribes.IsValidId(id) && tribes.TryGetTribeById(id, out tribe) && tribe != null;
            }

            private void RememberMembers(Wave wave, int tick)
            {
                if (wave.LastMemberTick.HasValue && unchecked(tick - wave.LastMemberTick.Value) < 12) return;
                wave.LastMemberTick = tick;
                members.Clear();
                if (!GameTribeManagerAPI.Instance.GetUnits(wave.Id, members)) return;
                foreach (int slot in members)
                {
                    if (!UnitAccess.TryGetById(slot, out var unit, out _)) continue;
                    var identity = (slot, unit->r_GlobalId);
                    if (wave.Members.Count < MaxMembersPerWave) wave.Members.Add(identity);
                    else if (!wave.Members.Contains(identity)) wave.MemberCap = true;
                }
            }

            private void Sample(Wave wave, int tick, string phase)
            {
                bool sameGroup = TryTribe(wave.Id, out var tribe) && tribe->r_GlobalId == wave.Global;
                int membersNow = sameGroup ? tribe->r_UnitsInGroup : 0;
                members.Clear();
                bool membershipReadable = sameGroup && GameTribeManagerAPI.Instance.GetUnits(wave.Id, members);
                int current = 0, detached = 0, dead = 0, missing = 0, unknown = 0;
                foreach (var identity in wave.Members)
                {
                    if (!UnitAccess.TryGetById(identity.Slot, out var unit, out _) || unit->r_GlobalId != identity.Global)
                    {
                        missing++;
                        LogUnit(() => $"OUTPOST_TRACE_UNIT {Prefix(wave, tick)} phase={phase} unit={identity.Slot}/{identity.Global} relation=missing-or-reused");
                        continue;
                    }
                    string relation;
                    if (unit->r_CurrentHealth == 0 || unit->r_IsKilledByProjectile != 0 ||
                        unit->r_AliveState == SHCDESE.Interop.Enums.AliveState.MarkedForDeletion)
                    { relation = "dead"; dead++; }
                    else if (membershipReadable && unit->r_TribeId == wave.Id && members.Contains(identity.Slot)) { relation = "member"; current++; }
                    else if (unit->r_TribeId != wave.Id || membershipReadable) { relation = "detached"; detached++; }
                    else { relation = "membership-unknown"; unknown++; }
                    // 0x11DD10 writes GameUnit+0x428. 0x11B520 reads it as a member exclusion.
                    ushort moveExcluded = *(ushort*)((byte*)unit + 0x428);
                    string row = $"OUTPOST_TRACE_UNIT {Prefix(wave, tick)} phase={phase} unit={identity.Slot}/{identity.Global} relation={relation} currentTribe={unit->r_TribeId} type={unit->r_UnitChimp} alive={unit->r_AliveState} hp={unit->r_CurrentHealth} death={unit->r_IsKilledByProjectile} state={unit->r_AIState} command={unit->r_AI_LastIssuedTribeCommand} context={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId} attacker={unit->r_AttackingUnitId}/{unit->r_AttackingUnitGlobalId} tile={unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY} goal={unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY} path={unit->r_PathPlanStateBitFlags}/{unit->r_CurrentPathPlanIndex}/{unit->r_PathPlanLength} moveExcluded={moveExcluded}";
                    LogUnit(() => row);
                }
                Shared.DevDiag.Log(() => $"OUTPOST_TRACE_SAMPLE {Prefix(wave, tick)} phase={phase} sameGroup={sameGroup} membershipReadable={membershipReadable} membersNow={membersNow} lastGateTick={wave.LastGateTick} target={wave.Target} tracked={wave.Members.Count} member={current} detached={detached} dead={dead} missing={missing} unknown={unknown} capped={wave.MemberCap}");
            }

            private void LogUnit(Func<string> message)
            {
                const int maxUnitRows = 24000;
                if (unitRows < maxUnitRows) Shared.DevDiag.Log(message);
                else if (unitRows == maxUnitRows)
                    Shared.DevDiag.Log(() => $"OUTPOST_TRACE_LIMIT session={session} unitRows={maxUnitRows}");
                if (unitRows <= maxUnitRows) unitRows++;
            }

            private string Prefix(Wave wave, int tick) =>
                $"session={session} tick={tick} tribe={wave.Id}/{wave.Global} owner={wave.OwnerId}";

            private sealed class Wave
            {
                internal readonly int Id, OwnerId;
                internal readonly uint Global;
                internal readonly HashSet<(int Slot, uint Global)> Members = new HashSet<(int, uint)>();
                internal int Target, OrderEvents, SampleIndex, LastGateTick;
                internal int? StartTick, LastGate, LastMemberTick;
                internal bool MemberCap;
                internal bool Complete => SampleIndex == SampleDelays.Length;

                internal Wave(int id, uint global, int owner, int target)
                {
                    Id = id;
                    Global = global;
                    OwnerId = owner;
                    Target = target;
                }
            }
        }
    }
}
#endif
