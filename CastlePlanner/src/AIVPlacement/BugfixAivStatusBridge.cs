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
        private static MethodInfo clearStatuses;
        private static MethodInfo replaceStatuses;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return clearStatuses != null && replaceStatuses != null;
            }
        }

        public static bool IsSelectionListVisible(FRONT_Multiplayer_AISettings view) =>
            IsAvailable &&
            view?.FindName("BugfixesAndQoLAivSelectionListHost") is Noesis.FrameworkElement host &&
            host.Visibility == Noesis.Visibility.Visible;

        public static void Clear(FRONT_Multiplayer.MPAIVInfo info)
        {
            Resolve();
            clearStatuses?.Invoke(null, new object[] { info });
        }

        public static bool TryReplace(
            FRONT_Multiplayer.MPAIVInfo info,
            ulong[] checksums,
            int[] statuses,
            string[] toolTips)
        {
            Resolve();
            if (replaceStatuses == null || info == null)
                return false;
            replaceStatuses.Invoke(null, new object[] { info, checksums, statuses, toolTips });
            return true;
        }

        private static void Resolve()
        {
            if (resolved)
                return;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                string.Equals(candidate.GetName().Name, AssemblyName, StringComparison.Ordinal));
            Type api = assembly?.GetType(ApiTypeName, false);
            clearStatuses = api?.GetMethod(
                "ClearStatuses",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(FRONT_Multiplayer.MPAIVInfo) },
                null);
            replaceStatuses = api?.GetMethod(
                "ReplaceStatuses",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(FRONT_Multiplayer.MPAIVInfo), typeof(ulong[]), typeof(int[]), typeof(string[]) },
                null);
            resolved = clearStatuses != null && replaceStatuses != null;
        }
    }
}
