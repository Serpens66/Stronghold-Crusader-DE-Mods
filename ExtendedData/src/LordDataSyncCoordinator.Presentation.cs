using APIShared.Presentation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ExtendedData
{
    internal sealed partial class LordDataSyncCoordinator
    {
        private PackagedLordPresentationRegistration presentationRegistration;
        private string presentationDigest;
        private int presentationGeneration;
        private bool legacyPresentationActive;

        private void InitializePresentation()
        {
            if (!PackagedLordPresentation.TryRegister(ExtendedDataPlugin.PluginGuid, "TrailLords", out presentationRegistration, out string reason))
                throw new InvalidOperationException("Packaged Lord presentation is unavailable: " + reason);
        }
        private void RefreshPresentation(LordDataSnapshot snapshot)
        {
            legacyPresentationActive = false;
            int generation = Interlocked.Increment(ref presentationGeneration);
            if (!Shared.UnityMainThreadDispatch.IsMainThread)
            {
                // Presentation is optional; callers continue with Fixes and network state.
                if (!Shared.UnityMainThreadDispatch.TryEnqueue(() => {
                    if (generation == Volatile.Read(ref presentationGeneration)) ApplyPresentation(snapshot);
                })) Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/LordData/presentation-dispatch",
                    "Lord presentation skipped because the Unity dispatcher is unavailable; gameplay Lord data is unaffected.");
                return;
            }
            ApplyPresentation(snapshot);
        }
        internal void PrepareLegacyTrailPresentation(IReadOnlyDictionary<int, FRONT_Multiplayer.MPAIVInfo> infos, bool networkCoop = false)
        {
            if (!activation.LordDataIntegration || presentationRegistration == null) return;
            if (networkCoop && active != null) { RefreshPresentation(active); return; }
            try { PrepareLegacyEntries(infos, networkCoop); }
            catch (Exception error) { ReportPresentationFailure(error); }
        }
        private void PrepareLegacyEntries(IReadOnlyDictionary<int, FRONT_Multiplayer.MPAIVInfo> infos, bool networkCoop)
        {
            // Old Trails have no authenticated lordmeta. Keep installed SE presentation; otherwise
            // use only the saved identity, without changing gameplay data or selecting speech clips.
            var entries = new List<PackagedLordPresentationEntry>();
            foreach (var pair in infos ?? new Dictionary<int, FRONT_Multiplayer.MPAIVInfo>())
            {
                var info = pair.Value;
                if (pair.Key < 1 || pair.Key > 8 || info == null || info.builtInLord ||
                    (info.lordConfig?.lordType ?? -1) >= 0 || string.IsNullOrWhiteSpace(info.lordName) ||
                    !networkCoop && GameAIManagerAPI.Instance.LordDataDict.ContainsKey(info.lordName.ToLower())) continue;
                entries.Add(new PackagedLordPresentationEntry(pair.Key, info.lordName,
                    LordPresentationData.FallbackName(info.lordName), null));
            }
            Interlocked.Increment(ref presentationGeneration);
            presentationRegistration.SetEntries(entries);
            presentationDigest = null;
            legacyPresentationActive = entries.Count != 0;
        }
        private void RestoreLegacySavedPresentation()
        {
            if (!activation.LordDataIntegration || active != null || pendingTrail != null || activeFromSave || legacyPresentationActive) return;
            var hud = ExtendedDataRuntime.GetExistingMainViewModel()?.HUDIngameMenu;
            var aivs = hud?.restartSkirmishMapInfo?.aivs;
            if (aivs != null && aivs.Any(info => !string.IsNullOrWhiteSpace(info?.lordName)))
                PrepareLegacyTrailPresentation(aivs.Select((info, index) => new { info, index }).ToDictionary(x => x.index + 1, x => x.info));
            else if (hud?.restartMPInfo?.LordNames is string[] names)
                PrepareLegacyTrailPresentation(names.Select((name, index) => new { name, index }).ToDictionary(x => x.index + 1,
                    x => new FRONT_Multiplayer.MPAIVInfo { lordName = x.name, lordType = -1, builtInLord = false }));
        }
        private void ApplyPresentation(LordDataSnapshot snapshot)
        {
            if (presentationRegistration == null || presentationDigest == snapshot?.Digest && snapshot != null) return;
            // Presentation is optional. Gameplay Lord data, Fixes and network state never depend on it,
            // so no failure here may reach the lifecycle, publication or invalidation callers.
            try { ApplyPresentationEntries(snapshot); }
            catch (Exception error) { ReportPresentationFailure(error); }
        }
        private void ReportPresentationFailure(Exception error)
        {
            presentationDigest = null;
            legacyPresentationActive = false;
            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/LordData/presentation",
                "Lord presentation unavailable; gameplay Lord data is unaffected: " + error);
            try { presentationRegistration?.SetEntries(new List<PackagedLordPresentationEntry>()); }
            catch (Exception clear) { Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/LordData/presentation-clear", clear.ToString()); }
        }
        private void ApplyPresentationEntries(LordDataSnapshot snapshot)
        {
            var entries = new List<PackagedLordPresentationEntry>();
            foreach (var slot in snapshot?.Slots ?? Array.Empty<LordDataSlot>())
            {
                // A built-in Lord using a custom AIC keeps Vanilla's localized presentation.
                if (slot.LordType >= 0 && slot.Presentation?.MetadataJson == null) continue;
                var value = slot.Presentation ?? new LordPresentationData { DisplayName = LordPresentationData.FallbackName(slot.LordName) };
                string guid = null;
                bool globalMedia = false;
                if (value.MediaId != null)
                    guid = ResolveMediaProvider(slot, value);
                else if (GameAIManagerAPI.Instance.LordDataDict.TryGetValue(slot.LordName.ToLower(), out var installed) &&
                    GameAssetModManager.Instance.TryGetRegisteredDirectory(installed.AssetProviderGuid, out string installedRoot))
                {
                    string metadata = Path.Combine(installedRoot, "lordmeta.json");
                    if (value.MetadataJson != null && File.Exists(metadata) && File.ReadAllText(metadata) == value.MetadataJson)
                    {
                        if (installed.AssetMode == ModAssetMode.Local) guid = installed.AssetProviderGuid;
                        else globalMedia = true;
                    }
                }
                entries.Add(new PackagedLordPresentationEntry(slot.PlayerId, slot.LordName,
                    value.DisplayName, value.MetadataJson, guid, globalMedia));
            }
            presentationRegistration.SetEntries(entries);
            presentationDigest = snapshot?.Digest;
        }
        // Media of one slot is optional: failures keep its name and metadata without media.
        private string ResolveMediaProvider(LordDataSlot slot, LordPresentationData value)
        {
            try
            {
                // Resolve a peer-local verified package root, never an absolute path received from the host.
                if (value.SourceRoot == null && pendingTrailRequirements.TryGetValue(slot.PlayerId, out var requirement) &&
                    requirement.Presentation?.MediaId == value.MediaId)
                    value.SourceRoot = requirement.Presentation.SourceRoot;
                string cache = Path.Combine(BepInEx.Paths.CachePath, "ExtendedData", "LordPresentation");
                string cachedRoot = Path.Combine(cache, TrailLordMediaStore.DirectoryName, value.MediaId);
                // A verified content-addressed copy is reused; rewriting it would only repeat IO
                // on files that may already belong to a registered asset provider.
                if (!IsVerifiedMediaCache(value, cache, cachedRoot))
                {
                    if (value.SourceRoot == null) FindSavedPresentationSource(value);
                    if (value.SourceRoot != null)
                        TrailLordMediaStore.Stage(new[] { value }, cache);
                    if (!Directory.Exists(cachedRoot)) return null;
                    TrailLordMediaStore.BindAndVerify(new[] { value }, cache);
                }
                string provider = "ExtendedData.LordMedia." + value.MediaId;
                if (!GameAssetModManager.Instance.IsRegistered(provider))
                {
                    // Asset-only private provider: no foreign GUID, scripts, locale overrides or game data.
                    File.WriteAllText(Path.Combine(cachedRoot, "info.json"), Shared.DependencyFreeJson.Serialize(new Dictionary<string, object> {
                        ["GUID"] = provider, ["Name"] = value.DisplayName, ["Author"] = "Trail author", ["Version"] = "1.0.0",
                        ["NetworkMode"] = 0, ["AssetMode"] = "Local", ["Manifest"] = 1,
                    }), new UTF8Encoding(false));
                    GameAssetModManager.Instance.RegisterAssetMod(cachedRoot);
                }
                return GameAssetModManager.Instance.IsRegistered(provider) ? provider : null;
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "saved-lord-media-cache",
                    "Lord media unavailable for player " + slot.PlayerId + "; name and metadata remain: " + error.Message);
                return null;
            }
        }
        private static bool IsVerifiedMediaCache(LordPresentationData value, string cache, string cachedRoot)
        {
            if (!Directory.Exists(cachedRoot)) return false;
            try { TrailLordMediaStore.BindAndVerify(new[] { value }, cache); return true; }
            catch (InvalidDataException) { return false; }
        }
        private void FindSavedPresentationSource(LordPresentationData value)
        {
            var roots = new List<string> { ConfigSettings.GetUserCustomTrailsPath() };
            roots.AddRange(Shared.WorkshopContentPaths.GetSubscribedItemRoots(message =>
                Shared.DebugLogHelper.LogWarningLimited(log, "saved-lord-media-discovery", message)));
            // Only fixed package locations one level below known roots; never an unrestricted recursive scan.
            foreach (string root in roots.Where(Directory.Exists))
            {
                string[] packages;
                try { packages = new[] { root }.Concat(Directory.GetDirectories(root)).ToArray(); }
                catch (IOException error) { Shared.DebugLogHelper.LogWarningLimited(log, "saved-lord-media-scan", error.Message); continue; }
                catch (UnauthorizedAccessException error) { Shared.DebugLogHelper.LogWarningLimited(log, "saved-lord-media-scan", error.Message); continue; }
                foreach (string package in packages)
                    foreach (string location in new[] { package, Path.Combine(package, "CoopMissions") })
                    {
                        if (!Directory.Exists(Path.Combine(location, TrailLordMediaStore.DirectoryName, value.MediaId))) continue;
                        try { TrailLordMediaStore.BindAndVerify(new[] { value }, location); return; }
                        catch (InvalidDataException error) {
                            Shared.DebugLogHelper.LogWarningLimited(log, "saved-lord-media-invalid", error.Message);
                        }
                    }
            }
        }
    }
}
