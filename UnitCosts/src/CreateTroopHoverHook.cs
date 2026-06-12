using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace UnitCosts
{
    internal sealed class CreateTroopHoverHook : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Action<MainViewModel> onEnter;
        private readonly Action onLeave;
        private readonly Hook enterHook;
        private readonly Hook leaveHook;
        private readonly ButtonCreateTroopHoverDelegate enterTrampoline;
        private readonly ButtonCreateTroopHoverDelegate leaveTrampoline;
        private bool disposed;

        private delegate void ButtonCreateTroopHoverDelegate(MainViewModel self, object parameter);

        public CreateTroopHoverHook(ManualLogSource log, Action<MainViewModel> onEnter, Action onLeave)
        {
            this.log = log;
            this.onEnter = onEnter;
            this.onLeave = onLeave;

            enterHook = new Hook(FindHoverMethod("ButtonEnterCreateTroop"), (ButtonCreateTroopHoverDelegate)ButtonEnterCreateTroopHook);
            enterTrampoline = enterHook.GenerateTrampoline<ButtonCreateTroopHoverDelegate>();

            leaveHook = new Hook(FindHoverMethod("ButtonLeaveCreateTroop"), (ButtonCreateTroopHoverDelegate)ButtonLeaveCreateTroopHook);
            leaveTrampoline = leaveHook.GenerateTrampoline<ButtonCreateTroopHoverDelegate>();

            log.LogDebug("UnitCosts create troop hover hooks installed.");
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
            log.LogDebug("UnitCosts create troop hover hooks disposed.");
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

        private void ButtonEnterCreateTroopHook(MainViewModel self, object parameter)
        {
            enterTrampoline(self, parameter);

            try
            {
                onEnter(self);
            }
            catch (Exception ex)
            {
                log.LogDebug("UnitCosts create troop enter hook failed: " + ex.Message);
            }
        }

        private void ButtonLeaveCreateTroopHook(MainViewModel self, object parameter)
        {
            leaveTrampoline(self, parameter);

            try
            {
                onLeave();
            }
            catch (Exception ex)
            {
                log.LogDebug("UnitCosts create troop leave hook failed: " + ex.Message);
            }
        }
    }
}
