using CrusaderDE;
using System;
using System.Linq;
using System.Reflection;

namespace CastlePlanner.AIVPlacement
{
    internal static class BugfixAivStatusBridge
    {
        private const string AssemblyName = "BugfixesAndQoL";
        private const string ApiTypeName = "BugfixesAndQoL.AivCandidateStatusApi";
        private static bool resolved;
        private static MethodInfo setStatus;
        private static MethodInfo clearStatuses;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return setStatus != null && clearStatuses != null;
            }
        }

        public static bool IsSelectionListVisible(FRONT_Multiplayer_AISettings view) =>
            IsAvailable &&
            view?.FindName("BugfixesAndQoLAivSelectionListHost") is Noesis.FrameworkElement host &&
            host.Visibility == Noesis.Visibility.Visible;

        public static bool TrySet(
            FRONT_Multiplayer.MPAIVInfo info,
            ulong checksum,
            int neutralStatus,
            string toolTip)
        {
            Resolve();
            if (setStatus == null || info == null)
                return false;
            setStatus.Invoke(null, new object[] { info, checksum, neutralStatus, toolTip ?? string.Empty });
            return true;
        }

        public static void Clear(FRONT_Multiplayer.MPAIVInfo info)
        {
            Resolve();
            clearStatuses?.Invoke(null, new object[] { info });
        }

        private static void Resolve()
        {
            if (resolved)
                return;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                string.Equals(candidate.GetName().Name, AssemblyName, StringComparison.Ordinal));
            Type api = assembly?.GetType(ApiTypeName, false);
            setStatus = api?.GetMethod(
                "SetStatus",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(FRONT_Multiplayer.MPAIVInfo), typeof(ulong), typeof(int), typeof(string) },
                null);
            clearStatuses = api?.GetMethod(
                "ClearStatuses",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(FRONT_Multiplayer.MPAIVInfo) },
                null);
            resolved = setStatus != null && clearStatuses != null;
        }
    }
}
