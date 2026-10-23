using System;
using System.Collections.Generic;
using System.IO;

namespace DependencyBootstrapProbe
{
    internal static class PairTests
    {
        internal static void Run()
        {
            // One tiny reusable fixture; synthetic bytes are never executed as plugins.
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../pair-fixture"));
            string runtime = Path.Combine(root, PairContract.Runtime), bootstrap = Path.Combine(root, PairContract.Bootstrap), manifest = Path.Combine(root, PairContract.Manifest);
            Directory.CreateDirectory(Path.GetDirectoryName(runtime)); Directory.CreateDirectory(Path.GetDirectoryName(bootstrap));
            File.WriteAllText(runtime, "synthetic runtime"); File.WriteAllText(bootstrap, "synthetic bootstrap");
            var data = new Dictionary<string, object> { { "Schema", 1 }, { "Guid", PairContract.Guid }, { "Version", "1.0.0" },
                { "RuntimeSHA256", ProbeIdentity.Hash(runtime).ToLowerInvariant() }, { "BootstrapSHA256", ProbeIdentity.Hash(bootstrap).ToLowerInvariant() } };
            Action write = () => File.WriteAllText(manifest, Shared.DependencyFreeJson.Serialize(data)); write();
            string hash = ProbeIdentity.Hash(bootstrap);
            Func<PairContract> read = () => PairContract.Read(root, bootstrap, hash);
            var winner = new ShadowPlugin { Guid = PairContract.Guid, Version = "1.0.0", Path = runtime };
            var pair = read(); pair.Bind(new[] { winner }, bootstrap, hash);
            Reject(() => PairContract.Read(root, runtime, hash));
            Reject(() => PairContract.Read(root, bootstrap, new string('0',64)));
            Reject(() => pair.Bind(new ShadowPlugin[0], bootstrap, hash));
            Reject(() => pair.Bind(new[] { winner, winner }, bootstrap, hash));
            winner.Guid = "wrong"; Reject(() => pair.Bind(new[] { winner }, bootstrap, hash)); winner.Guid = PairContract.Guid;
            winner.Version = "2.0.0"; Reject(() => pair.Bind(new[] { winner }, bootstrap, hash)); winner.Version = "1.0.0";
            winner.Path = bootstrap; Reject(() => pair.Bind(new[] { winner }, bootstrap, hash)); winner.Path = runtime;
            string original = File.ReadAllText(runtime); File.AppendAllText(runtime,"changed at same version"); Reject(() => read()); File.WriteAllText(runtime,original);
            File.Move(runtime,runtime+".absent"); try { Reject(() => read()); } finally { File.Move(runtime+".absent",runtime); }
            string duplicate = Path.Combine(Path.GetDirectoryName(bootstrap),"renamed.dll");
            File.Copy(bootstrap,duplicate,true); try { Reject(() => read()); } finally { File.Delete(duplicate); }
            File.Copy(typeof(PairTests).Assembly.Location,duplicate,true);
            // Unrelated managed assemblies must be allowed.
            read(); File.Delete(duplicate);
            using(var competing = Mono.Cecil.AssemblyDefinition.CreateAssembly(new Mono.Cecil.AssemblyNameDefinition("DependencyBootstrapProbe.Patcher",new Version(9,0)),"Competing",Mono.Cecil.ModuleKind.Dll))
                competing.Write(duplicate);
            try { Reject(() => read()); } finally { File.Delete(duplicate); }
            data["Version"]="invalid"; write(); Reject(() => read()); data["Version"]="1.0.0";
            data["Extra"]="unexpected"; write(); Reject(() => read()); data.Remove("Extra");
            data["Schema"]="1"; write(); Reject(() => read()); data["Schema"]=1;
            write(); File.AppendAllText(manifest," "); Reject(() => pair.Bind(new[]{winner},bootstrap,hash)); write();
            File.Move(manifest,manifest+".absent"); try { Reject(() => read()); } finally { File.Move(manifest+".absent",manifest); }
            pair.Bind(new[] { winner },bootstrap,hash);
            var dictionary = new Dictionary<string, object> { { "old", new object() } };
            if (!ReferenceEquals(dictionary, ShadowRuntime.SelectIndex(dictionary, () => { throw new IOException("preparation"); }, true)))
                throw new Exception("Preparation failure changed original index");
            if (PairActivation.Bound) throw new Exception("Unprepared package activated correction");
            foreach (bool requested in new[] { false, true }) foreach (bool bound in new[] { false, true })
                if (PairActivation.AllowsCorrection(requested,bound) != (requested && bound)) throw new Exception("Activation gate mismatch");
            // Exercise all interrupted update orders through the actual runtime reader.
            var paths = new[] { runtime, bootstrap, manifest };
            var old = Array.ConvertAll(paths, File.ReadAllBytes);
            File.AppendAllText(runtime," revision B"); File.AppendAllText(bootstrap," revision B");
            data["RuntimeSHA256"]=ProbeIdentity.Hash(runtime).ToLowerInvariant(); data["BootstrapSHA256"]=ProbeIdentity.Hash(bootstrap).ToLowerInvariant(); write();
            var next = Array.ConvertAll(paths, File.ReadAllBytes); string newHash=ProbeIdentity.Hash(bootstrap);
            foreach (string order in new[] { "012","021","102","120","201","210" }) foreach(bool upgrade in new[]{false,true})
            {
                for(int i=0;i<3;i++) { if(upgrade) File.WriteAllBytes(paths[i],old[i]); else File.Delete(paths[i]); }
                for(int step=0;step<3;step++)
                {
                    int index=order[step]-'0'; File.WriteAllBytes(paths[index],next[index]);
                    if(step<2) Reject(() => PairContract.Read(root,bootstrap,newHash));
                    else PairContract.Read(root,bootstrap,newHash).Bind(new[]{winner},bootstrap,newHash);
                }
            }
            for(int i=0;i<3;i++) File.WriteAllBytes(paths[i],old[i]);
            Console.WriteLine("PASS: actual runtime pair code, selected identity, changed/missing/mixed files, duplicates, invalid metadata, original-index fallback");
        }
        private static void Reject(Action action)
        {
            try { action(); } catch { return; }
            throw new Exception("Invalid package accepted");
        }
    }
}
