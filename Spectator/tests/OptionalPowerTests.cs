namespace Noesis { public class ImageSource { } public static class Marshal { } }
namespace Spectator
{
    public static class OptionalPowerTests
    {
        public static int Main(string[] args)
        {
            try { if (args.Length > 1 && args[1] == "absent") { var p = new ToolPower { Id=5, Cost=3816, TargetKind=0 }; OptionalExtremePowers.Describe(p); Expect(p.Api==null && p.Cost==3816,"absent provider"); System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(args[0])); OptionalExtremePowers.Describe(p); Expect(p.Api==null,"absence cached after startup discovery"); return 0; } int count = Run(args[0]); System.Console.WriteLine("PASS: " + count + " production optional-adapter checks (absence, tuning, replacement, explicit AI, once, rejection, unavailable)."); return 0; }
            catch (System.Exception error) { System.Console.Error.WriteLine(error); return 1; }
        }
        private static int checks;
        private static void Expect(bool value, string label)
        { checks++; if (!value) throw new System.Exception(label); }
        public static int Run(string fixturePath)
        {
            var assembly = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(fixturePath));
            object api = assembly.GetType("ExtremePowers.API.ExtremePowersBootstrap").GetProperty("Instance").GetValue(null, null);
            var configured = new ToolPower { Id = 5 };
            OptionalExtremePowers.Describe(configured);
            Expect(configured.Api == api && configured.Cost == 20000, "existing cost tuning retained");
            var replacement = System.Activator.CreateInstance(assembly.GetType("ExtremePowers.API.Replacement"));
            var kind = replacement.GetType().GetProperty("TargetKind");
            kind.SetValue(replacement, System.Enum.ToObject(kind.PropertyType, 1), null);
            api.GetType().GetField("Replacement").SetValue(api, replacement);
            var spawn = new ToolPower { Id = 5 };
            OptionalExtremePowers.Describe(spawn);
            Expect(spawn.TargetKind == 1 && spawn.Name == "Configured spawn" && spawn.Sprite == "custom spawn" && spawn.Help == "Existing mod callback", "replacement metadata and map target retained");
            Expect(!spawn.SameContract(configured), "replacement change invalidates queued action");
            var repeated = new ToolPower { Id = 5 };
            OptionalExtremePowers.Describe(repeated);
            Expect(spawn.SameContract(repeated), "unchanged provider contract accepted");
            string reason;
            Expect(OptionalExtremePowers.ExecuteReplacement(spawn, 7, 1234, 991, 42, out reason), "existing replacement executes");
            var last = api.GetType().GetField("Last").GetValue(api);
            Expect((int)last.GetType().GetField("Player").GetValue(last) == 7 && (ulong)last.GetType().GetField("Operation").GetValue(last) == 991, "explicit AI and operation preserved");
            object target = last.GetType().GetField("Target").GetValue(last);
            Expect((int)target.GetType().GetField("Tile").GetValue(target) == 1234 && (int)api.GetType().GetField("Calls").GetValue(api) == 1, "map target and exactly one callback");
            api.GetType().GetField("Reject").SetValue(api, true);
            Expect(!OptionalExtremePowers.ExecuteReplacement(spawn, 7, 1234, 992, 42, out reason) && reason == "fixture rejection", "provider rejection retained");
            api.GetType().GetField("SessionReady").SetValue(api, false);
            var disabled = new ToolPower { Id=5, Cost=3816, TargetKind=0 };
            OptionalExtremePowers.Describe(disabled);
            Expect(disabled.Api==null && disabled.Replacement==null && disabled.Cost==3816 && disabled.TargetKind==0, "inactive provider retains Vanilla power");
            api.GetType().GetField("SessionReady").SetValue(api, true);
            api.GetType().GetProperty("NativeBackendAvailable").SetValue(api, false, null);
            bool unavailable = false;
            try { OptionalExtremePowers.Describe(new ToolPower { Id = 0 }); } catch (System.InvalidOperationException) { unavailable = true; }
            Expect(unavailable, "unavailable optional backend fails closed");
            for (int i=0;i<100;i++) { var rejectedPower = new ToolPower { Id=0 }; OptionalExtremePowers.Describe(rejectedPower); Expect(rejectedPower.TargetKind == -1, "faulted provider stays disabled without repeated exceptions"); }
            return checks;
        }
    }
}
