using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace UnitCosts
{
    internal sealed class MakeTroopGameActionHook : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Func<int, eChimps, int, bool> shouldBlockMakeTroop;
        private readonly Hook hook;
        private readonly EngineInterfaceGameActionDelegate trampoline;
        private bool disposed;

        private delegate int EngineInterfaceGameActionDelegate(Enums.GameActionCommand command, int structureID, int state, int value2);

        public MakeTroopGameActionHook(ManualLogSource log, Func<int, eChimps, int, bool> shouldBlockMakeTroop)
        {
            this.log = log;
            this.shouldBlockMakeTroop = shouldBlockMakeTroop;

            MethodInfo gameActionMethod = typeof(EngineInterface).GetMethod(
                nameof(EngineInterface.GameAction),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                null);

            if (gameActionMethod == null)
                throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.GameAction));

            hook = new Hook(gameActionMethod, (EngineInterfaceGameActionDelegate)EngineInterfaceGameActionHook);
            trampoline = hook.GenerateTrampoline<EngineInterfaceGameActionDelegate>();
            log.LogDebug("UnitCosts MakeTroop GameAction hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            log.LogDebug("UnitCosts MakeTroop GameAction hook disposed.");
        }

        private int EngineInterfaceGameActionHook(Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            if (command != Enums.GameActionCommand.MakeTroop)
                return trampoline(command, structureID, state, value2);

            try
            {
                int amount = NormalizeMakeTroopAmount(structureID, state, value2);
                bool block = shouldBlockMakeTroop(amount, (eChimps)state, state);
                if (block)
                    return 0;
            }
            catch (Exception ex)
            {
                log.LogDebug("UnitCosts game action hook failed: " + ex.Message);
            }

            return trampoline(command, structureID, state, value2);
        }

        private int NormalizeMakeTroopAmount(int structureID, int state, int value2)
        {
            // For MakeTroop the generic structureID GameAction parameter is the requested amount.
            // Vanilla passes 1, 5 with Shift, or 1000 with Ctrl.
            if (structureID == 1 || structureID == 5 || structureID == 1000)
                return structureID;

            log.LogWarning("UnitCosts MakeTroop received unexpected amount parameter: " +
                "structureID=" + structureID +
                " state=" + state +
                " value2=" + value2 +
                "; falling back to amount=1.");
            return 1;
        }
    }
}
