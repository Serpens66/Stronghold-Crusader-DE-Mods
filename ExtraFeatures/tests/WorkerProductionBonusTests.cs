using APIShared;
using APIShared.GameModes;
using APIShared.ModSettings;
using APIShared.Modifiers;
using ExtraFeatures;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Only the external shared-service boundary is doubled. Production EF settings,
// profile adapter, lifecycle relay and preset engine execute unchanged.
namespace APIShared.Economy
{
    public sealed class WorkerProductionProfile
    {
        public WorkerProductionProfile(ValueModifierContribution fixedBonusPercent=default,GameplayModActivationProfile? activation=null)
        {FixedBonusPercent=fixedBonusPercent;Activation=activation;}
        public ValueModifierContribution FixedBonusPercent {get;}
        public GameplayModActivationProfile? Activation {get;}
    }
    public enum WorkerProductionIssueCode {NativeUnavailable,RuntimeFault}
    public sealed class WorkerProductionIssue
    {
        public WorkerProductionIssueCode Code {get;set;}
        public string Diagnostic {get;set;}="fixture diagnostic";
    }
    public sealed class WorkerProductionStatus
    {
        public long Revision {get;set;}
        public bool IsAvailable {get;set;}=true;
        public bool CanStart=>Issues.Count==0;
        public int ResolvedFixedBonusPercent {get;set;}=50;
        public IReadOnlyList<WorkerProductionIssue> Issues {get;set;}=Array.Empty<WorkerProductionIssue>();
        public string PeerReadinessCode {get;set;}="";
        public string PeerReadinessDiagnostic {get;set;}="";
        public long RejectedLocalLaunchSequence {get;set;}
        public string RejectedLocalLaunchCode {get;set;}="";
    }
    public sealed class WorkerProductionModifierRegistration
    {
        public WorkerProductionProfile Profile {get;private set;}
        public void SetProfile(WorkerProductionProfile profile){Profile=profile;}
    }
    public static class WorkerProductionModifiers
    {
        private static readonly List<Action<WorkerProductionStatus>> observers=new List<Action<WorkerProductionStatus>>();
        public static int Registrations;
        public static WorkerProductionModifierRegistration Last;
        public static WorkerProductionStatus Status {get;private set;}=new WorkerProductionStatus();
        public static bool TryRegister(string owner,string id,out WorkerProductionModifierRegistration handle,out string reason,int order=0)
        {Registrations++;handle=Last=new WorkerProductionModifierRegistration();reason="";return true;}
        public static bool TryRegisterObserver(string owner,string id,Action<WorkerProductionStatus> observer,out string reason,int order=0,bool replayCurrent=true)
        {observers.Add(observer);reason="";if(replayCurrent)observer(Status);return true;}
        private static long revision;
        internal static void Publish(WorkerProductionStatus value){if(value.Revision==0)value.Revision=++revision;Status=value;foreach(var observer in observers.ToArray())observer(value);}
    }
}

internal static class WorkerProductionBonusTests
{
    internal static void Run(Action<bool,string> check)
    {
        Settings(check);
        var settings=new ExtraFeaturesViewModel(false);
        settings.PreparePresets(null,Path.Combine(TestWorkDirectory.New("ProductionBonusRuntime"),"ExtraFeatures.dll"),"ProductionBonusRuntime");
        settings.ActivatePresets();settings.FixedProductionBonusPercent=-50;
        int localDialogs=0;
        bool delayPresentation=false;Action pendingPresentation=null;string lastDialog="";
        var runtime=new WorkerProductionBonusRuntime(null,settings,(title,details)=>{localDialogs++;lastDialog=details;},
            action=>{if(delayPresentation)pendingPresentation=action;else action();});
        int before=APIShared.Economy.WorkerProductionModifiers.Registrations;
        runtime.Register();runtime.Register();
        var handle=APIShared.Economy.WorkerProductionModifiers.Last;
        check(APIShared.Economy.WorkerProductionModifiers.Registrations==before+1,"Production profile registers once for process lifetime");
        check(handle.Profile.FixedBonusPercent.Kind==ValueModifierKind.FixedValue && handle.Profile.FixedBonusPercent.Value==-50,
            "EF stages its chosen whole percent before mission admission");
        check(handle.Profile.Activation.HasValue && handle.Profile.Activation.Value.ModGuid==ExtraFeaturesPlugin.PluginGuid,
            "EF supplies its own central mode policy without owning a second gameplay gate");
        settings.FixedProductionBonusPercent=50;
        check(handle.Profile==null,"Neutral 50 contributes nothing and does not hide another owner's factors");
        settings.EnableMod=false;settings.FixedProductionBonusPercent=-100;
        check(handle.Profile==null,"Disabled EF stages no production contribution");
        settings.EnableMod=true;
        check(handle.Profile.FixedBonusPercent.Value==-100,"Reactivation stages the chosen desired profile");
        var context=new MissionContext(19100,MissionStartKind.LoadedSave,default(GameModeSnapshot),null,"fixture",false);
        Shared.MissionEvents.Publish(new MissionLifecycleNotification(context,MissionLifecycleKind.Initialization,
            MissionInitializationPhase.BeforeLoad,MissionEndReason.None,false));
        check(!settings.CanConfigureFixedProductionBonus,"The production editor locks before native loading");
        settings.FixedProductionBonusValueText="25";
        var draft=settings.System_CreateModDefaultSnapshot();draft[nameof(settings.FixedProductionBonusPercent)]=MessagePack.MessagePackSerializer.Serialize(25);
        settings.System_ApplyWorkingSnapshot(draft);
        check(settings.FixedProductionBonusPercent==-100 && handle.Profile.FixedBonusPercent.Value==-100,
            "Text and working snapshots cannot bypass the running-mission lock");
        Shared.MissionEvents.Publish(new MissionLifecycleNotification(context,MissionLifecycleKind.End,
            MissionInitializationPhase.NativeLoaded,MissionEndReason.Unloaded,false));
        check(settings.CanConfigureFixedProductionBonus,"End always unlocks the editor; shared world owner controls native retirement");
        APIShared.Economy.WorkerProductionModifiers.Publish(new APIShared.Economy.WorkerProductionStatus{
            ResolvedFixedBonusPercent=125,PeerReadinessCode="Pending"});
        check(settings.ProductionBonusWarningVisibility==Noesis.Visibility.Visible && settings.ProductionBonusIssueVisibility==Noesis.Visibility.Collapsed,
            "Shared peer readiness appears in an independent warning without inventing an EF report");
        string readyText=settings.EffectiveFixedProductionBonusText;
        APIShared.Economy.WorkerProductionModifiers.Publish(new APIShared.Economy.WorkerProductionStatus{
            IsAvailable=false,Issues=new[]{new APIShared.Economy.WorkerProductionIssue{Code=APIShared.Economy.WorkerProductionIssueCode.NativeUnavailable}}});
        check(settings.ProductionBonusIssueVisibility==Noesis.Visibility.Visible && settings.ProductionBonusWarningVisibility==Noesis.Visibility.Visible &&
            settings.EffectiveFixedProductionBonusText!=readyText,"A blocking shared issue remains visible and never masquerades as an effective fallback percent");
        var rejection=new APIShared.Economy.WorkerProductionStatus{RejectedLocalLaunchSequence=1,RejectedLocalLaunchCode="Unavailable",
            PeerReadinessCode="Unavailable"};
        APIShared.Economy.WorkerProductionModifiers.Publish(rejection);APIShared.Economy.WorkerProductionModifiers.Publish(rejection);
        check(localDialogs==1,"An actual rejected local start opens one local dialog without repeating on status replay");
        var fault=new APIShared.Economy.WorkerProductionStatus{RejectedLocalLaunchSequence=1,
            Issues=new[]{new APIShared.Economy.WorkerProductionIssue{Code=APIShared.Economy.WorkerProductionIssueCode.RuntimeFault}}};
        delayPresentation=true;APIShared.Economy.WorkerProductionModifiers.Publish(fault);
        APIShared.Economy.WorkerProductionModifiers.Publish(new APIShared.Economy.WorkerProductionStatus{RejectedLocalLaunchSequence=1});
        delayPresentation=false;pendingPresentation();
        APIShared.Economy.WorkerProductionModifiers.Publish(fault);
        check(localDialogs==2 && lastDialog.Contains("fixture diagnostic"),
            "A fault snapshot survives world retirement before the UI pump and is shown once despite a replay");
        bool detailRequested=false;settings.ProductionBonusDetailsRequested+=()=>detailRequested=true;
        settings.ProductionBonusDetailsCommand.Execute(null);
        check(detailRequested && localDialogs==3,"Details use a local presentation request rather than a network setting");
        check(typeof(ExtraFeaturesViewModel).GetProperty("WorkerProductionBonusCompatibilityKey")==null,
            "Obsolete EF-owned per-player production reports are removed");
        var fallbackSettings=new ExtraFeaturesViewModel(false);
        var posted=new Queue<Action>();int fallbackDialogs=0,fallbackRegistrations=0;string fallbackDetails="";
        var fallbackRuntime=new WorkerProductionBonusRuntime(null,fallbackSettings,
            (title,details)=>{fallbackDialogs++;fallbackDetails=details;},
            registerPresentation:callback=>{fallbackRegistrations++;return null;},
            tryPostPresentation:callback=>{posted.Enqueue(callback);return true;});
        fallbackRuntime.Register();
        var deferredFault=new APIShared.Economy.WorkerProductionStatus{RejectedLocalLaunchSequence=1,
            Issues=new[]{new APIShared.Economy.WorkerProductionIssue{Code=APIShared.Economy.WorkerProductionIssueCode.RuntimeFault,
                Diagnostic="deferred after frame service failure"}}};
        APIShared.Economy.WorkerProductionModifiers.Publish(deferredFault);
        APIShared.Economy.WorkerProductionModifiers.Publish(new APIShared.Economy.WorkerProductionStatus{RejectedLocalLaunchSequence=1});
        check(fallbackRegistrations==1 && posted.Count==1 && fallbackDialogs==0,
            "Failed render registration uses one deferred public Unity post and never invokes UI on the observer thread");
        posted.Dequeue()();
        check(fallbackDialogs==1 && fallbackDetails.Contains("deferred after frame service failure"),
            "Fallback posting preserves the fault through world retirement and presents it on the dispatched callback");
        APIShared.Economy.WorkerProductionModifiers.Publish(deferredFault);posted.Dequeue()();
        check(fallbackDialogs==1,"A later fallback drain does not replay an already presented fault");

    }

    private static void Settings(Action<bool, string> check)
    {
        string path = Path.Combine(TestWorkDirectory.New("ProductionBonusPreset"), "ExtraFeatures.dll");
        Func<ExtraFeaturesViewModel> prepare = () =>
        {
            var model = new ExtraFeaturesViewModel(false);
            model.PreparePresets(null, path, "ProductionBonus"); model.ActivatePresets();
            return model;
        };
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        var settings = prepare();
        check(settings.FixedProductionBonusPercent == 50, "Production bonus defaults to Vanilla 50 percent");
        settings.FixedProductionBonusValueText = "-50";
        check(settings.FixedProductionBonusPercent == -50, "Signed whole production percent accepts a penalty");
        foreach (string invalid in new[] { "1.5", "1,5", "1%", "", "-", "2147483648" })
        {
            settings.FixedProductionBonusValueText = invalid;
            check(settings.FixedProductionBonusPercent == -50, "Invalid production percent preserves the value: " + invalid);
        }
        settings.FixedProductionBonusPercent = int.MinValue;
        check(settings.FixedProductionBonusPercent == -100, "Production percent clamps at minus 100");
        settings.FixedProductionBonusPercent = int.MaxValue;
        check(settings.FixedProductionBonusPercent == 1000, "Production percent clamps at plus 1000");
        settings.FixedProductionBonusPercent = -1;
        var restored = prepare();
        check(restored.FixedProductionBonusPercent == -1,
            "Production setting survives restart through shared preset storage");
        restored.System_SavePersonalPreset("production-bonus", "Production bonus", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(restored.FixedProductionBonusPercent), Mode = PublishedPresetValueMode.Fixed } }, false);
        var preset = restored.System_TestPublishedPresets.Single(p => p.Id == "production-bonus");
        restored.System_LoadModDefaults();
        check(restored.FixedProductionBonusPercent == 50, "Shared defaults restore the Vanilla coefficient");
        restored.System_TestLoadPreset(preset.StableId);
        check(restored.FixedProductionBonusPercent == -1, "Named preset restores a negative production percent");
        GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
        restored.FixedProductionBonusPercent = 100; restored.FixedProductionBonusValueText = "200";
        check(restored.FixedProductionBonusPercent == -1 && !restored.CanConfigureFixedProductionBonus,
            "Clients cannot edit the host production setting");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.FixedProductionBonusPercent = -100);
        check(restored.FixedProductionBonusPercent == -100, "Authoritative host production percent is accepted");
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        var own = prepare();
        check(own.FixedProductionBonusPercent == -1, "Received host percent does not overwrite the local preset");
        own.ResetToDefaultCommand.Execute(null);
        check(own.FixedProductionBonusPercent == 50, "ExtraFeatures reset restores the production bonus default");
        own.FixedProductionBonusPercent = -25;
        own.SetFixedProductionBonusMissionLocked(true);
        var saved = own.System_CreateModDefaultSnapshot();
        saved[nameof(own.FixedProductionBonusPercent)] = MessagePack.MessagePackSerializer.Serialize(-75);
        own.System_ApplyWorkingSnapshot(saved);
        check(own.FixedProductionBonusPercent == -25 && !own.CanConfigureFixedProductionBonus,
            "A local working snapshot cannot change a locked production setting");
        own.System_TestLoadPreset(preset.StableId);
        check(own.FixedProductionBonusPercent == -25, "A published preset cannot bypass the production mission lock");
        own.System_LoadModDefaults();
        check(own.FixedProductionBonusPercent == -25, "Loading Mod defaults cannot bypass the production mission lock");
        own.System_EnterMissionPreset(saved, "Production bonus mission restoration", false);
        check(own.FixedProductionBonusPercent == -75 && !own.CanConfigureFixedProductionBonus,
            "A genuine mission-entry snapshot restores the locked setting without unlocking its editor");
        own.System_ExitMissionPreset();
        check(own.FixedProductionBonusPercent == -25 && !own.CanConfigureFixedProductionBonus,
            "A genuine mission-exit snapshot restores the owned setting while the editor remains locked");
        own.SetFixedProductionBonusMissionLocked(false);
    }
}
