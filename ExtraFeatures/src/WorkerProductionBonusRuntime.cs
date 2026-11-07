using APIShared;
using APIShared.Economy;
using APIShared.Modifiers;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExtraFeatures
{
    // EF owns desired settings and its activation policy. The shared owner performs
    // preparation, native publication, exact state, and all-peer admission.
    internal sealed class WorkerProductionBonusRuntime
    {
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly Action<string,string> showLocalDialog;
        private readonly Action<Action> deferPresentation;
        private readonly Func<Action,Action> registerPresentation;
        private readonly Func<Action,bool> tryPostPresentation;
        private Action requestPresentation;
        private int presentationPosted;
        private readonly List<IDisposable> subscriptions=new List<IDisposable>();
        private WorkerProductionModifierRegistration registration;
        private long session;
        private long lastRejectedLaunch;
        private readonly object faultSync=new object();
        private long lastLatchedFaultRevision=long.MinValue;
        private WorkerProductionStatus pendingFault;
        private bool registered,detailsRequested;
        internal WorkerProductionBonusRuntime(ManualLogSource log,ExtraFeaturesViewModel settings,Action<string,string> showLocalDialog=null,
            Action<Action> deferPresentation=null,Func<Action,Action> registerPresentation=null,Func<Action,bool> tryPostPresentation=null)
        {
            this.log=log;this.settings=settings??throw new ArgumentNullException(nameof(settings));
            this.showLocalDialog=showLocalDialog??ShowLocalDialog;this.deferPresentation=deferPresentation;
            this.registerPresentation=registerPresentation??RegisterPresentation;
            this.tryPostPresentation=tryPostPresentation??TryPostPresentation;
        }
        private Action RegisterPresentation(Action callback)
        {
#if API_SHARED_PRESET_TESTS
            return null;
#else
            if(ApiShared.ForMod(ExtraFeaturesPlugin.PluginGuid).Ui.TryRegisterBeforeRender("production-status",callback,out var work,out string reason))
                return work.Request;
            Shared.DebugLogHelper.LogErrorLimited(log,"ExtraFeatures.WorkerProductionBonus.UI",reason);
            return null;
#endif
        }
        private static bool TryPostPresentation(Action callback)
        {
#if API_SHARED_PRESET_TESTS
            return false;
#else
            return ApiShared.ForMod(ExtraFeaturesPlugin.PluginGuid).Ui.TryPost(callback);
#endif
        }
        private static void ShowLocalDialog(string title,string details)
        {
#if !API_SHARED_PRESET_TESTS
            HUD_ConfirmationPopup.ShowConfirmationOKMessage(title,()=>{},details);
#endif
        }
        internal void Register()
        {
            if(registered)return;registered=true;
            if(!WorkerProductionModifiers.TryRegister(ExtraFeaturesPlugin.PluginGuid,"fixed-production-bonus",out registration,out string reason))
                Shared.DebugLogHelper.LogErrorLimited(log,"ExtraFeatures.WorkerProductionBonus",reason);
            requestPresentation=registerPresentation(RefreshPresentation);
            subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(Initializing));
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(Started));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(Ended));
            settings.SettingChanged+=SettingChanged;
            settings.ProductionBonusDetailsRequested+=()=>{detailsRequested=true;RequestPresentation();};
            WorkerProductionModifiers.TryRegisterObserver(ExtraFeaturesPlugin.PluginGuid,"production-status",StatusChanged,out reason);
            PublishDesired();
        }
        private void PublishDesired()
        {
            registration?.SetProfile(!settings.EnableMod || settings.FixedProductionBonusPercent==50 ? null :
                new WorkerProductionProfile(fixedBonusPercent:ValueModifierContribution.FixedValue(settings.FixedProductionBonusPercent),
                    activation:Shared.SerpsModProfiles.GetProfile(ExtraFeaturesPlugin.PluginGuid,ExtraFeaturesPlugin.PluginName)));
        }
        private void SettingChanged(string name)
        {
            if(name==nameof(settings.EnableMod) || name==nameof(settings.FixedProductionBonusPercent))PublishDesired();
        }
        private void Initializing(MissionLifecycleNotification note)
        {session=note.Context.SessionId;settings.SetFixedProductionBonusMissionLocked(true);}
        private void Started(MissionLifecycleNotification note)
        {session=note.Context.SessionId;settings.SetFixedProductionBonusMissionLocked(true);}
        private void Ended(MissionLifecycleNotification note)
        {if(session==note.Context.SessionId){session=0;settings.SetFixedProductionBonusMissionLocked(false);}}
        private void StatusChanged(WorkerProductionStatus status)
        {
            // World retirement can clear the live fault before Unity pumps presentation.
            // Retain an immutable fault snapshot now; never invoke UI on this caller thread.
            if(status.Issues.Any(value=>value.Code==WorkerProductionIssueCode.RuntimeFault))
            {
                lock(faultSync)
                {
                    if(status.Revision>lastLatchedFaultRevision)
                    {lastLatchedFaultRevision=status.Revision;pendingFault=status;}
                }
            }
            RequestPresentation();
        }
        private void RequestPresentation()
        {
            if(deferPresentation!=null){deferPresentation(RefreshPresentation);return;}
            if(requestPresentation!=null){requestPresentation();return;}
            // Frame-service initialization is independent of the existing Unity context.
            // Keep failures visible through its public deferred post, coalesced without
            // capturing an obsolete session/status. The drain reads canonical state.
            if(Interlocked.CompareExchange(ref presentationPosted,1,0)!=0)return;
            if(!tryPostPresentation(()=>{Interlocked.Exchange(ref presentationPosted,0);RefreshPresentation();}))
            {
                Interlocked.Exchange(ref presentationPosted,0);
                Shared.DebugLogHelper.LogErrorLimited(log,"ExtraFeatures.WorkerProductionBonus.UI.Post","Unity production presentation is unavailable.");
            }
        }
        private static string Text(string code)
        {
            string key="ExtraFeatures.ProductionBonus.Issue."+code;
            string translated=SerpLocalization.Get(key);
            return translated==key?SerpLocalization.Get("ExtraFeatures.ProductionBonus.Issue.Unknown"):translated;
        }
        private void RefreshPresentation()
        {
            // Callbacks can originate on different threads. Read the registry's current
            // immutable state here, so a late older notification cannot hide a fault.
            var state=WorkerProductionModifiers.Status;if(state==null)return;
            bool rejectedNow=state.RejectedLocalLaunchSequence>lastRejectedLaunch;
            if(rejectedNow)
            {lastRejectedLaunch=state.RejectedLocalLaunchSequence;detailsRequested=true;}
            WorkerProductionStatus fault;
            lock(faultSync){fault=pendingFault;pendingFault=null;}
            if(fault!=null)detailsRequested=true;
            string issue=string.Join(" ",state.Issues.Select(value=>Text(value.Code.ToString())).Distinct());
            string readiness=state.PeerReadinessCode;
            string warning=string.IsNullOrEmpty(readiness)?"":Text(readiness);
            if(registration==null)issue=Text("Unavailable")+(issue.Length==0?"":" "+issue);
            settings.UpdateProductionBonusPresentation(state.ResolvedFixedBonusPercent,issue,warning,state.CanStart && registration!=null);
            if(!detailsRequested)return;detailsRequested=false;
            var dialogState=fault??state;
            string details=string.Join("\n\n",dialogState.Issues.Select(value=>Text(value.Code.ToString())+"\n"+value.Diagnostic));
            if(rejectedNow && state.RejectedLocalLaunchCode.Length!=0)details+=(details.Length==0?"":"\n\n")+Text(state.RejectedLocalLaunchCode);
            if(warning.Length!=0)details+=(details.Length==0?"":"\n\n")+warning+"\n"+state.PeerReadinessDiagnostic;
            if(details.Length==0)details=SerpLocalization.Get("ExtraFeatures.ProductionBonus.NoIssues");
            showLocalDialog(SerpLocalization.Get("ExtraFeatures.ProductionBonus.DetailsTitle"),details);
        }
    }
}
