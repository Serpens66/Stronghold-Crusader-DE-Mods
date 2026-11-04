using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL.PitchStairs
{
    internal sealed unsafe partial class PitchStairsRuntime
    {
        private delegate EngineInterface.LoadMapReturnData LoadMap(int campaign, string file, bool dummy, bool save, int trail, int mission, bool classic);
        private delegate EngineInterface.LoadMapReturnData LoadSave(string file);
        private delegate EngineInterface.LoadMapReturnData LoadEditor(string file, bool editor);
        private delegate EngineInterface.LoadMapReturnData NewEditor(int size, int type, bool siege, bool multiplayer);

        // Permanent, like the native hooks. Even a partially installed set only forwards loads
        // while ready=false; no published callback is disposed during plugin teardown.
        private readonly List<Hook> loadBoundaries = new List<Hook>();
        private LoadMap loadMapOriginal;
        private LoadSave loadSaveOriginal;
        private LoadEditor loadEditorOriginal;
        private NewEditor newEditorOriginal;

        internal void InstallLoadBoundaries()
        {
            loadMapOriginal = PrepareLoadBoundary("loadMap", (LoadMap)((campaign, file, dummy, save, trail, mission, classic) =>
                activation.RunLoad(() => loadMapOriginal(campaign, file, dummy, save, trail, mission, classic))));
            loadSaveOriginal = PrepareLoadBoundary(nameof(EngineInterface.LoadSaveFile), (LoadSave)(file =>
                activation.RunLoad(() => loadSaveOriginal(file))));
            loadEditorOriginal = PrepareLoadBoundary(nameof(EngineInterface.LoadMapFile), (LoadEditor)((file, editor) =>
                activation.RunLoad(() => loadEditorOriginal(file, editor))));
            newEditorOriginal = PrepareLoadBoundary(nameof(EngineInterface.newMapEditor), (NewEditor)((size, type, siege, multiplayer) =>
                activation.RunLoad(() => newEditorOriginal(size, type, siege, multiplayer))));
            // Every trampoline is assigned before a detour can execute. Native publication
            // follows only after all four boundaries are available.
            foreach (Hook hook in loadBoundaries) hook.Apply();
        }

        private T PrepareLoadBoundary<T>(string name, T callback) where T : Delegate
        {
            MethodInfo invoke = typeof(T).GetMethod("Invoke");
            ParameterInfo[] signature = invoke.GetParameters();
            var parameters = new Type[signature.Length];
            for (int i = 0; i < signature.Length; i++) parameters[i] = signature[i].ParameterType;
            MethodInfo method = typeof(EngineInterface).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, parameters, null);
            if (method == null || method.ReturnType != invoke.ReturnType)
                throw new MissingMethodException(typeof(EngineInterface).FullName, name);
            var hook = new Hook(method, callback, new HookConfig
            {
                ManualApply = true,
                ID = "BugfixesAndQoL.PitchStairs.LoadBoundary." + name
            });
            loadBoundaries.Add(hook);
            return hook.GenerateTrampoline<T>();
        }
    }
}
