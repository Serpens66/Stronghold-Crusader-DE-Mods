using System;
using System.Collections.Generic;
namespace Spectator {
internal static class SpectatorSelectionTests {
 static int Main() {
  var calls = new List<string>();
  var controller = new PlayerSelectionController(() => calls.Add("cancel"), p => calls.Add("publish:" + p));
  Require(controller.Select(2) && calls.Count == 2 && calls[0] == "cancel" && calls[1] == "publish:2", "ordered cancellation/publication");
  Require(!controller.Select(2) && calls.Count == 2, "same perspective silent");
  int queuedRecipient = controller.PlayerId;
  Require(controller.Select(5) && controller.PlayerId == 5 && queuedRecipient == 2, "fixed captured recipient");
  Require(controller.Select(0) && controller.PlayerId == -1, "invalid player clears selection");
  Require(!controller.Select(9), "invalid repeated selection silent");
  Require(controller.Select(8) && controller.Select(-1), "session reset");
  controller.Select(5); calls.Clear();
  for(int snapshot=0;snapshot<120;snapshot++) {
   bool selfEligible = ToolSessionPolicy.CanSelect(false,true,true,false,true);
   Require(!controller.Revalidate(selfEligible) && controller.PlayerId==5,"live human test recipient survives every new state snapshot");
  }
  Require(calls.Count==0,"stable roster snapshots must not cancel an active placement cursor");
  Require(controller.Revalidate(ToolSessionPolicy.CanSelect(false,true,true,false,false)) && controller.PlayerId==-1,
   "self ceases to be eligible on defeat even while its roster slot stays occupied");
  Require(calls.Count==2 && calls[0]=="cancel" && calls[1]=="publish:-1","roster invalidation uses the same ordered transition boundary");
  Require(!controller.Revalidate(false) && calls.Count==2,"later invalid snapshots cancel exactly once");
  foreach(int ai in new[]{2,8}) {
   controller.Select(ai); calls.Clear();
   for(int snapshot=0;snapshot<12;snapshot++)
    Require(!controller.Revalidate(ToolSessionPolicy.CanSelect(false,true,true,true,false)) && controller.PlayerId==ai,"sparse occupied AI recipients remain selected");
   Require(calls.Count==0,"unchanged AI selection stays silent");
   Require(controller.Revalidate(ToolSessionPolicy.CanSelect(false,true,false,false,false)),"vacated slots are invalidated");
  }
  controller.Select(5);
  bool networkTools = ToolSessionPolicy.CanMutate(true,true,true,true,true,true,false);
  Require(controller.Revalidate(ToolSessionPolicy.CanSelect(false,networkTools,true,false,true)),"network veto invalidates the active-player testing selection");
  controller.Select(5);
  Require(controller.Revalidate(ToolSessionPolicy.CanSelect(false,false,true,false,true)),"turning testing off removes the human testing recipient");
  controller.Select(2); calls.Clear();
  Require(!controller.Revalidate(ToolSessionPolicy.CanSelect(true,false,true,false,false)) && calls.Count==0,
   "spectator perspective eligibility stays independent of mutation rights and recipient life state");
  Console.WriteLine("PASS: production selection transitions, roster revalidation, active-human retention, cancellation and fixed capture."); return 0;
 }
 static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
}}
