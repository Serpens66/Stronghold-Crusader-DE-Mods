using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Logging;
using BugfixesAndQoL;
internal static class ActivationTests {
 private sealed class DebugListener : ILogListener {
  public LogLevel DisplayedLogLevel => LogLevel.Debug;
  public void LogEvent(object sender, LogEventArgs args) { }
  public void Dispose() { }
 }
 private static int count;
 private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); count++; }
 private static FieldInfo Field(string name) => typeof(AiRaidRetargetFixRuntime).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
 internal static void Run() {
  var logger = new ManualLogSource("Raid integration tests");
  var lines = new List<string>(); var levels = new List<LogLevel>();
  logger.LogEvent += (sender, args) => { lines.Add(args.Data.ToString()); levels.Add(args.Level); };
  var runtime = new AiRaidRetargetFixRuntime(logger);
  var activation = (RaidActivationState)Field("activation").GetValue(runtime);
  Field("initialized").SetValue(runtime, true);
  runtime.SetEnabled(true);
  runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId=1 });
  Check(!(bool)Field("active").GetValue(runtime), "Missing hook prevents correction");
  activation.Available=true;
  runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId=2, IsEditor=true });
  Check(!(bool)Field("active").GetValue(runtime), "Editor excluded");
  runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId=3, IsReplay=true });
  Check((bool)Field("active").GetValue(runtime), "Cached session delivery activates persistent correction");
  var frames = (IDictionary)Field("pendingAttackCandidates").GetValue(runtime);
  var valueType=Field("pendingAttackCandidates").FieldType.GetGenericArguments()[1];
  frames.Add(1,Activator.CreateInstance(valueType));
    var retries = (IDictionary)Field("pendingRaidRetries").GetValue(runtime);
  var rejected = (IDictionary)Field("rejectedRaidTargets").GetValue(runtime);
  object key=Activator.CreateInstance(Field("pendingRaidRetries").FieldType.GetGenericArguments()[0]);
  retries.Add(key,null); rejected.Add(key,null);
  long epoch=(long)Field("observationEpoch").GetValue(runtime);
  runtime.SetEnabled(false);
  Check(!(bool)Field("active").GetValue(runtime) && frames.Count==0 && retries.Count==0 && rejected.Count==0, "Disable clears pending frames/retries/rejections");
  Check((long)Field("observationEpoch").GetValue(runtime)>epoch, "Disable invalidates search evidence");
  runtime.SetEnabled(true);
  Check((bool)Field("active").GetValue(runtime) && frames.Count==0, "Reenable waits for a new attack");
  runtime.OnSessionEnded();
  Check(!(bool)Field("active").GetValue(runtime) && frames.Count==0, "Map end clears state");
  runtime.SetEnabled(true);
  Check(!(bool)Field("active").GetValue(runtime), "No active session no correction");
  runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId=4 });
  Check((bool)Field("active").GetValue(runtime), "Next map active");
  lines.Clear();
  runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId=5 });
  var retryType=typeof(AiRaidRetargetFixRuntime).GetNestedType("RaidRetry",BindingFlags.NonPublic);
  var logRetry=typeof(AiRaidRetargetFixRuntime).GetMethod("LogRetry",BindingFlags.NonPublic|BindingFlags.Instance);
  var debugListener = new DebugListener();
  Logger.Listeners.Add(debugListener);
  var cacheExpiry = typeof(Shared.DebugLogHelper).GetField("debugEnabledCacheExpiresAtUtc", BindingFlags.NonPublic | BindingFlags.Static);
  cacheExpiry.SetValue(null, DateTime.MinValue);
  levels.Clear();
  for(int i=0;i<24000;i++) {
   object retry=Activator.CreateInstance(retryType,BindingFlags.NonPublic|BindingFlags.Instance,null,
    new object[] {i%8+1,i%6,i+1,(uint)(i+1),2,119,6450u,0,i},null);
   logRetry.Invoke(runtime,new object[] {retry,"selected",204,1371841u,0,0u});
  }
  Check(lines.Count==1 && lines[0].Contains("AI_RAID_RETRY_CONFIRMED"), "24000 successful retries only one compact confirmation per session");
  var repeated=typeof(AiRaidRetargetFixRuntime).GetMethod("LogRepeated",BindingFlags.NonPublic|BindingFlags.Instance);
  for(int i=0;i<10000;i++) repeated.Invoke(runtime,new object[] {"retry:nativeSearchObserved:invalidStand","validation warning",true});
  repeated.Invoke(runtime,new object[] {"retry:nativeSearchObserved:invalidBuilding","distinct warning",true});
  Check(lines.Count==3 && (int)Field("suppressedMessageCount").GetValue(runtime)==9999,"Warnings bounded but distinct validation reasons retained");
  runtime.OnSessionEnded();
  Check(lines.Count==4 && lines[3].Contains("selected=24000") && lines[3].Contains("suppressedWarnings=9999"),"Single correct session summary after long stream");
  Check(levels[0]==LogLevel.Debug && levels[1]==LogLevel.Warning && levels[2]==LogLevel.Warning && levels[3]==LogLevel.Debug,
   "Retry confirmation and summary are Debug; validation warnings remain Warning");
  int bytes=System.Text.Encoding.UTF8.GetByteCount(String.Join("\r\n",lines));
  Check(bytes<100000,"Minimal logging below 100 KB for 34001 simulated operations");
  Logger.Listeners.Remove(debugListener);
  cacheExpiry.SetValue(null, DateTime.MinValue);
  lines.Clear(); levels.Clear();
  runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId=6 });
  object quietRetry=Activator.CreateInstance(retryType,BindingFlags.NonPublic|BindingFlags.Instance,null,
   new object[] {1,0,1,1u,2,119,6450u,0,0},null);
  logRetry.Invoke(runtime,new object[] {quietRetry,"selected",204,1371841u,0,0u});
  repeated.Invoke(runtime,new object[] {"debug-off-warning","visible validation warning",true});
  runtime.OnSessionEnded();
  Check(lines.Count==1 && levels[0]==LogLevel.Warning && lines[0].Contains("visible validation warning"),
   "Without Debug, confirmations and summaries are suppressed while validation warnings remain visible");
  Console.WriteLine("PASS: "+count+" runtime activation/lifecycle/logging assertions; simulated stream log bytes="+bytes+".");
 }
}
