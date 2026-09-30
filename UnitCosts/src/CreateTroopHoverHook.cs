using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace UnitCosts
{
    internal sealed class CreateTroopHoverHook
    {
        private readonly ManualLogSource log;
        private readonly Func<bool> isActive;
        private readonly Action<MainViewModel> onEnter;
        private readonly Action onLeave;
        private readonly Hook enterHook;
        private readonly Hook leaveHook;
        private readonly ButtonCreateTroopHoverDelegate enterTrampoline;
        private readonly ButtonCreateTroopHoverDelegate leaveTrampoline;

        private delegate void ButtonCreateTroopHoverDelegate(MainViewModel self, object parameter);

        public CreateTroopHoverHook(ManualLogSource log, Func<bool> isActive, Action<MainViewModel> onEnter, Action onLeave)
        {
            this.log = log;
            this.isActive = isActive;
            this.onEnter = onEnter;
            this.onLeave = onLeave;

            MethodInfo enterMethod = FindHoverMethod("ButtonEnterCreateTroop");
            MethodInfo leaveMethod = FindHoverMethod("ButtonLeaveCreateTroop");
            Hook installedEnterHook = null;
            Hook installedLeaveHook = null;
            try
            {
                installedEnterHook = new Hook(enterMethod, (ButtonCreateTroopHoverDelegate)ButtonEnterCreateTroopHook);
                ButtonCreateTroopHoverDelegate installedEnterTrampoline = installedEnterHook.GenerateTrampoline<ButtonCreateTroopHoverDelegate>();
                installedLeaveHook = new Hook(leaveMethod, (ButtonCreateTroopHoverDelegate)ButtonLeaveCreateTroopHook);
                ButtonCreateTroopHoverDelegate installedLeaveTrampoline = installedLeaveHook.GenerateTrampoline<ButtonCreateTroopHoverDelegate>();

                enterHook = installedEnterHook;
                enterTrampoline = installedEnterTrampoline;
                leaveHook = installedLeaveHook;
                leaveTrampoline = installedLeaveTrampoline;
            }
            catch
            {
                installedLeaveHook?.Dispose();
                installedEnterHook?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "UnitCosts create troop hover hooks installed.");
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

            if (!isActive())
                return;

            try
            {
                onEnter(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts create troop enter hook failed:", ex.Message);
            }
        }

        private void ButtonLeaveCreateTroopHook(MainViewModel self, object parameter)
        {
            leaveTrampoline(self, parameter);

            if (!isActive())
                return;

            try
            {
                onLeave();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts create troop leave hook failed:", ex.Message);
            }
        }
    }
}
