using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using System;
using System.Reflection;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadOptionsController
    {
        private delegate void EditorSetupButtonDelegate(FRONT_EditorSetup self, string command);

        private readonly ManualLogSource log;
        private readonly CustomLordUploadOptionsViewModel viewModel = new CustomLordUploadOptionsViewModel();
        private Hook? editorHook;
        private EditorSetupButtonDelegate? editorOriginal;
        private readonly object decisionLock = new object();
        private PendingDecision? pendingDecision;

        internal CustomLordUploadOptionsController(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding("CustomLordUploadOptionsHost", viewModel);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Custom Lord upload checkbox binding failed; additional files remain enabled: " + exception);
            }

        }

        internal void InitializeEditorHook()
        {
            MethodInfo method = typeof(FRONT_EditorSetup).GetMethod(
                "ButtonClicked",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null) ?? throw new MissingMethodException(typeof(FRONT_EditorSetup).FullName, "ButtonClicked(string)");
            editorHook = new Hook(method, (EditorSetupButtonDelegate)EditorSetupButtonHook);
            editorOriginal = editorHook.GenerateTrampoline<EditorSetupButtonDelegate>();
        }

        internal bool ConsumeDecision(string uploadRoot, string itemName)
        {
            lock (decisionLock)
            {
                if (pendingDecision == null || !pendingDecision.Matches(uploadRoot, itemName))
                    return true;
                bool include = pendingDecision.IncludeAdditionalFiles;
                pendingDecision = null;
                return include;
            }
        }

        private void EditorSetupButtonHook(FRONT_EditorSetup self, string command)
        {
            FileRow? selectedRow = (self.FindName("UploadList") as ListView)?.SelectedItem as FileRow;
            bool isCustomLord = selectedRow?.lord != null;

            if (string.Equals(command, "DoUpload", StringComparison.Ordinal) && isCustomLord)
            {
                string root = ConfigSettings.GetWorkshopUploadContentPath();
                string itemName = selectedRow!.lord.lordName;
                if (!Shared.WorkshopUploadStaging.TryResetDirectChild(root, itemName, out _, out string error))
                {
                    Shared.DebugLogHelper.LogError(log, "Custom Lord Workshop staging cleanup failed for [" + itemName + "]: " + error);
                    HUD_ConfirmationPopup.ShowOK(
                        SerpLocalization.Get("WorkshopUpload.StagingCleanupFailed"),
                        delegate { });
                    return;
                }

                ArmDecision(root, itemName, viewModel.IncludeAdditionalFiles);
                try
                {
                    editorOriginal!(self, command);
                }
                finally
                {
                    ClearDecision(root, itemName);
                }
                return;
            }

            editorOriginal!(self, command);

            if (string.Equals(command, "Upload", StringComparison.Ordinal))
            {
                if (isCustomLord)
                    viewModel.Open();
                else
                    viewModel.Close();
            }
            else if (string.Equals(command, "CloseDoUpload", StringComparison.Ordinal) ||
                     string.Equals(command, "CloseUpload", StringComparison.Ordinal))
            {
                viewModel.Close();
            }
        }

        private void ArmDecision(string root, string itemName, bool includeAdditionalFiles)
        {
            lock (decisionLock)
                pendingDecision = new PendingDecision(root, itemName, includeAdditionalFiles);
        }

        private void ClearDecision(string root, string itemName)
        {
            lock (decisionLock)
            {
                if (pendingDecision?.Matches(root, itemName) == true)
                    pendingDecision = null;
            }
        }

        private sealed class PendingDecision
        {
            private readonly string root;
            private readonly string itemName;

            internal PendingDecision(string root, string itemName, bool includeAdditionalFiles)
            {
                this.root = Normalize(root);
                this.itemName = itemName ?? string.Empty;
                IncludeAdditionalFiles = includeAdditionalFiles;
            }

            internal bool IncludeAdditionalFiles { get; }

            internal bool Matches(string candidateRoot, string candidateItemName) =>
                string.Equals(root, Normalize(candidateRoot), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(itemName, candidateItemName, StringComparison.OrdinalIgnoreCase);

            private static string Normalize(string value)
            {
                try { return System.IO.Path.GetFullPath(value ?? string.Empty); }
                catch { return value ?? string.Empty; }
            }
        }
    }
}
