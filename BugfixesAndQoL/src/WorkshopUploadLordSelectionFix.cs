// Feature: Keep the selected Extended Lord after a successful Workshop upload.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class WorkshopUploadLordSelectionFix
    {
        private delegate void EditorSetupButtonDelegate(FRONT_EditorSetup self, string command);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly WorkshopUploadLordSelectionPolicy policy =
            new WorkshopUploadLordSelectionPolicy();
        private readonly Hook buttonHook;
        private readonly EditorSetupButtonDelegate buttonOriginal;
        private readonly FieldInfo uploadModeField;
        private readonly FieldInfo selectedLordTypeField;
        private readonly MethodInfo updateUploadAivListMethod;
        private readonly MethodInfo updateUploadLordListMethod;
        private bool firstRestoreLogged;

        internal WorkshopUploadLordSelectionFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            MethodInfo buttonMethod = typeof(FRONT_EditorSetup).GetMethod(
                "ButtonClicked",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            if (buttonMethod == null)
                throw new MissingMethodException(typeof(FRONT_EditorSetup).FullName, "ButtonClicked(string)");
            if (buttonMethod.ReturnType != typeof(void))
                throw new InvalidOperationException("Vanilla FRONT_EditorSetup.ButtonClicked(string) no longer returns void.");

            uploadModeField = FindRequiredField("uploadMode");
            selectedLordTypeField = FindRequiredField("SelectedLordType");
            updateUploadAivListMethod = FindRequiredMethod("UpdateUploadAIVList");
            updateUploadLordListMethod = FindRequiredMethod("UpdateUploadLordList");
            ValidateReflectionContracts();

            Hook candidate = null;
            try
            {
                candidate = new Hook(buttonMethod, (EditorSetupButtonDelegate)ButtonClickedHook);
                buttonOriginal = candidate.GenerateTrampoline<EditorSetupButtonDelegate>();
                buttonHook = candidate;
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Workshop upload Lord-selection hook installed.");
        }

        private void ButtonClickedHook(FRONT_EditorSetup self, string command)
        {
            if (!settings.EnableMod || !settings.EnableWorkshopUploadLordSelectionFix)
            {
                policy.CancelPending();
                buttonOriginal(self, command);
                return;
            }

            bool isDoUpload = string.Equals(command, "DoUpload", StringComparison.Ordinal);
            WorkshopUploadLordRefresh currentMode = GetCurrentMode(self);
            WorkshopUploadLordRefresh refresh = policy.HandleCommand(
                true,
                command,
                currentMode,
                (int)selectedLordTypeField.GetValue(self),
                out int restoredLordType);

            if (refresh == WorkshopUploadLordRefresh.None)
            {
                try
                {
                    buttonOriginal(self, command);
                }
                catch
                {
                    if (isDoUpload)
                        policy.CancelPending();
                    throw;
                }

                if (isDoUpload && FRONT_EditorSetup.canCloseWorkshop)
                    policy.CancelPending();
                return;
            }

            try
            {
                selectedLordTypeField.SetValue(self, restoredLordType);
                if (refresh == WorkshopUploadLordRefresh.Aiv)
                    updateUploadAivListMethod.Invoke(self, null);
                else
                    updateUploadLordListMethod.Invoke(self, null);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not restore Workshop upload Lord slot {restoredLordType}; delegating to Vanilla: {exception}");
                buttonOriginal(self, command);
                return;
            }

            if (!firstRestoreLogged)
            {
                firstRestoreLogged = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL preserved Workshop upload Lord slot {restoredLordType} after a successful {refresh} upload.");
            }
        }

        private WorkshopUploadLordRefresh GetCurrentMode(FRONT_EditorSetup self)
        {
            string mode = uploadModeField.GetValue(self)?.ToString();
            if (string.Equals(mode, "AIV", StringComparison.Ordinal))
                return WorkshopUploadLordRefresh.Aiv;
            if (string.Equals(mode, "LordConfig", StringComparison.Ordinal))
                return WorkshopUploadLordRefresh.LordConfig;
            return WorkshopUploadLordRefresh.None;
        }

        private void ValidateReflectionContracts()
        {
            if (!uploadModeField.FieldType.IsEnum ||
                !Enum.IsDefined(uploadModeField.FieldType, "AIV") ||
                !Enum.IsDefined(uploadModeField.FieldType, "LordConfig"))
            {
                throw new InvalidOperationException(
                    "Vanilla FRONT_EditorSetup.uploadMode no longer exposes the expected AIV and LordConfig enum values.");
            }

            if (selectedLordTypeField.FieldType != typeof(int))
            {
                throw new InvalidOperationException(
                    "Vanilla FRONT_EditorSetup.SelectedLordType is no longer an Int32 field.");
            }

            if (updateUploadAivListMethod.ReturnType != typeof(void) ||
                updateUploadLordListMethod.ReturnType != typeof(void))
            {
                throw new InvalidOperationException(
                    "Vanilla Workshop list refresh methods no longer return void.");
            }
        }

        private static FieldInfo FindRequiredField(string name)
        {
            FieldInfo field = typeof(FRONT_EditorSetup).GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(typeof(FRONT_EditorSetup).FullName, name);
            return field;
        }

        private static MethodInfo FindRequiredMethod(string name)
        {
            MethodInfo method = typeof(FRONT_EditorSetup).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
                throw new MissingMethodException(typeof(FRONT_EditorSetup).FullName, name + "()");
            return method;
        }
    }
}
