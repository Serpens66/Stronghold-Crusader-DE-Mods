// Feature: Keep the selected Extended Lord after a successful Workshop upload.
using System;

namespace BugfixesAndQoL
{
    internal enum WorkshopUploadLordRefresh
    {
        None,
        Aiv,
        LordConfig
    }

    internal sealed class WorkshopUploadLordSelectionPolicy
    {
        internal const int MinimumLordType = 2;
        internal const int MaximumLordType = 29;

        private WorkshopUploadLordRefresh pendingRefresh;
        private int pendingLordType;

        internal WorkshopUploadLordRefresh HandleCommand(
            bool enabled,
            string command,
            WorkshopUploadLordRefresh currentMode,
            int selectedLordType,
            out int restoredLordType)
        {
            restoredLordType = 0;
            if (!enabled)
            {
                Clear();
                return WorkshopUploadLordRefresh.None;
            }

            WorkshopUploadLordRefresh requestedRefresh = GetRefreshCommand(command);
            if (requestedRefresh != WorkshopUploadLordRefresh.None &&
                requestedRefresh == pendingRefresh &&
                requestedRefresh == currentMode)
            {
                restoredLordType = pendingLordType;
                Clear();
                return requestedRefresh;
            }

            if (string.Equals(command, "DoUpload", StringComparison.Ordinal))
            {
                if (currentMode != WorkshopUploadLordRefresh.None &&
                    selectedLordType >= MinimumLordType &&
                    selectedLordType <= MaximumLordType)
                {
                    pendingRefresh = currentMode;
                    pendingLordType = selectedLordType;
                }
                else
                {
                    Clear();
                }

                return WorkshopUploadLordRefresh.None;
            }

            Clear();
            return WorkshopUploadLordRefresh.None;
        }

        internal void CancelPending()
        {
            Clear();
        }

        private static WorkshopUploadLordRefresh GetRefreshCommand(string command)
        {
            if (string.Equals(command, "UploadAIV", StringComparison.Ordinal))
                return WorkshopUploadLordRefresh.Aiv;
            if (string.Equals(command, "UploadLordConfig", StringComparison.Ordinal))
                return WorkshopUploadLordRefresh.LordConfig;
            return WorkshopUploadLordRefresh.None;
        }

        private void Clear()
        {
            pendingRefresh = WorkshopUploadLordRefresh.None;
            pendingLordType = 0;
        }
    }
}
