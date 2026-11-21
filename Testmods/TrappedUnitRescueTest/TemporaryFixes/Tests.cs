using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TrappedUnitRescueTest.TemporaryFixes
{
    internal static class Tests
    {
        internal static void Run(string path)
        {
            byte[] original = File.ReadAllBytes(path);
            using (var classification = ModuleDefinition.ReadModule(new MemoryStream(original)))
            {
                string state = ScannerCapture.Apply(classification);
                if (state != "patched")
                {
                    // A supported updated dependency must not fail the build merely because
                    // the historical defect is no longer available to transform.
                    if (state != "already-fixed" && state != "not-applicable-no-mounted-keep-installer")
                        throw new Exception("Unexpected capture classification: " + state);
                    if (!original.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Self-test modified source assembly.");
                    Console.WriteLine("PASS TemporaryFixes source=" + state + "; broken-capture-transformation=NOT_EXECUTED; original file unchanged.");
                    return;
                }
            }
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(original)))
            {
                var owner = module.GetType("Fixes.Detours.FixesUnitDetours");
                var install = owner.Methods.Single(m => m.Name == "InstallMountedKeepRecovery");
                var delayed = owner.NestedTypes.SelectMany(t => t.Methods).Single(m => m.Name.StartsWith("<InstallMountedKeepRecovery>b__") && m.ReturnType.FullName == "System.Void");
                Console.WriteLine("BROKEN=" + ScannerCapture.Fingerprint(install, delayed));
                var emitter = owner.Methods.Single(m => m.Name == "EmitMountedKeepRecovery");
                string emitterBefore = ScannerCapture.Fingerprint(emitter);
                Guid mvid = module.Mvid;
                if (ScannerCapture.Apply(module) != "patched") throw new Exception("Audited original not corrected.");
                if (ScannerCapture.Apply(module) != "already-fixed") throw new Exception("Correction is not idempotent.");
                if (module.Mvid != mvid || ScannerCapture.Fingerprint(emitter) != emitterBefore) throw new Exception("Correction changed emitter or module identity.");
                using (var serialized = new MemoryStream())
                {
                    module.Write(serialized); serialized.Position = 0;
                    using (var reread = ModuleDefinition.ReadModule(serialized))
                        if (ScannerCapture.Apply(reread) != "already-fixed") throw new Exception("Serialized correction lost contract.");
                }
                if (delayed.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_CurrentAddress")) throw new Exception("Deferred scanner read remains.");
                var field = delayed.Body.Instructions.Select(i => i.Operand).OfType<FieldReference>().Single(f => f.Name == "relocate");
                if (field.FieldType.MetadataType != MetadataType.UInt64) throw new Exception("Captured address remains mutable scanner object.");
                var store = install.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.Name == "relocate");
                if (!(store.Previous.Operand is MethodReference eager) || eager.Name != "get_CurrentAddress") throw new Exception("Address not captured immediately after Scan.");
                // A future source correction may omit our inert getter slot and name the
                // scalar relocateAddress. It is the same supported dataflow contract.
                ((FieldDefinition)field.Resolve()).Name = "relocateAddress";
                foreach (var nop in delayed.Body.Instructions.Where(i => i.OpCode == OpCodes.Nop).ToArray()) delayed.Body.Instructions.Remove(nop);
                if (ScannerCapture.Apply(module) != "already-fixed") throw new Exception("Equivalent future fixed contract was not skipped.");
            }
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(original)))
            {
                var install = module.GetType("Fixes.Detours.FixesUnitDetours").Methods.Single(m => m.Name == "InstallMountedKeepRecovery");
                var size = install.Body.Instructions.First(i => i.OpCode == OpCodes.Ldc_I4_S && Convert.ToInt32(i.Operand) == 14);
                size.Operand = (sbyte)15;
                using (var before = new MemoryStream())
                {
                    module.Write(before); byte[] untouched = before.ToArray(); bool rejected = false;
                    try { ScannerCapture.Apply(module); } catch (InvalidOperationException) { rejected = true; }
                    using (var after = new MemoryStream()) { module.Write(after); if (!untouched.SequenceEqual(after.ToArray())) throw new Exception("Unknown contract was partially modified."); }
                    if (!rejected) throw new Exception("Unknown installer was accepted.");
                }
            }
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(original)))
            {
                var owner = module.GetType("Fixes.Detours.FixesUnitDetours");
                module.Assembly.Name.Version = new Version(99,0,0,0);
                var unrelated = new MethodDefinition("UnrelatedFutureFeature", MethodAttributes.Static | MethodAttributes.Public, module.TypeSystem.Void);
                unrelated.Body.Instructions.Add(Instruction.Create(OpCodes.Ret)); owner.Methods.Add(unrelated);
                if (ScannerCapture.Apply(module) != "patched") throw new Exception("Unrelated update blocked the capture correction.");
            }
            if (!original.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Self-test modified source assembly.");
            Console.WriteLine("PASS TemporaryFixes original-Cecil correction, idempotence, serialized closure, native emitter unchanged, unknown contract untouched, original file unchanged.");
        }
    }
}
