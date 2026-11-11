using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using Steamworks;

namespace BugfixesAndQoL
{
    // Fixes' scalar setting controls local notifications. It has no per-player storage,
    // so keep Extender persistence and omit only this property from the network paths.
    internal static class FixesSuppressMessagesSync
    {
        private const string ModelName = "Fixes.UI.LobbySettingsViewModel";
        private const string PropertyName = "SuppressMessages";
        private const string RoutingType = "SHCDESE.API.Components.ModManager.LobbyModSettingsRouting";
        private static readonly APIShared.Patching.SafeIlHook[] Hooks = new APIShared.Patching.SafeIlHook[3];
        private static PropertyInfo localProperty;
        private static bool attempted;

        internal static void Initialize(ManualLogSource log, Assembly fixesAssembly)
        {
            if (attempted) return;
            attempted = true;
            try
            {
                PropertyInfo property = FindLocalProperty(fixesAssembly);
                if (property == null) return;
                var manager = typeof(GameXAMLManagerAPI);
                var change = FindChangeHandler(manager);
                var join = RequireMethod(manager, nameof(GameXAMLManagerAPI.SyncSettingsToNewPlayer), BindingFlags.Public | BindingFlags.Instance,
                    typeof(Platform_Multiplayer.MPLobbyMember));
                var receive = RequireMethod(manager, "ApplyPerPlayerUpdate", BindingFlags.NonPublic | BindingFlags.Static,
                    typeof(object), typeof(PropertyInfo), typeof(LobbyModSettingSyncPacket), typeof(object), typeof(CSteamID?));

                // Probe all three bodies before publishing a hook. The APIShared-owned
                // BroadcastSettingChange and ReceiveSettingsUpdate entries remain intact.
                ILContext.Manipulator changePatch = il => PatchSend(il, "BroadcastSettingChange");
                ILContext.Manipulator joinPatch = il => PatchSend(il, "SendPacketToSteamId");
                Probe(change, changePatch);
                Probe(join, joinPatch);
                Probe(receive, PatchReceive);
                Install(0, change, changePatch, log);
                Install(1, join, joinPatch, log);
                Install(2, receive, PatchReceive, log);
                // Until all hooks are installed their callbacks pass through the original policy.
                Volatile.Write(ref localProperty, property);
                Shared.DebugLogHelper.LogInfo(log, "FIXES_SUPPRESS_MESSAGES_LOCAL: local storage retained and lobby sync disabled for SuppressMessages.");
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/FixesSuppressMessagesSync/Initialize",
                    "FIXES_SUPPRESS_MESSAGES_SYNC_INACTIVE: " + error);
            }
        }

        private static PropertyInfo FindLocalProperty(Assembly fixesAssembly)
        {
            var model = fixesAssembly.GetType(ModelName, false);
            var property = model?.GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (property == null || property.PropertyType != typeof(bool) || property.GetGetMethod() == null || property.GetSetMethod() == null ||
                !property.IsDefined(typeof(SyncPerPlayerAttribute), false) || property.IsDefined(typeof(SyncHostOnlyAttribute), false) ||
                model.GetProperty(PropertyName + "Data", BindingFlags.Public | BindingFlags.Instance) != null)
                return null;
            return property;
        }

        private static MethodInfo FindChangeHandler(Type manager)
        {
            const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var registration = manager.GetMethods(methods).Single(method => method.Name == "RegisterLobbyModSettingsCore");
            using (var definition = new DynamicMethodDefinition(registration))
            {
                var handler = definition.Definition.Body.Instructions
                    .Where(instruction => instruction.OpCode == OpCodes.Ldftn)
                    .Select(instruction => instruction.Operand as MethodReference)
                    .Single(method => method?.Parameters.Count == 2 &&
                        method.Parameters[0].ParameterType.FullName == typeof(object).FullName &&
                        method.Parameters[1].ParameterType.FullName == typeof(PropertyChangedEventArgs).FullName);
                var owner = manager.Assembly.GetType(handler.DeclaringType.FullName.Replace('/', '+'), true);
                return RequireMethod(owner, handler.Name, methods, typeof(object), typeof(PropertyChangedEventArgs));
            }
        }

        private static MethodInfo RequireMethod(Type owner, string name, BindingFlags flags, params Type[] parameters)
        {
            var method = owner.GetMethod(name, flags, null, parameters, null);
            if (method == null || method.ReturnType != typeof(void) || method.GetMethodBody() == null)
                throw new MissingMethodException(owner.FullName, name);
            return method;
        }

        private static void Probe(MethodInfo method, ILContext.Manipulator patch)
        {
            using (var definition = new DynamicMethodDefinition(method))
            using (var context = new ILContext(definition.Definition))
            {
                context.ReferenceBag = RuntimeILReferenceBag.Instance;
                context.Invoke(patch);
                definition.Generate();
            }
        }

        private static void Install(int index, MethodInfo method, ILContext.Manipulator patch, ManualLogSource log)
        {
            // Installed hooks stay rooted and logically dormant until all three are installed.
            if (!APIShared.Patching.SafeIlHook.TryInstall(BugfixesAndQoLPlugin.PluginGuid, "BugfixesAndQoL.FixesSuppressMessagesSync." + method.Name,
                method, patch, log, out var installed, out string failure))
                throw new InvalidOperationException(failure);
            Hooks[index] = installed;
        }

        private static void PatchSend(ILContext il, string sendMethod)
        {
            var instructions = il.Body.Instructions;
            var routing = instructions.Single(instruction => instruction.OpCode == OpCodes.Call &&
                instruction.Operand is MethodReference method && method.DeclaringType.FullName == RoutingType &&
                method.Name == "IsSynced" && !method.HasThis && method.ReturnType.FullName == typeof(bool).FullName &&
                method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == typeof(PropertyInfo).FullName);
            var branch = routing.Next;
            int fallThrough = instructions.IndexOf(branch) + 1;
            int skipTo = branch?.Operand is ILLabel skip ? instructions.IndexOf(skip.Target) : -1;
            bool onChange = sendMethod == "BroadcastSettingChange";
            string sender = onChange ? typeof(GameXAMLManagerAPI).FullName : typeof(GameNetworkAPI).FullName;
            if ((branch?.OpCode != OpCodes.Brfalse && branch?.OpCode != OpCodes.Brfalse_S) || skipTo <= fallThrough ||
                !instructions.Skip(fallThrough).Take(skipTo - fallThrough).Any(instruction =>
                    instruction.Operand is MethodReference method && method.Name == sendMethod && method.DeclaringType.FullName == sender) ||
                (onChange && !instructions.Skip(skipTo).Any(instruction => instruction.Operand is MethodReference method &&
                    method.Name == "IsPersisted" && method.DeclaringType.FullName == RoutingType)))
                throw new InvalidOperationException("Extender lobby setting send branch changed: " + il.Method.Name);

            var cursor = new ILCursor(il).Goto(routing, MoveType.AfterLabel);
            cursor.Emit(OpCodes.Dup);
            cursor.Goto(routing, MoveType.After);
            cursor.EmitDelegate<Func<PropertyInfo, bool, bool>>(AllowSend);
        }

        private static void PatchReceive(ILContext il)
        {
            var cursor = new ILCursor(il);
            var original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_1); // PropertyInfo in the static ApplyPerPlayerUpdate method.
            cursor.EmitDelegate<Func<PropertyInfo, bool>>(IsLocal);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        }

        private static bool AllowSend(PropertyInfo property, bool synced) => synced && !IsLocal(property);

        private static bool IsLocal(PropertyInfo property)
        {
            var active = Volatile.Read(ref localProperty);
            return active != null && property?.DeclaringType == active.DeclaringType && property.Name == active.Name;
        }
    }
}
