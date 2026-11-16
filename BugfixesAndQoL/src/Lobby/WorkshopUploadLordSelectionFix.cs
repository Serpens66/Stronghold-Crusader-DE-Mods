// Preserve the selected Extended Lord without replacing Vanilla's upload navigation.
using APIShared.Patching;
using BepInEx.Logging;
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class WorkshopUploadLordSelectionFix
    {
        private static readonly List<WorkshopUploadLordSelectionFix> Published = new List<WorkshopUploadLordSelectionFix>();
        private delegate void EditorSetupButtonDelegate(FRONT_EditorSetup self, string command);
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ManualLogSource log;
        private readonly WorkshopUploadLordSelectionContext context = new WorkshopUploadLordSelectionContext();
        private readonly FieldInfo uploadModeField, selectedLordTypeField;
        private readonly SafeIlHook selectionHook;
        private readonly Hook buttonHook;
        private readonly EditorSetupButtonDelegate buttonOriginal;
        private volatile bool ready;

        internal WorkshopUploadLordSelectionFix(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            this.log = log;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            MethodInfo method = typeof(FRONT_EditorSetup).GetMethod("ButtonClicked", new[] { typeof(string) });
            uploadModeField = FindRequiredField("uploadMode");
            selectedLordTypeField = FindRequiredField("SelectedLordType");
            if (method == null || method.ReturnType != typeof(void) || selectedLordTypeField.FieldType != typeof(int) ||
                !uploadModeField.FieldType.IsEnum ||
                Convert.ToInt32(Enum.Parse(uploadModeField.FieldType, "AIV")) != 2 ||
                Convert.ToInt32(Enum.Parse(uploadModeField.FieldType, "LordConfig")) != 3)
                throw new InvalidOperationException("Vanilla Workshop selection contracts changed.");
            // Root before the first hook can be published, including a partially successful
            // constructor. Such a cohort stays inert and installed for the process lifetime.
            lock (Published) Published.Add(this);
            if (!SafeIlHook.TryInstall(BugfixesAndQoLPlugin.PluginGuid, "BugfixesAndQoL.WorkshopLordSelection", method,
                il => WorkshopUploadLordSelectionIl.Patch(il, ResolveSelection), log, out selectionHook, out string failure))
                throw new InvalidOperationException("Workshop selection unavailable: " + failure);
            selectionHook.Rebuilt += OnSelectionRebuilt;
            Hook candidate = null;
            bool publicationAttempted = false;
            try
            {
                candidate = new Hook(method, (EditorSetupButtonDelegate)ButtonClickedHook,
                    new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.WorkshopLordSelection" });
                buttonHook = candidate;
                buttonOriginal = candidate.GenerateTrampoline<EditorSetupButtonDelegate>();
                publicationAttempted = true;
                candidate.Apply();
                settings.SettingChanged += OnSettingChanged;
                ready = true;
            }
            catch
            {
                ready = false;
                if (!publicationAttempted) candidate?.Dispose();
                throw;
            }
        }

        // Loss and recovery may occur without an intervening UI command. A compatible
        // rebuild retains the contract and must not discard a still-pending upload.
        private void OnSelectionRebuilt(SafeIlHook hook)
        {
            if (!hook.IsApplied) context.CancelAll();
        }

        private void OnSettingChanged(string property)
        {
            if (!settings.EnableClientFeatures || !settings.EnableWorkshopUploadLordSelectionFix)
                context.CancelAll();
        }

        private int ResolveSelection(int vanilla, FRONT_EditorSetup self, WorkshopUploadLordRefresh refresh) =>
            ready && selectionHook.IsApplied && settings.EnableClientFeatures && settings.EnableWorkshopUploadLordSelectionFix
                ? context.Resolve(self, refresh, vanilla) : vanilla;

        private void ButtonClickedHook(FRONT_EditorSetup self, string command)
        {
            WorkshopUploadLordRefresh refresh = WorkshopUploadLordRefresh.None;
            int lord = 0;
            bool isDoUpload = string.Equals(command, "DoUpload", StringComparison.Ordinal);
            if (!settings.EnableClientFeatures || !settings.EnableWorkshopUploadLordSelectionFix)
                context.CancelAll();
            else if (!ready || !selectionHook.IsApplied)
                context.CancelAll();
            else if (ready && selectionHook.IsApplied && self != null)
            {
                try
                {
                    int mode = Convert.ToInt32(uploadModeField.GetValue(self));
                    refresh = context.Handle(self, true, command,
                        mode == 2 ? WorkshopUploadLordRefresh.Aiv : mode == 3 ? WorkshopUploadLordRefresh.LordConfig : WorkshopUploadLordRefresh.None,
                        (int)selectedLordTypeField.GetValue(self), out lord);
                }
                catch (Exception error)
                {
                    ready = false;
                    context.CancelAll();
                    try { Shared.DebugLogHelper.LogErrorLimited(log, "WorkshopLordSelection/Capture",
                        "Workshop selection disabled; Vanilla navigation retained: " + error); }
                    catch { } // A logger failure must not suppress the original UI operation.
                }
            }
            // Even inactive/unrelated nested calls shadow the outer restore. An original failure
            // propagates once; retrying it could repeat Steam operations and UI side effects.
            using (context.Enter(self, refresh, lord))
            {
                try { buttonOriginal(self, command); }
                catch { if (isDoUpload && self != null) context.Cancel(self); throw; }
                if (isDoUpload && FRONT_EditorSetup.canCloseWorkshop && self != null) context.Cancel(self);
            }
        }

        private static FieldInfo FindRequiredField(string name) => typeof(FRONT_EditorSetup).GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(FRONT_EditorSetup).FullName, name);
    }

    internal static class WorkshopUploadLordSelectionIl
    {
        internal static void Patch(ILContext il, Func<int, FRONT_EditorSetup, WorkshopUploadLordRefresh, int> resolve)
        {
            // Validate the complete pair before mutating either branch. Other assignments,
            // including Lord_XX and the constructor, are outside this transformation.
            Instruction aiv = FindStore(il, "UploadAIV", 2, "UpdateUploadAIVList");
            Instruction lord = FindStore(il, "UploadLordConfig", 3, "UpdateUploadLordList");
            foreach (var pair in new[] { Tuple.Create(aiv, WorkshopUploadLordRefresh.Aiv), Tuple.Create(lord, WorkshopUploadLordRefresh.LordConfig) })
            {
                var cursor = new ILCursor(il); cursor.Goto(pair.Item1, MoveType.Before);
                cursor.MoveAfterLabels();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldc_I4, (int)pair.Item2);
                cursor.EmitDelegate(resolve);
            }
        }

        private static Instruction FindStore(ILContext il, string command, int mode, string refresh)
        {
            var code = il.Body.Instructions;
            var matches = code.Where(i => i.OpCode == OpCodes.Ldstr && Equals(i.Operand, command) &&
                i.Previous?.OpCode == OpCodes.Ldarg_1 && IsCall(i.Next, "System.String", "op_Equality") &&
                (i.Next.Next?.OpCode == OpCodes.Brtrue || i.Next.Next?.OpCode == OpCodes.Brtrue_S)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Ambiguous Workshop command: " + command);
            var branch = matches[0].Next.Next;
            Instruction start = Target(branch.Operand);
            int begin = code.IndexOf(start), end = begin;
            if (begin < 0) throw new InvalidOperationException("Workshop branch missing.");
            while (end < code.Count && code[end].OpCode != OpCodes.Ret) end++;
            var stores = code.Skip(begin).Take(end - begin).Where(i => IsField(i, OpCodes.Stfld, "SelectedLordType")).ToArray();
            if (end == code.Count || stores.Length != 1) throw new InvalidOperationException("Workshop store is not unique.");
            Instruction store = stores[0]; int index = code.IndexOf(store);
            if (index < begin + 5 || index + 3 != end || code[index - 5].OpCode != OpCodes.Ldarg_0 ||
                !code[index - 4].MatchLdcI4(mode) || !IsField(code[index - 3], OpCodes.Stfld, "uploadMode") ||
                code[index - 2].OpCode != OpCodes.Ldarg_0 || !code[index - 1].MatchLdcI4(1) ||
                code[index + 1].OpCode != OpCodes.Ldarg_0 || !IsCall(code[index + 2], typeof(FRONT_EditorSetup).FullName, refresh))
                throw new InvalidOperationException("Workshop selection/refresh sequence changed.");
            // No alternate control-flow entry may arrive with another stack or bypass the scope transform.
            foreach (Instruction instruction in code)
            {
                var targets = instruction.Operand is Instruction[] array ? array :
                    instruction.Operand is ILLabel[] labels ? labels.Select(label => label?.Target).ToArray() :
                    new[] { Target(instruction.Operand) };
                foreach (Instruction target in targets)
                    if (target != null && code.IndexOf(target) > begin && code.IndexOf(target) <= end)
                        throw new InvalidOperationException("Unexpected entry into Workshop refresh branch.");
            }
            return store;
        }
        private static bool IsField(Instruction i, OpCode opcode, string name) => i != null && i.OpCode == opcode &&
            i.Operand is FieldReference f && f.DeclaringType.FullName == typeof(FRONT_EditorSetup).FullName && f.Name == name;
        private static bool IsCall(Instruction i, string owner, string name) => i != null &&
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) && i.Operand is MethodReference m &&
            m.DeclaringType.FullName == owner && m.Name == name;
        private static Instruction Target(object operand) => operand is ILLabel label ? label.Target : operand as Instruction;
    }
}
