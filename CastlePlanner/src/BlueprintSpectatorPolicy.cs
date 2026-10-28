namespace CastlePlanner {
 internal static class BlueprintSpectatorPolicy {
  internal static bool IncludeStep(SHCDESE.Interop.Enums.AivBuildStepState state) =>
   state==SHCDESE.Interop.Enums.AivBuildStepState.Pending || state==SHCDESE.Interop.Enums.AivBuildStepState.Unknown2 || state==SHCDESE.Interop.Enums.AivBuildStepState.Built || state==SHCDESE.Interop.Enums.AivBuildStepState.RetryPending;
  internal static bool ShouldSample(ref int ticks,bool active){if(!active){ticks=0;return false;}if(++ticks<40)return false;ticks=0;return true;}

  internal static bool IsSpectator(bool mapActive,bool editor,bool multiplayerMode,bool spectatorFlag,int controlledPlayer) =>
   mapActive && !editor && multiplayerMode && spectatorFlag && controlledPlayer<=0;
 }
}
