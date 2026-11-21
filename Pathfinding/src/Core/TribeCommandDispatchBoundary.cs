using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared.Patching;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RedBird.Core.Memory;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding
{
    // Own only the managed SE publishers, never their native interception sites. The
    // outer wrappers close scopes even when SE deliberately emits no Post after a veto.
    internal static class TribeCommandDispatchBoundary
    {
        private static Hook moveHook, targetHook;
        private static SafeIlHook acceptedHook;
        private static BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate moveOriginal;
        private static BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate targetOriginal;
        private static ManualLogSource log;
        private static Action<TribeIssueOrderWithTargetEventArgs> reconcileQueue, cancelClimb, cancelMovement;
        private static AssassinPathfindingRuntime assassin;
        private static int ready, failed;
        [ThreadStatic] private static int depth;

        internal static bool IsAvailable => Volatile.Read(ref ready) != 0 &&
            Volatile.Read(ref failed) == 0 && acceptedHook != null && acceptedHook.IsApplied;
        internal static bool QueueHooksApplied => IsAvailable && moveHook?.IsApplied == true && targetHook?.IsApplied == true;
        internal static bool IsDispatching => depth != 0 && IsAvailable;
        internal static void BindAssassin(AssassinPathfindingRuntime owner) => assassin = owner;
        internal static void BindClimb(Action<TribeIssueOrderWithTargetEventArgs> callback) => cancelClimb = callback;
        internal static void BindMovement(Action<TribeIssueOrderWithTargetEventArgs> callback) => cancelMovement = callback;
        internal static void BindQueue(Action<TribeIssueOrderWithTargetEventArgs> callback) => reconcileQueue = callback;

        internal static void Initialize(ManualLogSource logger)
        {
            if (IsAvailable) return;
            if (moveHook != null || targetHook != null || acceptedHook != null)
                throw new InvalidOperationException("The published command boundary is unavailable; restart is required.");
            log = logger;
            Type type = typeof(BulkTribeDetours);
            var move = type.GetMethod(nameof(BulkTribeDetours.c_game_tribe_issueorder_movehere_hook_impl),
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(NativePointer<GameTribeManager>), typeof(int),
                    typeof(int), typeof(int), typeof(short), typeof(int), typeof(TribeMoveType) }, null);
            var target = type.GetMethod(nameof(BulkTribeDetours.c_game_tribe_issueorder_withtarget_hook_impl),
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(NativePointer<GameTribeManager>), typeof(int),
                    typeof(TribeAICommand), typeof(int), typeof(int), typeof(int) }, null);
            if (move == null || target == null || move.ReturnType != typeof(long) || target.ReturnType != typeof(long))
                throw new MissingMethodException("The installed SE command publisher signature changed.");
            // SafeIlHook validates and compiles the actual transform before publication.
            if (!SafeIlHook.TryInstall(PathfindingPlugin.PluginGuid, "Pathfinding.Command.AcceptedTarget", target,
                    TransformAcceptedTarget, log, out acceptedHook, out string reason))
                throw new InvalidOperationException(reason);
            acceptedHook.Rebuilt += OnRebuilt;
            // Assign each root before Apply. A successfully published member is retained
            // permanently if a later member fails; ready remains false for that cohort.
            moveHook = new Hook(move, (BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate)Move,
                new HookConfig { ManualApply = true, ID = "Pathfinding.Command.MoveLifetime" });
            moveOriginal = moveHook.GenerateTrampoline<BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate>();
            moveHook.Apply();
            targetHook = new Hook(target, (BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate)Target,
                new HookConfig { ManualApply = true, ID = "Pathfinding.Command.TargetLifetime" });
            targetOriginal = targetHook.GenerateTrampoline<BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate>();
            targetHook.Apply();
            if (!acceptedHook.IsApplied) throw new InvalidOperationException(acceptedHook.Diagnostic);
            Volatile.Write(ref ready, 1);
        }

        private static void OnRebuilt(SafeIlHook hook)
        {
            // MonoMod holds its patch lock on an arbitrary thread. No simulation or
            // Unity work here; the simulation consumer observes the permanent latch.
            if (!hook.IsApplied) Interlocked.Exchange(ref failed, 1);
        }

        internal static void TransformAcceptedTarget(ILContext il)
        {
            var instructions = il.Body.Instructions;
            string args = typeof(TribeIssueOrderWithTargetEventArgs).FullName;
            var raises = instructions.Where(i => i.Operand is MethodReference method && method.Name == "Raise" &&
                method.DeclaringType is GenericInstanceType generic && generic.GenericArguments.Count == 1 &&
                generic.GenericArguments[0].FullName == args).ToArray();
            if (raises.Length != 2) throw new InvalidOperationException("SE target publisher must have exactly Pre and Post Raise calls.");
            int index = instructions.IndexOf(raises[0]);
            if (index < 2 || !instructions[index - 1].MatchLdloc(out int local) ||
                index + 3 >= instructions.Count || !instructions[index + 1].MatchLdloc(local) ||
                !(instructions[index + 2].Operand is MethodReference skip) ||
                skip.DeclaringType.FullName != typeof(EventHookBase).FullName || skip.Name != "get_SkipOriginalFunction" ||
                (instructions[index + 3].OpCode != OpCodes.Brtrue && instructions[index + 3].OpCode != OpCodes.Brtrue_S))
                throw new InvalidOperationException("SE target Pre Raise/argument/Skip decision changed.");
            // The same typed local must originate in the Pre constructor; neither a
            // second event nor an unrelated local may masquerade as the acceptance point.
            var stores = instructions.Take(index).Where(i => i.MatchStloc(local)).ToArray();
            if (stores.Length != 1 || !(stores[0].Previous?.Operand is MethodReference ctor) ||
                ctor.Name != ".ctor" || ctor.DeclaringType.FullName != args || ctor.Parameters.Count != 6)
                throw new InvalidOperationException("SE target Pre argument provenance changed.");
            int constructor = instructions.IndexOf(stores[0].Previous);
            if (constructor < 6 || !instructions[constructor - 6].MatchLdcI4((int)EventHookPhase.Pre) ||
                !Enumerable.Range(1, 5).All(argument => instructions[constructor - 6 + argument].MatchLdarg(argument)))
                throw new InvalidOperationException("SE target event is no longer constructed as Pre from the five dispatch arguments.");
            object destination = instructions[index + 3].Operand;
            var branch = destination is ILLabel label ? label.Target : destination as Instruction;
            if (branch == null || instructions.IndexOf(branch) <= instructions.IndexOf(raises[1]))
                throw new InvalidOperationException("SE target veto no longer skips Original and Post.");
            var cursor = new ILCursor(il);
            cursor.Goto(raises[0], MoveType.After);
            cursor.Emit(OpCodes.Ldloc, il.Body.Variables[local]);
            cursor.EmitDelegate<Action<TribeIssueOrderWithTargetEventArgs>>(AcceptedTarget);
        }

        private static void AcceptedTarget(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!IsAvailable || args.Phase != EventHookPhase.Pre || args.SkipOriginalFunction) return;
            try { reconcileQueue?.Invoke(args); }
            catch (Exception error) { Report("queue", error); }
            // Preserve the former logical order, but run after ALL mutable Pre handlers.
            try { cancelClimb?.Invoke(args); }
            catch (Exception error) { Report("climb", error); }
            try { cancelMovement?.Invoke(args); }
            catch (Exception error) { Report("movement", error); }
        }

        private static long Move(NativePointer<GameTribeManager> manager, int tribe, int x, int y, short patrol, int newOrder, TribeMoveType mode)
        {
            var owner = assassin;
            int epoch = 0;
            object parent = owner?.EnterCommandDispatch(out epoch);
            depth++;
            try { return moveOriginal(manager, tribe, x, y, patrol, newOrder, mode); }
            finally { depth--; owner?.LeaveCommandDispatch(parent, epoch); }
        }

        private static long Target(NativePointer<GameTribeManager> manager, int tribe, TribeAICommand command, int first, int second, int extra)
        {
            var owner = assassin;
            int epoch = 0;
            object parent = owner?.EnterCommandDispatch(out epoch);
            depth++;
            try { return targetOriginal(manager, tribe, command, first, second, extra); }
            finally { depth--; owner?.LeaveCommandDispatch(parent, epoch); }
        }

        private static void Report(string feature, Exception error)
        {
            try { Shared.DebugLogHelper.LogErrorLimited(log, "Pathfinding/accepted-target/" + feature,
                "Final target preparation failed; the native dispatch remains unchanged: " + error); }
            catch { }
        }
    }
}
