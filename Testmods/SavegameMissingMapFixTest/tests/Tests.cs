using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace SavegameMissingMapFixTest
{
    using Restart = HUD_IngameMenu.RestartSkirmishMapInfo;
    internal static class Launcher
    {
        private static string game;
        public static int Main(string[] args)
        {
            game = args[0];
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try { StartupTests.Run(game); Run(args[1]); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string file = new AssemblyName(args.Name).Name + ".dll";
            foreach (string root in new[] { "BepInEx/core", "Stronghold Crusader Definitive Edition_Data/Managed", "BepInEx/plugins/000shcdese" })
            {
                string path = Path.Combine(game, root, file);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string save) { Tests.Run(save); }
    }

    internal static class Tests
    {
        private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static FileHeader installed;
        private static FileHeader installedTrail;
        private static Func<EngineInterface.MultiplayerSetupData, string, bool, bool> parseSetup;
        private static string[] PortableSplit(string value, string separator, StringSplitOptions options) => value.Split(new[] { separator }, options);
        private static bool ParseSetup(EngineInterface.MultiplayerSetupData setup, string value, bool ignoreOrder) => parseSetup(setup, value, ignoreOrder);
        private static MapFileManager NullManager() => null;
        private static MainViewModel NullMain() => null;
        private static FileHeader Catalog(MapFileManager manager, string name, bool builtin, bool workshop) => installed;
        private static FileHeader CatalogTrail(MapFileManager manager, string trail, string file) => installedTrail;
        private static Noesis.TextureSource NullImage(MainViewModel view, byte[] bytes) => null;
        private static void TraceException(Exception ex) => Console.WriteLine("Isolated Vanilla decoder exception: " + ex);
        // Only desktop tests replace Unity/Noesis and catalog access. The production transform is unchanged.
        private static void Isolate(ILContext il)
        {
            foreach (var handler in il.Body.ExceptionHandlers)
                if (handler.HandlerStart.OpCode == OpCodes.Pop)
                {
                    handler.HandlerStart.OpCode = OpCodes.Call;
                    handler.HandlerStart.Operand = il.Import(typeof(Tests).GetMethod(nameof(TraceException), BindingFlags.Static | BindingFlags.NonPublic));
                }
            foreach (var i in il.Body.Instructions.ToArray())
            {
                if (!(i.Operand is MethodReference m)) continue;
                string replacement = null;
                if (m.DeclaringType.FullName == "MapFileManager" && m.Name == "get_Instance") replacement = nameof(NullManager);
                if (m.DeclaringType.FullName == "CrusaderDE.MainViewModel" && m.Name == "get_Instance") replacement = nameof(NullMain);
                if (m.Name == "GetHeaderFromFileNameForSkirmishRestart") replacement = nameof(Catalog);
                if (m.Name == "LoadImageFile") replacement = nameof(NullImage);
                if (m.Name == "FromString" && m.DeclaringType.Name == "MultiplayerSetupData") replacement = nameof(ParseSetup);
                if (m.Name == "TrailLookup" && m.DeclaringType.Name == "SavegameFix") replacement = nameof(CatalogTrail);
                if (replacement == null) continue;
                i.OpCode = OpCodes.Call;
                i.Operand = il.Import(typeof(Tests).GetMethod(replacement, BindingFlags.NonPublic | BindingFlags.Static));
            }
        }
        internal static void Run(string save)
        {
            SavegameFix.ValidateMembers();
            // Unity Mono supplies String.Split(string, options), desktop .NET Framework does not.
            // Execute a clone of the real setup parser with only this BCL overload adapted.
            using (var setup = new DynamicMethodDefinition(typeof(EngineInterface.MultiplayerSetupData).GetMethod("FromString")))
            using (var il = new ILContext(setup.Definition))
            {
                foreach (var i in il.Body.Instructions)
                    if (i.Operand is MethodReference m && m.DeclaringType.FullName == "System.String" && m.Name == "Split" && m.Parameters[0].ParameterType.FullName == "System.String")
                    {
                        i.OpCode = OpCodes.Call;
                        i.Operand = il.Import(typeof(Tests).GetMethod(nameof(PortableSplit), BindingFlags.NonPublic | BindingFlags.Static));
                    }
                parseSetup = (Func<EngineInterface.MultiplayerSetupData, string, bool, bool>)setup.Generate().CreateDelegate(typeof(Func<EngineInterface.MultiplayerSetupData,string,bool,bool>));
            }
            byte[] payload;
            using (var reader = new BinaryReader(File.OpenRead(save)))
            {
                reader.ReadUInt32();
                for (int i = 0; i < 6; i++) { uint length = reader.ReadUInt32(); reader.BaseStream.Seek(length, SeekOrigin.Current); }
                payload = reader.ReadBytes(reader.ReadInt32());
            }
            using (var decode = new DynamicMethodDefinition(SavegameFix.Decode))
            using (var encode = new DynamicMethodDefinition(SavegameFix.Encode))
            {
                using (var il = new ILContext(decode.Definition)) { SavegameFix.ModifyDecode(il); Isolate(il); }
                using (var il = new ILContext(encode.Definition)) SavegameFix.ModifyEncode(il);
                var read = (Func<byte[], bool, Restart>)decode.Generate().CreateDelegate(typeof(Func<byte[],bool,Restart>));
                var write = (Func<Restart, byte[]>)encode.Generate().CreateDelegate(typeof(Func<Restart,byte[]>));
                SavegameFix.Enabled = false;
                Require(read(payload, false) == null, "Inactive hook changed Vanilla rejection.");
                SavegameFix.Enabled = true;
                var recovered = read(payload, false);
                Require(recovered != null && recovered.selectedHeader == null, "Missing map not recovered or fabricated as installed.");
                Require(recovered.aivs[0].lordName == "Mansur" && recovered.aivs[1].lordName.EndsWith("The Leopard"), "Lord identities lost.");
                Require(recovered.aivs[0].imageData.Length == 43750 && recovered.aivs[1].imageData.Length == 11956, "Portrait data lost.");
                Require(recovered.aivs[0].lordConfig != null && recovered.aivs[1].lordConfig != null && recovered.aivs[0].aivs.Count == 1, "AI data lost.");
                Require(write(recovered).SequenceEqual(payload), "Recovered encode differs from original save payload.");
                var second = read(write(recovered), false);
                Require(write(second).SequenceEqual(payload), "Second load/save lost metadata.");
                Require(SavegameFix.Missing(second), "Missing-map state not attached to second object.");
                var menu = (HUD_IngameMenu)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HUD_IngameMenu));
                menu.restartSkirmishMapInfo = second;
                Require(SavegameFix.BlockAction(menu, 5) && !SavegameFix.BlockAction(menu, 1), "Restart guard changed other menu actions.");
                SavegameFix.Enabled = false;
                Require(!SavegameFix.BlockAction(menu, 5), "Inactive action guard changed Vanilla.");
                SavegameFix.Enabled = true;
                Require(read(new byte[] { 60, 8 }, false) == null, "Truncated data accepted.");
                installed = new FileHeader { workshopMap = true, fileName = "Tactical Boardgame (2 Player)" };
                var normal = read(payload, false);
                Require(ReferenceEquals(normal.selectedHeader, installed) && !SavegameFix.Missing(normal), "Installed map changed.");
                Require(write(normal).SequenceEqual(payload), "Installed-map encoding changed.");
                menu.restartSkirmishMapInfo = normal;
                Require(!SavegameFix.BlockAction(menu, 5), "Installed-map restart was blocked.");
                installed = null;
                byte[] local = (byte[])payload.Clone(); local[5 + 8 * 12] = 0;
                Require(write(read(local, false)).SequenceEqual(local), "Local map identity lost.");
                byte[] builtin = (byte[])payload.Clone(); builtin[5 + 8 * 12] = 1;
                Require(write(read(builtin, false)).SequenceEqual(builtin), "Built-in map identity lost.");
                Require(write(read(payload, true)).SequenceEqual(payload), "Editor/trail decode lost serialization identity.");
                byte[] trail = (byte[])payload.Clone(); trail[trail.Length - 13] = 1;
                Require(write(read(trail, false)).SequenceEqual(trail), "Missing custom-trail map lost data.");
                installedTrail = new FileHeader { fileName = "Tactical Boardgame (2 Player)", workshopMap = true };
                Require(ReferenceEquals(read(trail, false).selectedHeader, installedTrail), "Available custom-trail fallback was bypassed.");
                Console.WriteLine("PASS: transformed installed Vanilla decoder/encoder executed; disabled, missing/installed/local/builtin, portrait/AIC/AIV, repeated roundtrip and corruption cases.");
            }
            foreach (var pair in new[] { Tuple.Create(SavegameFix.Menu, (ILContext.Manipulator)SavegameFix.ModifyMenu), Tuple.Create(SavegameFix.Action, (ILContext.Manipulator)SavegameFix.ModifyAction) })
                using (var copy = new DynamicMethodDefinition(pair.Item1))
                {
                    using (var il = new ILContext(copy.Definition)) pair.Item2(il);
                    GC.KeepAlive(copy.Generate());
                }
            using (var copy = new DynamicMethodDefinition(SavegameFix.Encode))
            using (var il = new ILContext(copy.Definition))
            {
                var target = il.Body.Instructions.First(i => i.Operand is FieldReference f && f.Name == "selectedHeader");
                ((FieldReference)target.Operand).Name = "changedHeader";
                string before = string.Join("\n", il.Body.Instructions.Select(i => i.ToString()));
                bool rejected = false;
                try { SavegameFix.ModifyEncode(il); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && before == string.Join("\n", il.Body.Instructions.Select(i => i.ToString())), "Changed IL was not rejected atomically.");
            }
            Console.WriteLine("PASS: actual menu/action IL transformed and generated; changed encoder contract rejected before mutation.");
        }
    }
}
