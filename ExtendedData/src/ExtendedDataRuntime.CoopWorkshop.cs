using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrusaderDE;
using ExtendedData.Core;
using R3;
using SHCDESE.EventAPI;
using Steamworks;

namespace ExtendedData
{
    internal sealed partial class ExtendedDataRuntime
    {
        private const EItemState PendingWorkshopFlags = EItemState.k_EItemStateNeedsUpdate |
            EItemState.k_EItemStateDownloading | EItemState.k_EItemStateDownloadPending;
        private readonly Dictionary<ulong, CoopWorkshopInstallation> coopWorkshopSources = new Dictionary<ulong, CoopWorkshopInstallation>();
        private Callback<ItemInstalled_t> coopWorkshopInstalled;
        private CoopTrailPackageConflictException coopPackageConflict;
        private ulong workshopNoticeLobby;
        private string workshopNoticePackage;
        private string lastWorkshopUpdateNotice;
        private string lastWorkshopUpdateLog;
        private bool workshopConflictShown;

        private void InitializeCoopWorkshopDiagnostics()
        {
            subscriptions.Add(SteamworksR3EventHooks.OnSteamworksInitialized.Observable.Subscribe(args =>
            {
                if (args.Phase == EventHookPhase.Post)
                    Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(RegisterCoopWorkshopCallback);
            }));
            RegisterCoopWorkshopCallback();
        }

        private void RegisterCoopWorkshopCallback()
        {
            if (!launchReadinessActive || coopWorkshopInstalled != null || !Shared.WorkshopContentPaths.IsSteamworksReady()) return;
            try
            {
                coopWorkshopInstalled = Callback<ItemInstalled_t>.Create(OnCoopWorkshopItemInstalled);
                // Remember pre-existing subscriptions even before the Coop catalogue is first opened.
                // The callback can then record an early update without reading or rediscovering any Trail.
                var items = GetCoopWorkshopSubscriptions();
                if (items == null) return;
                foreach (PublishedFileId_t id in items)
                    if ((ReadWorkshopState(id) & EItemState.k_EItemStateInstalled) != 0 &&
                        !coopWorkshopSources.ContainsKey(id.m_PublishedFileId))
                        coopWorkshopSources.Add(id.m_PublishedFileId, new CoopWorkshopInstallation());
            }
            catch (Exception exception) { LogWarning("Could not observe Coop Trail Workshop installations: " + exception.Message); }
        }

        private void OnCoopWorkshopItemInstalled(ItemInstalled_t notification)
        {
            // Steam can dispatch for other apps. Defer even on the Unity thread to avoid re-entering its callback pump.
            if (notification.m_unAppID != SteamManager.AppID) return;
            ulong id = notification.m_nPublishedFileId.m_PublishedFileId;
            ulong manifest = notification.m_unManifestID;
            Shared.UnityMainThreadDispatch.TryEnqueue(() =>
            {
                if (!launchReadinessActive || !CoopWorkshopInstallation.TryObserveCompletion(coopWorkshopSources, id, manifest)) return;
                if (coopWorkshopSources[id].PackageIds.Contains(settings.ActiveCoopPackageId))
                    ReportCoopWorkshopDiagnostics(GetExistingMainViewModel()?.FRONTMultiplayer);
            });
        }

        private void RememberCoopWorkshopSources(IEnumerable<string> roots)
        {
            if (!Shared.UnityMainThreadDispatch.IsMainThread || !Shared.WorkshopContentPaths.IsSteamworksReady()) return;
            try
            {
                var workshopRoots = new HashSet<string>(roots.Select(NormalizeWorkshopRoot), StringComparer.OrdinalIgnoreCase);
                var packagesByRoot = packageOverviews.ToLookup(item => NormalizeWorkshopRoot(Path.GetDirectoryName(item.RootPath)),
                    StringComparer.OrdinalIgnoreCase);
                workshopRoots.IntersectWith(packagesByRoot.Select(group => group.Key));
                if (workshopRoots.Count == 0 && coopWorkshopSources.Count == 0) return;
                var ids = GetCoopWorkshopSubscriptions();
                if (ids == null) return;

                var subscribed = new HashSet<ulong>(ids.Select(id => id.m_PublishedFileId));
                foreach (ulong removed in coopWorkshopSources.Keys.Where(id => !subscribed.Contains(id)).ToArray())
                    coopWorkshopSources.Remove(removed);
                if (workshopRoots.Count == 0) return;
                foreach (PublishedFileId_t id in ids)
                {
                    if ((ReadWorkshopState(id) & EItemState.k_EItemStateInstalled) == 0 ||
                        !SteamUGC.GetItemInstallInfo(id, out _, out string folder, 4096, out uint installedAt) ||
                        string.IsNullOrWhiteSpace(folder)) continue;
                    string root = NormalizeWorkshopRoot(folder);
                    if (!workshopRoots.Contains(root)) continue;
                    var packageIds = new HashSet<string>(packagesByRoot[root].Select(item => item.Manifest.PackageId),
                        StringComparer.OrdinalIgnoreCase);
                    if (!coopWorkshopSources.TryGetValue(id.m_PublishedFileId, out var source) ||
                        (source.Root != null && !string.Equals(source.Root, root, StringComparison.OrdinalIgnoreCase)))
                        coopWorkshopSources[id.m_PublishedFileId] = source = new CoopWorkshopInstallation();
                    source.Root = root;
                    source.PackageIds = packageIds;
                    source.InstalledAt = installedAt;
                }
            }
            catch (Exception exception) { LogWarning("Could not associate Coop Trails with Workshop items: " + exception.Message); }
        }

        private void ReportCoopWorkshopDiagnostics(FRONT_Multiplayer lobby)
        {
            if (!Shared.UnityMainThreadDispatch.IsMainThread || !launchReadinessActive || readinessInMission) return;
            try
            {
                ulong lobbyId = HasActiveCoopPackage() && lobby?.currentLobby?.coopTrailGame == true && IsCurrentSteamLobby(lobby)
                    ? lobby.currentLobby.id.m_SteamID : 0;
                string packageId = lobbyId == 0 ? null : settings.ActiveCoopPackageId;
                if (lobbyId != workshopNoticeLobby || !string.Equals(packageId, workshopNoticePackage, StringComparison.OrdinalIgnoreCase))
                {
                    workshopNoticeLobby = lobbyId;
                    workshopNoticePackage = packageId;
                    lastWorkshopUpdateNotice = null;
                    lastWorkshopUpdateLog = null;
                    workshopConflictShown = false;
                }
                if (lobbyId == 0 || (!lobby.currentLobby.isHost && !HasCoherentHostPackageDescriptor())) return;

                bool mixedConflict = coopPackageConflict != null &&
                    coopPackageConflict.Candidates.All(item => string.Equals(item.Manifest.PackageId, packageId, StringComparison.OrdinalIgnoreCase)) &&
                    coopPackageConflict.Candidates.Any(item => IsLocalCoopSource(item.RootPath)) &&
                    coopPackageConflict.Candidates.Any(item => !IsLocalCoopSource(item.RootPath));
                if (!mixedConflict) workshopConflictShown = false;
                else if (!workshopConflictShown)
                {
                    workshopConflictShown = ShowCoopWorkshopNotice(lobbyId, "ExtendedData.CoopWorkshopLocalConflict");
                }

                if (!CoopWorkshopInstallation.TryMatch(coopWorkshopSources, packageId, out var match) ||
                    !Shared.WorkshopContentPaths.IsSteamworksReady())
                {
                    lastWorkshopUpdateNotice = lastWorkshopUpdateLog = null;
                    return;
                }
                var state = ReadWorkshopState(new PublishedFileId_t(match.Key));
                var notice = match.Value.Notice((state & EItemState.k_EItemStateSubscribed) != 0,
                    (state & EItemState.k_EItemStateInstalled) != 0, (state & PendingWorkshopFlags) != 0);
                if (notice == CoopWorkshopNotice.None)
                {
                    lastWorkshopUpdateNotice = lastWorkshopUpdateLog = null;
                    return;
                }
                ulong? completedManifest = notice == CoopWorkshopNotice.Restart ? match.Value.CompletedManifest : null;
                string signature = match.Key + "|" + notice + "|" + completedManifest;
                string key = notice == CoopWorkshopNotice.UpdatePending ? "ExtendedData.CoopWorkshopUpdatePending" : "ExtendedData.CoopWorkshopRestartHint";
                if (signature != lastWorkshopUpdateLog)
                {
                    lastWorkshopUpdateLog = signature;
                    LogInfo("COOP_WORKSHOP " + notice + "; " + DescribeLaunchBlockContext(lobby) + "; workshopId=" + match.Key +
                        "; steamState=" + state + "; observedInstallTimestamp=" + match.Value.InstalledAt +
                        "; completedManifest=" + completedManifest);
                }
                if (signature != lastWorkshopUpdateNotice && ShowCoopWorkshopNotice(lobbyId, key)) lastWorkshopUpdateNotice = signature;
            }
            catch (Exception exception) { LogWarning("Could not report Coop Trail Workshop status: " + exception.Message); }
        }

        private static bool ShowCoopWorkshopNotice(ulong lobbyId, string messageKey) =>
            APIShared.Lobby.LobbyLaunchStatus.TryShowLocalSystemMessage(lobbyId, SerpLocalization.Get(messageKey));

        private bool IsLocalCoopSource(string packageRoot) => string.Equals(NormalizeWorkshopRoot(Path.GetDirectoryName(packageRoot)),
            NormalizeWorkshopRoot(customTrailsRoot), StringComparison.OrdinalIgnoreCase);

        private string DescribeCoopWorkshopCandidate(string packageRoot, string fingerprint)
        {
            string source = "source=" + (IsLocalCoopSource(packageRoot) ? "local" : "workshop") + "; root=" + packageRoot +
                "; contentFingerprint=" + fingerprint;
            if (!Shared.UnityMainThreadDispatch.IsMainThread || !Shared.WorkshopContentPaths.IsSteamworksReady()) return source;
            var ids = coopWorkshopSources.Where(item => string.Equals(item.Value.Root,
                NormalizeWorkshopRoot(Path.GetDirectoryName(packageRoot)), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (ids.Length != 1) return source;
            try { return source + "; workshopId=" + ids[0].Key + "; steamState=" + ReadWorkshopState(new PublishedFileId_t(ids[0].Key)); }
            catch (Exception exception) { return source + "; workshopId=" + ids[0].Key + "; steamState=unavailable (" + exception.Message + ")"; }
        }

        private static string NormalizeWorkshopRoot(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static EItemState ReadWorkshopState(PublishedFileId_t id) => (EItemState)SteamUGC.GetItemState(id);

        private static PublishedFileId_t[] GetCoopWorkshopSubscriptions()
        {
            uint count = SteamUGC.GetNumSubscribedItems();
            var ids = new PublishedFileId_t[count];
            return SteamUGC.GetSubscribedItems(ids, count) == count && SteamUGC.GetNumSubscribedItems() == count ? ids : null;
        }
    }
}
