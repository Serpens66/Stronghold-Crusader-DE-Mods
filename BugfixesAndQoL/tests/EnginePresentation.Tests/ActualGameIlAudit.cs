using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;

namespace BugfixesAndQoL.VanillaPerformance
{
    // This executable reads the installed managed IL only. It never initializes
    // EngineInterface, patches game methods or calls any game/native entry point.
    internal static partial class EnginePresentationBridge
    {
        private static bool runValid, saveValid;
        private static void EnteredRun() { }
        private static void BeforePublish() { }
        private static void BeforeSave() { }
        internal static void Report(Exception error) { throw new Exception("Actual game IL rejected", error); }
        public static int Main(string[] args)
        {
            try
            {
                string managed=args[0];
                AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>
                {
                    string file=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");
                    return File.Exists(file)?Assembly.LoadFrom(file):null;
                };
                return Inspect();
            }
            catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        private static int Inspect()
        {
            foreach(string name in new[]{"run","SaveSaveGame"})
            {
                var method=typeof(EngineInterface).GetMethod(name);ValidateOriginal(method);
                var original=PatchProcessor.GetOriginalInstructions(method).ToList();
                var labels=original.Select(i=>i.labels.ToArray()).ToArray();
                var boundaries=original.Select(i=>i.blocks.ToArray()).ToArray();
                var result=Transpile(original,method).ToList();
                if(result.Count!=original.Count+(name=="run"?2:1) ||
                    result.Sum(i=>i.labels.Count)!=labels.Sum(l=>l.Length) ||
                    result.Sum(i=>i.blocks.Count)!=boundaries.Sum(b=>b.Length) ||
                    !original.Select((i,n)=>i.labels.SequenceEqual(labels[n])&&i.blocks.SequenceEqual(boundaries[n])).All(x=>x))
                    throw new Exception("Original IL metadata or callback count differs");
                Console.WriteLine("PASS installed game IL: "+method+"; original instructions="+original.Count+", labels/EH preserved");
            }
            ValidateLockedCall(typeof(EngineInterface).GetMethod("GetMeritData"),"DLL_GetMeritData");
            ValidateLockedCall(typeof(EngineInterface).GetMethod("SetAchData"),"DLL_SetAchValues");
            if(!runValid||!saveValid)throw new Exception("Partial bridge validation");
            Console.WriteLine("PASS installed original Merit/Achievement lock scopes and all required managed bridge anchors; no game execution.");
            return 0;
        }
    }
}
