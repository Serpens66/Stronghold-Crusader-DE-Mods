namespace CastlePlanner {
 internal static class BlueprintSpectatorPolicy {
  internal static bool IncludeStep(SHCDESE.Interop.Enums.AivBuildStepState state) =>
   state==SHCDESE.Interop.Enums.AivBuildStepState.Pending || state==SHCDESE.Interop.Enums.AivBuildStepState.Unknown2 || state==SHCDESE.Interop.Enums.AivBuildStepState.Built || state==SHCDESE.Interop.Enums.AivBuildStepState.RetryPending;
  internal static bool ShouldSample(ref int ticks,bool active){if(!active){ticks=0;return false;}if(++ticks<40)return false;ticks=0;return true;}

  internal static bool IsSpectator(bool mapActive,bool editor,bool multiplayerMode,bool spectatorFlag,int controlledPlayer,bool eliminatedParticipant=false) =>
   mapActive && !editor && multiplayerMode && ((spectatorFlag && controlledPlayer<=0) || eliminatedParticipant);
 }

 // Only confirmed positive-participant loss is retained. A later unreadable sample
 // cannot restore manual controls; the next mission explicitly starts a new role.
 internal sealed class BlueprintSpectatorRole {
  internal long SessionId { get; private set; }
  internal bool HasConfirmedLoss=>SessionId>0 && eliminatedPlayer>=1 && eliminatedPlayer<=8;
  private int eliminatedPlayer;
  private int dirty=1;
  internal void Begin(long session){SessionId=session;eliminatedPlayer=0;Invalidate();}
  internal void End(){Begin(0);}
  internal void Invalidate(){System.Threading.Interlocked.Exchange(ref dirty,1);}
  internal bool TakeInvalidation()=>System.Threading.Interlocked.Exchange(ref dirty,0)!=0;
  internal void Observe(long session,int observedPlayer,int actualPlayer,bool readable,bool eliminated){
   if(SessionId>0 && session==SessionId && actualPlayer>=1 && actualPlayer<=8 && observedPlayer==actualPlayer && readable && eliminated)
    eliminatedPlayer=actualPlayer;
  }
 }

 // Commands can be retained by an already open popup. Check the current local
 // role at execution as well as CanExecute; no shared preview state is changed.
 internal sealed class BlueprintParticipantCommand : System.Windows.Input.ICommand {
  private readonly System.Windows.Input.ICommand inner;
  private readonly System.Func<bool> allowed;
  internal BlueprintParticipantCommand(System.Windows.Input.ICommand inner,System.Func<bool> allowed){
   this.inner=inner??throw new System.ArgumentNullException(nameof(inner));
   this.allowed=allowed??throw new System.ArgumentNullException(nameof(allowed));
   inner.CanExecuteChanged+=OnInnerChanged;
  }
  public bool CanExecute(object parameter)=>allowed() && inner.CanExecute(parameter);
  public void Execute(object parameter){if(CanExecute(parameter))inner.Execute(parameter);}
  public event System.EventHandler CanExecuteChanged;
  internal void NotifyChanged()=>CanExecuteChanged?.Invoke(this,System.EventArgs.Empty);
  private void OnInnerChanged(object sender,System.EventArgs args)=>NotifyChanged();
 }
}
