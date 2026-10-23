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
  Console.WriteLine("PASS: production selection controller transitions, cancellation and fixed capture."); return 0;
 }
 static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
}}
