using CastlePlanner;
using System.Windows.Input;

internal static class BlueprintRoleBoundaryTests
{
    internal static void Run()
    {
        void Check(bool value,string message){if(!value)throw new Exception("Blueprint role: "+message);}
        var observations=new APIShared.RoleObservationBoundary();
        APIShared.ApiShared.Client=new APIShared.RoleClientBoundary(observations);
        APIShared.PlayerPerspectiveAPI.ActualPlayer=3;
        var controller=new BlueprintRuntimeController();
        controller.StartBoundary(11);
        controller.InitializeRoleBoundary();
        controller.RenderBoundary();
        Check(!controller.IsObserverBoundary,"alive participant starts with manual controls");
        var inner=new ConfirmBoundary();
        var confirm=new BlueprintParticipantCommand(inner,controller.ManualBoundary);
        confirm.Execute(null);
        Check(inner.Executions==1,"live participant can confirm normally");

        // Publisher thread is deliberately not the UI thread. Closed/idle presentation
        // has no continuous demand; the actual production callback must wake it alone.
        int reads=observations.Reads;
        Task.Run(()=>observations.Publish(new APIShared.PlayerDefeatSnapshot(11,3,true,true))).GetAwaiter().GetResult();
        Check(observations.Reads==reads && controller.RequestsBoundary>0,"state callback only invalidates and requests a frame");
        confirm.Execute(null); // Already retained command, before its next UI refresh.
        Check(inner.Executions==1 && !confirm.CanExecute(null),"retained confirm checks fresh positive-participant loss before the next frame");
        Check(observations.LastPlayer==3 && observations.LastSession==11,"loss query uses the actual participant and active session");
        controller.RenderBoundary();
        Check(controller.IsObserverBoundary,"idle frame switches to observer UI");
        controller.SetViewedPlayerBoundary(0);
        APIShared.PlayerPerspectiveAPI.ActualPlayer=-1;
        controller.RenderBoundary();
        Check(controller.IsObserverBoundary,"confirmed actual participant loss survives a cleared participant/view ID without native spectator flag");
        APIShared.PlayerPerspectiveAPI.ActualPlayer=3;
        Check(inner.PreviewStillSelecting,"local role changes do not cancel shared preview state");

        observations.Publish(new APIShared.PlayerDefeatSnapshot(11,3,false,false));
        controller.RenderBoundary();
        Check(controller.IsObserverBoundary,"transient unreadability cannot resurrect manual controls after confirmed loss");
        observations.Publish(new APIShared.PlayerDefeatSnapshot(11,3,true,false));
        controller.RenderBoundary();
        Check(controller.IsObserverBoundary,"confirmed local loss remains latched until mission end");

        controller.StartBoundary(12);
        observations.Publish(new APIShared.PlayerDefeatSnapshot(11,3,true,true));
        controller.RenderBoundary();
        Check(!controller.IsObserverBoundary,"old queued observations cannot cross mission replacement");
        observations.Publish(new APIShared.PlayerDefeatSnapshot(12,4,true,true));
        controller.RenderBoundary();
        Check(!controller.IsObserverBoundary,"a viewed/foreign lord's loss does not eliminate the participant");
        observations.Publish(new APIShared.PlayerDefeatSnapshot(12,3,false,true));
        controller.RenderBoundary();
        Check(!controller.IsObserverBoundary,"unreadable initial observation grants no elimination role");
        observations.Publish(new APIShared.PlayerDefeatSnapshot(12,3,true,true));
        controller.RenderBoundary();
        Check(controller.IsObserverBoundary,"loaded official-loss baseline is sufficient without a transition callback");
        controller.EndBoundary();
        observations.Publish(new APIShared.PlayerDefeatSnapshot(12,3,true,true));
        controller.RenderBoundary();
        Check(!controller.IsObserverBoundary,"end clears the positive-participant latch");

        controller.StartBoundary(13);
        APIShared.PlayerPerspectiveAPI.ActualPlayer=-1;
        controller.SetOriginalSpectatorBoundary(true);
        controller.RenderBoundary();
        Check(controller.IsObserverBoundary,"original spectator identity remains independent of selected/input-controlled AI");
        controller.SetEditorBoundary(true);
        controller.RenderBoundary();
        Check(!controller.IsObserverBoundary,"editor retains its own role even with spectator flags");

        APIShared.PlayerPerspectiveAPI.ActualPlayer=3;
        observations.Publish(new APIShared.PlayerDefeatSnapshot(14,3,true,true));
        var lateController=new BlueprintRuntimeController();
        lateController.StartBoundary(14);
        lateController.InitializeRoleBoundary();
        Check(lateController.RequestsBoundary>0,"late registration replays a loaded loss and wakes idle presentation");
        lateController.RenderBoundary();
        Check(lateController.IsObserverBoundary,"loaded defeated participant needs no repeated transition event");
    }

    private sealed class ConfirmBoundary : ICommand
    {
        internal int Executions;
        internal bool PreviewStillSelecting=true;
        public bool CanExecute(object parameter)=>true;
        public void Execute(object parameter)=>Executions++;
        public event EventHandler CanExecuteChanged { add{} remove{} }
    }
}

// These doubles stop at the external API/UI scheduling boundary; the observer
// registration, callback, snapshot lookup and command guard are production source.
namespace CastlePlanner
{
    internal static class CastlePlannerPlugin { internal const string PluginGuid="CastlePlanner_Serp"; }
    internal sealed partial class BlueprintRuntimeController
    {
        private bool mapActive,editorSessionActive,spectatorFlag;
        private int viewedPlayer=3;
        private object log=null;
        internal int RequestsBoundary;
        private void RequestFrame()=>Interlocked.Increment(ref RequestsBoundary);
        private bool HasSpectatorIdentity=>BlueprintSpectatorPolicy.IsSpectator(mapActive,editorSessionActive,true,
            spectatorFlag,viewedPlayer,observerRole.HasConfirmedLoss);
        internal bool IsObserverBoundary {get;private set;}
        internal void InitializeRoleBoundary()=>InitializeBlueprintRole();
        internal void StartBoundary(long session){mapActive=true;observerRole.Begin(session);}
        internal void EndBoundary(){mapActive=false;observerRole.End();}
        internal void SetOriginalSpectatorBoundary(bool value){spectatorFlag=value;viewedPlayer=value?-1:3;observerRole.Invalidate();}
        internal void SetViewedPlayerBoundary(int player){viewedPlayer=player;observerRole.Invalidate();}
        internal void SetEditorBoundary(bool value){editorSessionActive=value;observerRole.Invalidate();}
        internal bool ManualBoundary()=>CanUseManualBlueprintControls();
        internal void RenderBoundary(){if(observerRole.TakeInvalidation())RefreshBlueprintRoleEvidence();IsObserverBoundary=HasSpectatorIdentity;Interlocked.Exchange(ref RequestsBoundary,0);}
    }
}
namespace Shared
{
    internal static class DebugLogHelper { internal static void LogErrorLimited(object log,string key,string message)=>throw new Exception(message); }
}
namespace APIShared
{
    internal sealed class PlayerDefeatSnapshot
    {
        internal PlayerDefeatSnapshot(long session,int player,bool readable,bool eliminated){SessionId=session;PlayerId=player;Readable=readable;IsEliminated=eliminated;}
        internal long SessionId;internal int PlayerId;internal bool Readable,IsEliminated;
    }
    internal sealed class NativeCapabilityDiagnostic { internal string Reason=>"test boundary"; }
    internal interface IPlayerDefeatStateCapability
    {
        bool TryRegisterStateObserver(string id,Action<PlayerDefeatSnapshot> callback,out NativeCapabilityDiagnostic diagnostic);
        bool TryGetObservedState(long session,int player,out PlayerDefeatSnapshot snapshot);
    }
    internal sealed class RoleObservationBoundary : IPlayerDefeatStateCapability
    {
        private readonly int uiThread=Environment.CurrentManagedThreadId;
        private Action<PlayerDefeatSnapshot> changed;
        private PlayerDefeatSnapshot current;
        internal int Reads,LastPlayer;internal long LastSession;
        public bool TryRegisterStateObserver(string id,Action<PlayerDefeatSnapshot> callback,out NativeCapabilityDiagnostic diagnostic){changed=callback;diagnostic=null;if(current!=null)callback(current);return true;}
        public bool TryGetObservedState(long session,int player,out PlayerDefeatSnapshot snapshot){
            if(Environment.CurrentManagedThreadId!=uiThread)throw new Exception("Native observer callback queried UI state.");
            Reads++;LastPlayer=player;LastSession=session;snapshot=current;
            return snapshot!=null && snapshot.SessionId==session && snapshot.PlayerId==player;
        }
        internal void Publish(PlayerDefeatSnapshot snapshot){current=snapshot;changed?.Invoke(snapshot);}
    }
    internal sealed class RoleClientBoundary
    {
        private readonly RoleObservationBoundary observations;
        internal RoleClientBoundary(RoleObservationBoundary observations){this.observations=observations;}
        internal void WhenReady(Action<RoleClientBoundary> ready)=>ready(this);
        internal bool TryGetPlayerDefeat(out object capability,out NativeCapabilityDiagnostic diagnostic){capability=observations;diagnostic=null;return true;}
    }
    internal static class ApiShared { internal static RoleClientBoundary Client;internal static RoleClientBoundary ForMod(string owner)=>Client; }
    internal static class PlayerPerspectiveAPI { internal static int ActualPlayer;internal static int GetControlledPlayerId()=>ActualPlayer; }
}
