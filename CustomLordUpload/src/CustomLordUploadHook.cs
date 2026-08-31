using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using System;
using System.Reflection;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadHook
    {
        private delegate void UploadWorkshopMapDelegate(
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction);

        private readonly Hook hook;
        private readonly UploadWorkshopMapDelegate trampoline;
        private readonly CustomLordUploadWorkflow workflow;
        private readonly CustomLordUploadOptionsController options;

        internal CustomLordUploadHook(ManualLogSource log, CustomLordUploadOptionsController options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            // COMPATIBILITY: Recheck the exact Vanilla upload signature after game-DLL updates.
            MethodInfo? method = typeof(Platform_Workshop).GetMethod(
                nameof(Platform_Workshop.UploadWorkshopMap),
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(string[]),
                    typeof(bool), typeof(string), typeof(Action), typeof(Action)
                },
                null);
            if (method == null)
                throw new MissingMethodException(typeof(Platform_Workshop).FullName, nameof(Platform_Workshop.UploadWorkshopMap));

            // COMPATIBILITY: GameAIManagerAPI must still live in the Script Extender assembly being inspected.
            CustomLordRuntimeRules rules = CustomLordRuntimeRules.Discover(typeof(GameAIManagerAPI).Assembly);
            Shared.DebugLogHelper.LogInfo(
                log,
                "Custom Lord preflight rules: Script Extender=" + rules.ExtenderIdentity +
                ", knownProfile=" + rules.IsKnownIdentity +
                ", reflectedLordInfoFields=" + rules.LordInfoFields.Count +
                ", reflectedMessageTypes=" + rules.MessageTypes.Count +
                ", publicValidator=" + (rules.PublicValidator != null) + ".");
            workflow = new CustomLordUploadWorkflow(
                log,
                new CustomLordUploadStager(),
                new CustomLordUploadConfirmation(),
                rules);

            Hook? installed = null;
            try
            {
                installed = new Hook(method, (UploadWorkshopMapDelegate)UploadWorkshopMapHook);
                trampoline = installed.GenerateTrampoline<UploadWorkshopMapDelegate>();
                hook = installed;
            }
            catch
            {
                installed?.Dispose();
                throw;
            }
        }

        private void UploadWorkshopMapHook(
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction)
        {
            workflow.Handle(
                new CustomLordUploadRequest(
                    instance, nameMap, mapTitle, description, tags, publicMap,
                    previewImage, successAction, failAction,
                    options.ConsumeDecision(nameMap, mapTitle)),
                CallOriginal);
        }

        private void CallOriginal(CustomLordUploadRequest request)
        {
            trampoline(
                request.Instance,
                request.NameMap,
                request.MapTitle,
                request.Description,
                request.Tags,
                request.PublicMap,
                request.PreviewImage,
                request.SuccessAction,
                request.FailAction);
        }
    }
}
