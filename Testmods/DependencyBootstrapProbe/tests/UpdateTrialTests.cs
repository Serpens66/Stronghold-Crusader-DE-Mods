using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace DependencyBootstrapProbe
{
    internal static class UpdateTrialTests
    {
        private static void Require(bool condition, string text) { if(!condition) throw new Exception(text); }
        internal static void Run()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-adapter-fixture");
            Directory.CreateDirectory(root); string map=Path.Combine(root,LocalUpdateTrial.MapName); File.WriteAllText(map,"not an executed package");
            string hash=new string('A',64), other=new string('B',64);
            var data=new Dictionary<string,object>{{"Schema",1},{"TrialId",Guid.NewGuid().ToString("N")},{"MapSHA256",ProbeIdentity.Hash(map)},
                {"Builds",new List<object>{new Dictionary<string,object>{{"Version","0.1.1"},{"PatcherSHA256",hash},{"RuntimeSHA256",hash}},
                    new Dictionary<string,object>{{"Version","0.1.2"},{"PatcherSHA256",other},{"RuntimeSHA256",other}}}}};
            var original=new List<string>{"C:/original-a","C:/original-b"};
            Func<string,List<string>> apply=json=>LocalUpdateTrial.Extend(original,json,root,hash,hash,"0.1.1");
            Require(ReferenceEquals(original,apply(null)),"Disabled adapter changed input");
            string json=Shared.DependencyFreeJson.Serialize(data);
            var extended=apply(json); Require(!ReferenceEquals(original,extended) && original.Count==2 && extended.Take(2).SequenceEqual(original) && extended.Count==3 && extended[2]==root,"List copy contract");
            Reject(()=>apply("{}")); Reject(()=>apply(json.Replace(hash,other))); Reject(()=>apply(json.Replace("0.1.1","9.9.9")));
            File.AppendAllText(map,"changed"); Reject(()=>apply(json)); File.WriteAllText(map,"not an executed package");
            original.Add(root); Reject(()=>apply(json)); original.RemoveAt(2);
            string extra=Path.Combine(root,"ambiguous.map"); File.WriteAllText(extra,"x"); try{Reject(()=>apply(json));}finally{File.Delete(extra);}
            Require(original.SequenceEqual(new[]{"C:/original-a","C:/original-b"}),"Rejected request mutated original");
            Console.WriteLine("PASS: local adapter disabled, exact copy/append, invalid build/request/hash and duplicate/ambiguous paths");
        }
        private static void Reject(Action action){try{action();}catch{return;}throw new Exception("Invalid request accepted");}
        internal static void Package(string game,string root)
        {
            var assembly=Assembly.LoadFrom(Path.Combine(game,"BepInEx/plugins/000shcdese/SHCDESE.dll"));
            var archiveType=assembly.GetType("SHCDESE.API.Components.Archive.MapArchive",true);
            object[] args={Path.Combine(root,"remote",LocalUpdateTrial.MapName),null};
            Require((bool)archiveType.GetMethod("TryLoad").Invoke(null,args),"Installed Extender rejected test map");
            using(var archive=(IDisposable)args[1])
            {
                var managerType=assembly.GetType("SHCDESE.API.Components.Archive.MapModManager",true);
                object manager=FormatterServices.GetUninitializedObject(managerType);
                string se=Path.Combine(root,"validation-state");Directory.CreateDirectory(se);
                managerType.GetField("_seDirectory",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,se);
                string receipt=Path.Combine(se,Path.GetFileNameWithoutExtension(LocalUpdateTrial.MapName)+".json");
                var should=managerType.GetMethod("ShouldUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
                File.Copy(Path.Combine(root,"A/BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json"),receipt,true);
                Require((bool)should.Invoke(manager,new[]{Path.GetFileNameWithoutExtension(LocalUpdateTrial.MapName),args[1]}),"A -> B not recognized");
                File.Copy(Path.Combine(root,"B/BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json"),receipt,true);
                Require(!(bool)should.Invoke(manager,new[]{Path.GetFileNameWithoutExtension(LocalUpdateTrial.MapName),args[1]}),"B would update repeatedly");
            }
            Console.WriteLine("PASS: installed Extender loads actual map; original version decision accepts A -> B and rejects B -> B. No staging/updater executed.");
        }
    }
}
