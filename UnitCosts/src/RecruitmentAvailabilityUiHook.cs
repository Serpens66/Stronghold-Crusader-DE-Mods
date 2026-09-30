using BepInEx.Logging;
using CrusaderDE;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace UnitCosts
{
    internal sealed class RecruitmentAvailabilityUiHook
    {
        private readonly ManualLogSource log;
        private readonly Func<bool> isActive;
        private readonly Action refreshAvailability;
        private readonly ILHook materialUiHook;
        private readonly Hook hook;
        private readonly FatControlerNoesisGuiUpdateDelegate trampoline;

        private delegate void FatControlerNoesisGuiUpdateDelegate(FatControler self);

        public RecruitmentAvailabilityUiHook(ManualLogSource log, Func<bool> isActive,
            Func<eChimps, bool> noWeapons, Action refreshAvailability)
        {
            this.log = log;
            this.isActive = isActive;
            this.refreshAvailability = refreshAvailability;

            MethodInfo updateMethod = typeof(FatControler).GetMethod(
                nameof(FatControler.NoesisGUIUpdateChecksInGame),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (updateMethod == null || updateMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(FatControler).FullName, nameof(FatControler.NoesisGUIUpdateChecksInGame));

            RecruitmentWeaponUiIlContract.Validate(updateMethod);

            ILHook pendingMaterialHook = null;
            Hook pendingHook = null;
            try
            {
                pendingMaterialHook = new ILHook(updateMethod,
                    context => RecruitmentWeaponUiIlContract.Apply(context, noWeapons),
                    new ILHookConfig { ManualApply = true, ID = "UnitCosts.NoWeaponsRecruitmentUi" });
                pendingMaterialHook.Apply();
                pendingHook = new Hook(updateMethod, (FatControlerNoesisGuiUpdateDelegate)NoesisGuiUpdateChecksInGameHook);
                FatControlerNoesisGuiUpdateDelegate pendingTrampoline = pendingHook.GenerateTrampoline<FatControlerNoesisGuiUpdateDelegate>();
                materialUiHook = pendingMaterialHook;
                hook = pendingHook;
                trampoline = pendingTrampoline;
            }
            catch
            {
                pendingHook?.Dispose();
                pendingMaterialHook?.Dispose();
                throw;
            }
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts recruitment availability and No Weapons UI hooks installed for the process lifetime.");
        }

        private void NoesisGuiUpdateChecksInGameHook(FatControler self)
        {
            trampoline(self);

            if (!isActive())
                return;

            try
            {
                refreshAvailability();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts recruitment availability UI hook failed:", ex.Message);
            }
        }
    }
}
