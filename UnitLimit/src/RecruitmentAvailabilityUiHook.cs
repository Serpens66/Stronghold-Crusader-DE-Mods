using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace UnitLimit
{
    internal sealed class RecruitmentAvailabilityUiHook
    {
        private readonly ManualLogSource log;
        private readonly Action refreshAvailability;
        private readonly Hook hook;
        private readonly FatControlerNoesisGuiUpdateDelegate trampoline;

        private delegate void FatControlerNoesisGuiUpdateDelegate(FatControler self);

        public RecruitmentAvailabilityUiHook(ManualLogSource log, Action refreshAvailability)
        {
            this.log = log;
            this.refreshAvailability = refreshAvailability;

            MethodInfo updateMethod = typeof(FatControler).GetMethod(
                nameof(FatControler.NoesisGUIUpdateChecksInGame),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (updateMethod == null)
                throw new MissingMethodException(typeof(FatControler).FullName, nameof(FatControler.NoesisGUIUpdateChecksInGame));

            Hook candidate = null;
            try
            {
                candidate = new Hook(updateMethod, (FatControlerNoesisGuiUpdateDelegate)NoesisGuiUpdateChecksInGameHook);
                trampoline = candidate.GenerateTrampoline<FatControlerNoesisGuiUpdateDelegate>();
                hook = candidate;
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }
            Shared.DebugLogHelper.LogDebug(log, "UnitLimit recruitment availability UI hook installed.");
        }

        private void NoesisGuiUpdateChecksInGameHook(FatControler self)
        {
            trampoline(self);

            try
            {
                refreshAvailability();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitLimit recruitment availability UI hook failed:", ex.Message);
            }
        }
    }
}
