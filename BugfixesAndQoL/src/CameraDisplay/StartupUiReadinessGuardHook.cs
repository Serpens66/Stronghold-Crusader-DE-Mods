// Feature: Keep Vanilla's UI and radar update tail dormant until its roots exist.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class StartupUiReadinessGuardHook
    {
        private static readonly StartupUiReadinessGuardState startupState =
            new StartupUiReadinessGuardState();

        private readonly ILHook hook;

        internal StartupUiReadinessGuardHook(ManualLogSource log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            MethodInfo target = typeof(FatControler).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (target == null || target.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(FatControler).FullName, "Update");

            ValidateUiMemberContracts();
            StartupUiReadinessGuardIlContract.ValidateVulnerableRadarScrollMap(
                FindRadarScrollMapMethod());

            ILHook candidate = null;
            try
            {
                candidate = new ILHook(
                    target,
                    PatchUiUpdateReadinessBranch,
                    new ILHookConfig
                    {
                        ManualApply = true,
                        ID = "BugfixesAndQoL.StartupUiReadinessGuard"
                    });
                candidate.Apply();
                hook = candidate;
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Vanilla startup UI-readiness guard installed for the process lifetime.");
        }

        private static void PatchUiUpdateReadinessBranch(ILContext context)
        {
            StartupUiReadinessGuardIlContract.ApplyPatch(
                context,
                IsVanillaUiUpdateReady);
        }

        private static bool IsVanillaUiUpdateReady(bool viewModelLoaded)
        {
            return startupState.Evaluate(viewModelLoaded, AreUiAndControlsReady);
        }

        private static bool AreUiAndControlsReady()
        {
            MainViewModel main = MainViewModel.Instance;
            return main?.GlobalUIRoot != null &&
                main.HUDmain != null &&
                main.FrontEndMenu != null &&
                MainControls.instance != null;
        }

        private static void ValidateUiMemberContracts()
        {
            RequireInstanceField(nameof(MainViewModel.GlobalUIRoot), typeof(MasterController));
            RequireInstanceField(nameof(MainViewModel.HUDmain), typeof(HUD_Main));
            RequireInstanceField(nameof(MainViewModel.FrontEndMenu), typeof(FrontendMenus));
            RequireStaticField(typeof(MainControls), nameof(MainControls.instance), typeof(MainControls));

            FindRadarScrollMapMethod();
        }

        private static MethodInfo FindRadarScrollMapMethod()
        {
            MethodInfo method = typeof(FatControler).GetMethod(
                StartupUiReadinessGuardIlContract.RadarScrollMapMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(
                    typeof(FatControler).FullName,
                    StartupUiReadinessGuardIlContract.RadarScrollMapMethodName);

            return method;
        }

        private static void RequireInstanceField(string fieldName, Type expectedType)
        {
            FieldInfo field = typeof(MainViewModel).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.IsStatic || field.FieldType != expectedType)
                throw new MissingFieldException(typeof(MainViewModel).FullName, fieldName);
        }

        private static void RequireStaticField(Type declaringType, string fieldName, Type expectedType)
        {
            FieldInfo field = declaringType.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || !field.IsStatic || field.FieldType != expectedType)
                throw new MissingFieldException(declaringType.FullName, fieldName);
        }
    }
}
