using BepInEx.Bootstrap;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal static class MoveMoatCompatibility
    {
        internal const string PluginGuid = "MoveMoatTest_Serp";
        private const string BridgeTypeName = "MoveMoatTest.MoveMoatTestPlugin, MoveMoatTest";
        private const string BridgeMethodName = "RegisterImprovedMoatFillingProvider";

        internal static MoatFillHookOwner ResolveOwner(Func<bool> enabledProvider, out string detail)
        {
            if (!Chainloader.PluginInfos.ContainsKey(PluginGuid))
            {
                detail = "MoveMoatTest is not loaded";
                return MoatFillHookOwnershipPolicy.Resolve(false, false, 0);
            }

            Type bridgeType = Type.GetType(BridgeTypeName, throwOnError: false);
            MethodInfo method = bridgeType?.GetMethod(
                BridgeMethodName,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(Func<bool>) },
                modifiers: null);
            if (method == null || method.ReturnType != typeof(int))
            {
                detail = "the loaded MoveMoatTest has no compatible ownership bridge";
                return MoatFillHookOwnershipPolicy.Resolve(true, false, 0);
            }

            try
            {
                int result = (int)method.Invoke(
                    null,
                    new object[] { BugfixesAndQoLPlugin.PluginGuid, enabledProvider });
                MoatFillHookOwner owner = MoatFillHookOwnershipPolicy.Resolve(true, true, result);
                if (owner == MoatFillHookOwner.MoveMoat)
                {
                    detail = "MoveMoatTest owns the shared moat-work hooks";
                    return owner;
                }
                if (owner == MoatFillHookOwner.Standalone)
                {
                    detail = "MoveMoatTest reports that its shared hooks are not installed";
                    return owner;
                }
                detail = $"MoveMoatTest returned unknown bridge status {result}";
                return owner;
            }
            catch (Exception ex)
            {
                detail = "MoveMoatTest bridge invocation failed: " +
                    (ex is TargetInvocationException invocation && invocation.InnerException != null
                        ? invocation.InnerException.Message
                        : ex.Message);
                return MoatFillHookOwner.Conflict;
            }
        }
    }
}
