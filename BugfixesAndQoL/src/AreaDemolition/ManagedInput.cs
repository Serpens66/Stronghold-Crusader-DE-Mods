using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BugfixesAndQoL.AreaDemolition
{
    internal static class ManagedInput
    {
        private static readonly Harmony harmony=new Harmony(BugfixesAndQoLPlugin.PluginGuid+".AreaDemolition.Input");
        private static Runtime runtime;
        private static FieldInfo selectionBoxField;
        private static bool installed, escapeOwned;
        internal static void Install(Runtime target,ManualLogSource log)
        {
            if(installed) return;
            var setter=typeof(EditorDirector).GetMethod(nameof(EditorDirector.updateLeftMouseStateForEngine),new[]{typeof(int)});
            var editor=typeof(EditorDirector).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            var keyboard=typeof(KeyManager).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            selectionBoxField=typeof(EditorDirector).GetField("troopSelectionBoxOn",BindingFlags.Instance|BindingFlags.NonPublic);
            if(setter==null || editor==null || keyboard==null || typeof(KeyManager).GetMethod(nameof(KeyManager.ignoreEscape),Type.EmptyTypes)==null)
                throw new MissingMethodException("Validated primary-input and Escape boundaries are required.");
            if(selectionBoxField==null || selectionBoxField.IsStatic || selectionBoxField.FieldType!=typeof(bool))
                throw new MissingFieldException("EditorDirector.troopSelectionBoxOn");
            // Publish only after all permanent patches have been installed; inert patches preserve the original.
            harmony.Patch(setter,prefix:new HarmonyMethod(typeof(ManagedInput),nameof(PrimaryPrefix)){priority=Priority.Last});
            harmony.Patch(editor,postfix:new HarmonyMethod(typeof(ManagedInput),nameof(EditorPostfix)){priority=Priority.Last});
            harmony.Patch(keyboard,prefix:new HarmonyMethod(typeof(ManagedInput),nameof(KeyPrefix)){priority=Priority.First});
            runtime=target; installed=true;
        }
        private static bool PrimaryPrefix(EditorDirector __instance,int __0)
        { return runtime==null || !runtime.HandlePrimary(__instance,__0); }
        private static void EditorPostfix(EditorDirector __instance) => runtime?.AfterInput(__instance);
        internal static bool TryGetSelectionOn(EditorDirector director,out bool selectionOn)
        {
            selectionOn=false;
            if(director==null || selectionBoxField==null || TroopSelector.instance==null)return false;
            selectionOn=TroopSelector.instance.selection_on || (bool)selectionBoxField.GetValue(director);
            return true;
        }
        private static void KeyPrefix(KeyManager __instance)
        {
            if(runtime==null) return;
            bool held=Input.GetKey(KeyCode.Escape);
            if(!held) {escapeOwned=false; return;}
            if(Input.GetKeyDown(KeyCode.Escape) && runtime.CancelWithEscape()) escapeOwned=true;
            // Uses Vanilla's short suppression window and leaves original key-state processing running.
            if(escapeOwned) __instance.ignoreEscape();
        }
    }
}
