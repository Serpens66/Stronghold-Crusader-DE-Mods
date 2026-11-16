using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

internal static partial class Program
{
    private static readonly List<string> workshopEffects = new List<string>();
    private static void WorkshopEffect(string name, object[] values)
    { workshopEffects.Add(name + ":" + string.Join(",", values.Select(v => v == null ? "null" : v is string || v is bool || v.GetType().IsEnum || v is int ? v.ToString() : v.GetType().Name)));
      if (name == "UpdateUploadAIVList" || name == "UpdateUploadLordList") workshopEffects.Add("RefreshLord:" + values[0].GetType().GetField("SelectedLordType", All).GetValue(values[0])); }

    // Executes the actual installed Vanilla branch after the production IL edit. Only
    // Translation/Noesis/list rendering are substituted; field stores and branch IL are real.
    private static void WorkshopOriginalBranchTests()
    {
        Type featureType = mod.GetType("BugfixesAndQoL.WorkshopUploadLordSelectionFix", true);
        Type ownerType = featureType.GetMethod("ButtonClickedHook", All).GetParameters()[0].ParameterType;
        Type refreshType = mod.GetType("BugfixesAndQoL.WorkshopUploadLordRefresh", true);
        Type contextType = mod.GetType("BugfixesAndQoL.WorkshopUploadLordSelectionContext", true);
        Type patchType = mod.GetType("BugfixesAndQoL.WorkshopUploadLordSelectionIl", true);
        object feature = Feature("WorkshopUploadLordSelectionFix", Model(false, true, "EnableWorkshopUploadLordSelectionFix", true));
        object context = Activator.CreateInstance(contextType, true);
        featureType.GetField("context", All).SetValue(feature, context);
        featureType.GetField("ready", All).SetValue(feature, true);
        Type safeType = featureType.GetField("selectionHook", All).FieldType;
        object safe = FormatterServices.GetUninitializedObject(safeType);
        safeType.GetField("applied", All).SetValue(safe, 1);
        featureType.GetField("selectionHook", All).SetValue(feature, safe);
        Type resolverType = typeof(Func<,,,>).MakeGenericType(typeof(int), ownerType, refreshType, typeof(int));
        Delegate resolver = Delegate.CreateDelegate(resolverType, feature, featureType.GetMethod("ResolveSelection", All));
        MethodInfo original = ownerType.GetMethod("ButtonClicked", new[] { typeof(string) });
        foreach (var item in new[] { new[] { "UploadAIV", "2", "Aiv", "UpdateUploadAIVList" },
                                    new[] { "UploadLordConfig", "3", "LordConfig", "UpdateUploadLordList" } })
        using (var definition = new DynamicMethodDefinition(original))
        using (var il = new ILContext(definition.Definition))
        {
            il.ReferenceBag = RuntimeILReferenceBag.Instance;
            il.Invoke(body => patchType.GetMethod("Patch", All).Invoke(null, new object[] { body, resolver }));
            var code = il.Body.Instructions;
            var command = code.Single(i => i.OpCode == OpCodes.Ldstr && Equals(i.Operand, item[0]) && i.Previous.OpCode == OpCodes.Ldarg_1);
            var operand = command.Next.Next.Operand;
            var start = operand is ILLabel label ? label.Target : (Instruction)operand;
            int begin = code.IndexOf(start), end = begin;
            while (code[end].OpCode != OpCodes.Ret) end++;
            var branch = code.Skip(begin).Take(end - begin + 1).ToArray();
            il.Body.ExceptionHandlers.Clear(); code.Clear();
            foreach (var instruction in branch) code.Add(instruction);
            // Substitute external boundaries, retaining all original calls and their values.
            foreach (var instruction in branch)
            {
                if (!(instruction.Operand is MethodReference called)) continue;
                string name = called.DeclaringType.FullName;
                if (name != "CrusaderDE.MainViewModel" && name != "CrusaderDE.Translate" &&
                    name != "Noesis.UIElement" && !(name == ownerType.FullName && called.Name == item[3])) continue;
                MethodInfo target = (MethodInfo)called.ResolveReflection();
                Type runtimeOwner = target.DeclaringType;
                Type[] parameterTypes = (target.IsStatic ? Type.EmptyTypes : new[] { runtimeOwner }).Concat(target.GetParameters().Select(p => p.ParameterType)).ToArray();
                var parameters = parameterTypes.Select((t, n) => Expression.Parameter(t, "p" + n)).ToArray();
                var record = Expression.Call(typeof(Program).GetMethod(nameof(WorkshopEffect), All), Expression.Constant(called.Name),
                    Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object)))));
                Expression body = target.ReturnType == typeof(void) ? (Expression)record : Expression.Block(record, Expression.Default(target.ReturnType));
                Type callbackType = Expression.GetDelegateType(parameterTypes.Concat(new[] { target.ReturnType }).ToArray());
                Delegate callback = Expression.Lambda(callbackType, body, parameters).Compile();
                var cursor = new ILCursor(il); cursor.Goto(instruction); cursor.Remove();
                typeof(ILCursor).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Single(m => m.Name == "EmitDelegate" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                    .MakeGenericMethod(callbackType).Invoke(cursor, new object[] { callback });
            }
            MethodInfo execute = definition.Generate();
            object owner = FormatterServices.GetUninitializedObject(ownerType);
            object mode = Enum.Parse(refreshType, item[2]);
            workshopEffects.Clear();
            using ((IDisposable)contextType.GetMethod("Enter", All).Invoke(context, new[] { owner, mode, (object)17 }))
                execute.Invoke(null, new[] { owner, (object)item[0] });
            Check((int)ownerType.GetField("SelectedLordType", All).GetValue(owner) == 17 &&
                Convert.ToInt32(ownerType.GetField("uploadMode", All).GetValue(owner)) == int.Parse(item[1]), "patched original retains selected Lord and mode: " + item[0]);
            Check(workshopEffects.Count(s => s.StartsWith(item[3] + ":")) == 1 && workshopEffects.Contains("RefreshLord:17") &&
                workshopEffects.Any(s => s.StartsWith("set_Show_EditorWorkshop_Uploader:") && s.EndsWith(",False")) &&
                workshopEffects.Any(s => s.StartsWith("set_Show_EditorWorkshop_Requester:") && s.EndsWith(",True")) &&
                workshopEffects.Any(s => s.StartsWith("set_EditorUploadInfo:")) && workshopEffects.Any(s => s.StartsWith("set_Visibility:")),
                "actual original branch preserves all navigation effects and one list refresh: " + item[0]);
            contextType.GetMethod("Handle", All).Invoke(context, new[] { owner, (object)true, "DoUpload", mode, (object)17, (object)0 });
            featureType.GetMethod("OnSelectionRebuilt", All).Invoke(feature, new[] { safe });
            object[] pending = { owner, true, item[0], mode, 1, 0 };
            object afterRebuild = contextType.GetMethod("Handle", All).Invoke(context, pending);
            Check(Convert.ToInt32(afterRebuild) != 0 && (int)pending[5] == 17, "compatible rebuild retains the pending upload");
            contextType.GetMethod("Handle", All).Invoke(context, new[] { owner, (object)true, "DoUpload", mode, (object)17, (object)0 });
            safeType.GetField("applied", All).SetValue(safe, 0);
            featureType.GetMethod("OnSelectionRebuilt", All).Invoke(feature, new[] { safe });
            safeType.GetField("applied", All).SetValue(safe, 1);
            featureType.GetMethod("OnSelectionRebuilt", All).Invoke(feature, new[] { safe });
            pending = new object[] { owner, true, item[0], mode, 1, 0 };
            afterRebuild = contextType.GetMethod("Handle", All).Invoke(context, pending);
            Check(Convert.ToInt32(afterRebuild) == 0, "hook loss and recovery clears pending upload without an intervening command");
            execute.Invoke(null, new[] { owner, (object)item[0] });
            Check((int)ownerType.GetField("SelectedLordType", All).GetValue(owner) == 1, "later ordinary navigation retains Vanilla Rat default");
        }
    }
}
