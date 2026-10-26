using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CrusaderDE;
using ExtendedData.Core;
using MonoMod.RuntimeDetour;
using SHCDESE.API;

namespace ExtendedData
{
    internal sealed partial class ExtendedDataRuntime
    {
        private readonly CoopTrailPackageSession packageSession = new CoopTrailPackageSession();
        private IReadOnlyList<CoopTrailPackageOverview> packageOverviews = Array.Empty<CoopTrailPackageOverview>();
        private delegate void BuildLordCatalogueDelegate(CustomisationFileManager self);
        private Hook buildLordCatalogueHook;
        private BuildLordCatalogueDelegate buildLordCatalogueOriginal;
        private int lordCatalogueRevision;

        private void InstallLordCatalogueObserver()
        {
            MethodInfo method = typeof(CustomisationFileManager).GetMethod("BuildFileLists",
                BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null)
                ?? throw new MissingMethodException("CustomisationFileManager.BuildFileLists");
            buildLordCatalogueHook = new Hook(method, (BuildLordCatalogueDelegate)BuildLordCatalogueHook);
            buildLordCatalogueOriginal = buildLordCatalogueHook.GenerateTrampoline<BuildLordCatalogueDelegate>();
        }

        private void BuildLordCatalogueHook(CustomisationFileManager self)
        {
            // File-list building can run off-thread during startup. Only publish a revision;
            // cache inspection and all game/UI work remain in the existing main-thread paths.
            try { buildLordCatalogueOriginal(self); }
            finally { Interlocked.Increment(ref lordCatalogueRevision); }
        }

        private CoopTrailPackage LoadSelectedPackage(bool forceValidation = false)
        {
            long started = 0;
            Shared.DevDiag.Start(ref started);
            try
            {
                CoopTrailPackage package = packageSession.GetOrLoad(packageOverviews,
                    settings.ActiveCoopPackageId, out bool hit, forceValidation);
                Shared.DevDiag.Count(hit ? "coop.package.cacheHit" : "coop.package.fullValidation");
                return package;
            }
            finally { Shared.DevDiag.StopSpike(ref started, "coop.package", 10000); }
        }

        private void OnCoopPackagesExported()
        {
            packageSession.Clear();
            OnActiveCoopPackageChanged();
        }

        private ResolvedMission EnsureResolvedMission(int trailId, int missionId)
        {
            if (!HasActiveCoopPackage()) throw new InvalidOperationException("No custom Coop package is selected.");
            bool previousUpdating = updatingPackage;
            updatingPackage = true;
            try { ApplyActivePackage(refreshVisible: false); }
            finally { updatingPackage = previousUpdating; }
            if (activePackage == null || !catalog.TryGet(trailId, missionId, out LoadedMission loaded) ||
                !CoopTrailMissionRange.Contains(activePackage.Manifest.MissionCount, trailId, missionId))
                throw new InvalidDataException(string.IsNullOrEmpty(localPackageError) ? "Custom Coop mission is unavailable." : localPackageError);

            long started = 0;
            Shared.DevDiag.Start(ref started);
            try
            {
                // Vanilla normally consumes this flag in doOpen. A source edit can also
                // happen while the page stays open; refresh before consulting its objects.
                if (CustomisationFileManager.Instance.filesChanged)
                    CustomisationFileManager.Instance.BuildFileLists();
                int revision = Volatile.Read(ref lordCatalogueRevision);
                ResolvedMission mission = resolved.GetOrResolve(loaded,
                    item => ResolveMissionTemplate(item, revision),
                    item => IsMissionTemplateCurrent(item, revision), out bool hit);
                Shared.DevDiag.Count(hit ? "coop.mission.cacheHit" : "coop.mission.resolve");
                missionTables.Install(MissionCatalog.ToKey(trailId + 1, missionId), GetTrail(trailId + 1),
                    missionId - 1, CoopMissionDataCopies.CopySetup(mission.CoopData));
                localPackageError = string.Empty;
                return mission;
            }
            finally { Shared.DevDiag.StopSpike(ref started, "coop.mission", 10000); }
        }

        private ResolvedMission ResolveMissionTemplate(LoadedMission loaded, int revision)
        {
            ResolvedMission mission = new MissionAssetResolver().Resolve(loaded);
            IEnumerable<string> files = mission.AiInfoByPlayerIndex.Values
                .SelectMany(info => (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                    .Select(aiv => aiv.path).Concat(new[] { info.lordConfig?.path }))
                .Concat(new[] { mission.Header.filePath });
            mission.Dependencies = new CoopMissionDependencies(revision, files,
                loaded.Definition.Map.Source == "bundled" ? null : mission.Header);
            if (revision != Volatile.Read(ref lordCatalogueRevision))
                throw new MissionRetryException("Lord catalogue changed while preparing the Coop mission; select it again.");
            if (!string.IsNullOrWhiteSpace(loaded.Definition.ModSettingsError))
                LogError("Trail" + loaded.TrailNumber + "/" + loaded.MissionNumber.ToString("00") +
                    " ignored an invalid mod-settings sidecar; local mod settings remain unchanged: " + loaded.Definition.ModSettingsError);
            return mission;
        }

        private bool IsMissionTemplateCurrent(ResolvedMission mission, int revision)
        {
            MapReference map = mission.Loaded.Definition.Map;
            return mission.Dependencies.IsCurrent(revision, map.Source == "bundled" ? null :
                MapFileManager.Instance.GetHeaderFromFileNameMP(map.Name ?? map.Id?.ToString()));
        }

        private void RejectMissionSelection(int trailId, int missionId, Exception exception)
        {
            selected = null;
            missingMods = Array.Empty<string>();
            lordDataCoordinator?.SetEmbeddedTrailSlots(null);
            missionSettingsCoordinator.ExitContext();
            // Package loading already classified missing/mismatched content. Do not overwrite
            // that useful status when the absent package subsequently prevents mission selection.
            if (activePackage != null || string.IsNullOrWhiteSpace(localPackageError))
                SetLocalPackageError(ExtendedDataSettingsViewModel.InvalidStatusPrefix + exception.Message,
                    CoopPackageBlockMessages.MissionPreparationFailure(exception));
            LogError("Could not prepare custom Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ": " + exception);
            ShowLocalPackageBlockAfterSync();
        }
    }
}
