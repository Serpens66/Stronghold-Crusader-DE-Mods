using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TrappedUnitRescueTest.TemporaryFixes
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 2) throw new ArgumentException("--patch-copy <workspace/.work/.../fixes.dll> | --self-test <read-only fixes.dll>");
                if (args[0] == "--self-test") Tests.Run(args[1]);
                else if (args[0] == "--patch-copy") PatchCopy(args[1]);
                else throw new ArgumentException("Unknown operation.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] TEMP_FIXES_ERROR " + error); return 1; }
        }

        private static void PatchCopy(string path)
        {
            path = Path.GetFullPath(path);
            // The tool is intentionally unusable on installed or external-source assemblies.
            // Walk up from its own source/build tree to the containing workspace.
            var root = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
            if (root == null) throw new InvalidOperationException("Workspace identity unavailable.");
            string allowed = Path.Combine(root.FullName, ".work") + Path.DirectorySeparatorChar;
            if (!path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFileName(path), "fixes.dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to modify anything except the explicit workspace .work fixture fixes.dll.");
            // Refuse junctions/symlinks that could redirect an allowed fixture path outside it.
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)); directory != null && directory.FullName.Length >= root.FullName.Length; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Fixture path contains a reparse point.");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Fixture DLL is a reparse point.");
            byte[] original = File.ReadAllBytes(path);
            byte[] changed;
            using (var input = new MemoryStream(original))
            using (var module = ModuleDefinition.ReadModule(input))
            {
                string status = ScannerCapture.Apply(module);
                if (status != "patched") { Console.WriteLine("TEMP_FIXES " + status); return; }
                using (var output = new MemoryStream()) { module.Write(output); changed = output.ToArray(); }
            }
            // Validate the serialized candidate before replacing even this disposable copy.
            using (var verify = ModuleDefinition.ReadModule(new MemoryStream(changed)))
                if (ScannerCapture.Apply(verify) != "already-fixed") throw new InvalidOperationException("Serialized capture contract failed.");
            string pending = path + ".temporary-fixes-pending";
            bool created = false;
            try
            {
                using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { created = true; output.Write(changed, 0, changed.Length); output.Flush(true); }
                // Replace the directory entry, rather than writing through a possible hard link.
                File.Replace(pending, path, null); created = false;
            }
            finally { if (created && File.Exists(pending)) File.Delete(pending); }
            Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] TEMP_FIXES patched fixture-only immutable mounted-keep recovery address; " + path);
        }
    }

    internal static class ScannerCapture
    {
        private const string Owner = "Fixes.Detours.FixesUnitDetours";
        private const string Scanner = "RedBird.X64.Memory.Scanners.DataScanner";
        private const string Broken = "FE320FBDFB252BAE5988385659235BB0E65C6B3C2226A3BAEDD435857651E433";
        private const string Fixed = "687A9FEF7F0F8F7E0F215DE4CACAC1B6B6F540DAF5B8E94EDBAD46CAE42157A7";
        internal static string Apply(ModuleDefinition module)
        {
            var owner = module.GetType(Owner);
            if (owner == null) throw new InvalidOperationException("Fixes unit owner missing.");
            var install = owner.Methods.SingleOrDefault(m => m.Name == "InstallMountedKeepRecovery");
            if (install == null) return "not-applicable-no-mounted-keep-installer";
            var delayed = owner.NestedTypes.SelectMany(t => t.Methods).Single(m => m.Name.StartsWith("<InstallMountedKeepRecovery>b__", StringComparison.Ordinal) && m.ReturnType.FullName == "System.Void");
            string signature = Fingerprint(install, delayed);
            if (signature == Fixed) return "already-fixed";
            if (signature != Broken) throw new InvalidOperationException("Unknown mounted-keep scanner capture contract: " + signature);
            var getter = delayed.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference m && m.DeclaringType.FullName == Scanner && m.Name == "get_CurrentAddress");
            var load = getter.Previous;
            if (load.OpCode != OpCodes.Ldfld || !(load.Operand is FieldReference)) throw new InvalidOperationException("Capture load changed.");
            var field = ((FieldReference)load.Operand).Resolve();
            if (field.FieldType.FullName != Scanner) throw new InvalidOperationException("Capture type changed.");
            var store = install.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.Resolve() == field);
            var scan = store.Previous;
            if (scan.OpCode != OpCodes.Callvirt || !(scan.Operand is MethodReference mr) || mr.Name != "Scan" || mr.DeclaringType.FullName != Scanner)
                throw new InvalidOperationException("Scanner result producer changed.");
            install.Body.GetILProcessor().InsertBefore(store, Instruction.Create(OpCodes.Callvirt, (MethodReference)getter.Operand));
            field.FieldType = module.TypeSystem.UInt64;
            // Preserve all incoming labels/exception boundaries by keeping the now redundant
            // getter slot as a NOP. No native bytes, emitter semantics or settings change.
            getter.OpCode = OpCodes.Nop; getter.Operand = null;
            if (Fingerprint(install, delayed) != Fixed) throw new InvalidOperationException("Transformed capture contract failed: " + Fingerprint(install, delayed));
            return "patched";
        }

        internal static string Fingerprint(params MethodDefinition[] methods)
        {
            var result = new StringBuilder();
            foreach (var method in methods)
            {
                result.Append(Normalize(method.FullName)).Append('|').Append(method.Body.InitLocals).Append('|').Append(method.Body.MaxStackSize);
                foreach (var variable in method.Body.Variables) result.Append("|v:").Append(Normalize(variable.VariableType.FullName));
                var code = method.Body.Instructions;
                foreach (var instruction in code)
                {
                    if (instruction.OpCode == OpCodes.Nop) continue;
                    result.Append('\n').Append(instruction.OpCode.Code).Append(':');
                    object value = instruction.Operand;
                    if (value is Instruction target) result.Append(Index(code, target));
                    else if (value is Instruction[] targets) result.Append(string.Join(",", targets.Select(t => Index(code, t))));
                    else if (value is VariableDefinition variable) result.Append("v").Append(variable.Index);
                    else if (value is ParameterDefinition parameter) result.Append("p").Append(parameter.Index);
                    else if (value is MemberReference member) result.Append(Normalize(member.FullName));
                    else if (value != null) result.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                foreach (var h in method.Body.ExceptionHandlers)
                    result.Append("|eh:").Append(h.HandlerType).Append('|').Append(h.CatchType == null ? "" : Normalize(h.CatchType.FullName))
                        .Append('|').Append(Index(code, h.TryStart)).Append('|').Append(Index(code, h.TryEnd)).Append('|').Append(Index(code, h.HandlerStart)).Append('|').Append(Index(code, h.HandlerEnd)).Append('|').Append(Index(code, h.FilterStart));
                result.Append("\n--\n");
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(result.ToString()))).Replace("-", "");
        }
        private static int Index(Mono.Collections.Generic.Collection<Instruction> code, Instruction target)
        {
            if (target == null) return -1;
            int index = 0;
            foreach (var instruction in code)
            {
                if (instruction == target) return index;
                if (instruction.OpCode != OpCodes.Nop) index++;
            }
            throw new InvalidOperationException("Branch target outside method.");
        }
        private static string Normalize(string value)
        {
            value = Regex.Replace(value, @"<>c__DisplayClass\d+_(\d+)", "<>closure_$1");
            value = Regex.Replace(value, @"(b__|<>9__)(\d+)_(\d+)", "$1ordinal_$3");
            return value.Replace("::relocateAddress", "::relocate");
        }
    }
}
