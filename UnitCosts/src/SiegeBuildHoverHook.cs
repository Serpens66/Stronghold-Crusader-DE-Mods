using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace UnitCosts
{
    internal sealed class SiegeBuildHoverHook : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Action<object> onEnter;
        private readonly Action onLeave;
        private readonly Hook enterHook;
        private readonly Hook leaveHook;
        private readonly ButtonTroopPanelHoverDelegate enterTrampoline;
        private readonly ButtonTroopPanelHoverDelegate leaveTrampoline;
        private bool disposed;

        private delegate void ButtonTroopPanelHoverDelegate(MainViewModel self, object parameter);

        public SiegeBuildHoverHook(ManualLogSource log, Action<object> onEnter, Action onLeave)
        {
            this.log = log;
            this.onEnter = onEnter;
            this.onLeave = onLeave;

            enterHook = new Hook(FindHoverMethod("ButtonTroopPanelMouseEnter"), (ButtonTroopPanelHoverDelegate)ButtonTroopPanelMouseEnterHook);
            enterTrampoline = enterHook.GenerateTrampoline<ButtonTroopPanelHoverDelegate>();

            leaveHook = new Hook(FindHoverMethod("ButtonTroopPanelMouseLeave"), (ButtonTroopPanelHoverDelegate)ButtonTroopPanelMouseLeaveHook);
            leaveTrampoline = leaveHook.GenerateTrampoline<ButtonTroopPanelHoverDelegate>();

            log.LogInfo("UnitCosts siege build hover hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            enterHook?.Undo();
            enterHook?.Dispose();
            leaveHook?.Undo();
            leaveHook?.Dispose();
            log.LogInfo("UnitCosts siege build hover hooks disposed.");
        }

        private static MethodInfo FindHoverMethod(string methodName)
        {
            MethodInfo method = typeof(MainViewModel).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, methodName);

            return method;
        }

        private void ButtonTroopPanelMouseEnterHook(MainViewModel self, object parameter)
        {
            enterTrampoline(self, parameter);

            try
            {
                onEnter(parameter);
            }
            catch (Exception ex)
            {
                log.LogInfo("UnitCosts siege build enter hook failed: " + ex.Message);
            }
        }

        private void ButtonTroopPanelMouseLeaveHook(MainViewModel self, object parameter)
        {
            leaveTrampoline(self, parameter);

            try
            {
                onLeave();
            }
            catch (Exception ex)
            {
                log.LogInfo("UnitCosts siege build leave hook failed: " + ex.Message);
            }
        }
    }
}
