// Feature: Keep Vanilla's UI update block dormant until its Noesis roots exist.
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
            int insertionIndex = StartupUiReadinessGuardIlContract.FindUniqueInsertionIndex(context);
            var cursor = new ILCursor(context) { Index = insertionIndex };
            cursor.EmitDelegate<Func<bool, bool>>(IsVanillaUiUpdateReady);
        }

        private static bool IsVanillaUiUpdateReady(bool viewModelLoaded)
        {
            if (startupState.IsComplete)
                return viewModelLoaded;

            if (!viewModelLoaded)
                return false;

            MainViewModel main = MainViewModel.Instance;
            if (main?.HUDmain == null || main.FrontEndMenu == null)
                return false;

            startupState.MarkComplete();
            return true;
        }

        private static void ValidateUiMemberContracts()
        {
            RequireInstanceField(nameof(MainViewModel.HUDmain), typeof(HUD_Main));
            RequireInstanceField(nameof(MainViewModel.FrontEndMenu), typeof(FrontendMenus));

            MethodInfo radarScrollMap = typeof(FatControler).GetMethod(
                StartupUiReadinessGuardIlContract.RadarScrollMapMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (radarScrollMap == null || radarScrollMap.ReturnType != typeof(void))
                throw new MissingMethodException(
                    typeof(FatControler).FullName,
                    StartupUiReadinessGuardIlContract.RadarScrollMapMethodName);
        }

        private static void RequireInstanceField(string fieldName, Type expectedType)
        {
            FieldInfo field = typeof(MainViewModel).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.IsStatic || field.FieldType != expectedType)
                throw new MissingFieldException(typeof(MainViewModel).FullName, fieldName);
        }
    }
}
