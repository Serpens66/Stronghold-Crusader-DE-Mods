using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace UnitLimit
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
            log.LogInfo("UnitLimit MakeTroop GameAction hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            log.LogInfo("UnitLimit MakeTroop GameAction hook disposed.");
        }

        private int EngineInterfaceGameActionHook(Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            if (command != Enums.GameActionCommand.MakeTroop)
                return trampoline(command, structureID, state, value2);

            try
            {
                bool block = shouldBlockMakeTroop(structureID, (eChimps)state, state);
                if (block)
                    return 0;
            }
            catch (Exception ex)
            {
                log.LogInfo("Unit limit game action hook failed: " + ex.Message);
            }

            return trampoline(command, structureID, state, value2);
        }
    }
}
