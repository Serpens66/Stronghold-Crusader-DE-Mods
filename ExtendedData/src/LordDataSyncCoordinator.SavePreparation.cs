using APIShared.Lobby;
using APIShared.Savegames;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    internal sealed partial class LordDataSyncCoordinator
    {
        private sealed class PreparedLordSave
        {
            internal string Operation, Path, Hash, ManifestDigest, SnapshotDigest, Failure;
            internal LordDataSnapshot Author;
            internal LordPackageManifest AuthorManifest;
            internal bool Published, ExactDigests, Frozen, SeenPublication;
            internal readonly LordSaveOwnership Ownership = new LordSaveOwnership();
            internal bool MissionOwned => Ownership.MissionOwned;
            internal FileHeader Header;
            internal bool LegacyNamedLords;
        }
        private PreparedLordSave saveCandidate, preparedLordSave, provisionalLordSave;

        internal bool HasPreparedLordSave => saveCandidate != null || preparedLordSave != null;

        private void InitializeSavePreparation()
        {
            if (!MultiplayerSavePreparation.TryRegisterObserver(ExtendedDataPlugin.PluginGuid, "LordData.SavePreparation",
                ObserveSavePreparation, out string reason)) throw new InvalidOperationException(reason);
            if (!LobbyLaunchGuards.TryRegisterReadiness(ExtendedDataPlugin.PluginGuid, "LordData.PreparedSave",
                PreparedSaveGuard, out reason)) throw new InvalidOperationException(reason);
        }

        private void BeginSaveCandidate(FileHeader header, LordDataSnapshot author, LordPackageManifest authorManifest,
            LordPackageManifest published, LordDataSnapshot snapshot, string originalHash)
        {
            DiscardUnboundSaveCandidate();
            saveCandidate = new PreparedLordSave { Path = System.IO.Path.GetFullPath(header.filePath), Hash = originalHash,
                Author = author, AuthorManifest = authorManifest, ManifestDigest = published?.Digest, SnapshotDigest = snapshot?.Digest,
                Published = published != null, ExactDigests = published != null, SeenPublication = published != null };
        }

        private bool PrepareEmptyLordSave(FRONT_Multiplayer lobby, FileHeader header, string originalHash)
        {
            if (!IsHostLobby(lobby)) throw new InvalidDataException("Only the authenticated host can prepare saved Lord data.");
            var snapshot = LordDataSnapshot.Create(CurrentSessionId(lobby), fixes.Installed, Array.Empty<LordDataSlot>());
            var manifest = LordPackageManifest.Create(CurrentSessionId(lobby), false, Array.Empty<LordPackageSlot>());
            BeginSaveCandidate(header, null, null, manifest, snapshot, originalHash);
            PublishPackageManifest(manifest, "multiplayer-save");
            Publish(snapshot, "multiplayer-save");
            return true;
        }

        internal void DiscardUnboundSaveCandidate()
        {
            var candidate = saveCandidate;
            saveCandidate = null;
            if (candidate != null) RetireSavePublication(candidate);
        }

        private void ObserveSavePreparation(MultiplayerSavePreparationSnapshot snapshot)
        {
            if (snapshot == null || !activation.LordDataIntegration) return;
            var current = MultiplayerSavePreparation.Current;
            if (snapshot.Phase != MultiplayerSavePreparationPhase.Ended &&
                !LordSaveOwnership.IsCurrentObservation(snapshot.OperationId, snapshot.Revision, current?.OperationId, current?.Revision ?? -1)) return;
            if (snapshot.Phase == MultiplayerSavePreparationPhase.Preparing)
            {
                if (preparedLordSave?.Operation == snapshot.OperationId) return;
                if (preparedLordSave != null) RetireSavePublication(preparedLordSave);
                if (!IsCurrentPreparation(snapshot)) return;
                preparedLordSave = null;
                if (saveCandidate != null && !LordSaveFileRead.MatchesSource(saveCandidate.Path, saveCandidate.Hash,
                    System.IO.Path.GetFullPath(snapshot.LocalPath), snapshot.LocalHash))
                {
                    var changed = saveCandidate;
                    saveCandidate = null;
                    RetireSavePublication(changed);
                    if (!IsCurrentPreparation(snapshot)) return;
                    throw new InvalidDataException("The selected Lord save changed after its requester preparation.");
                }
                if (snapshot.IsHost && saveCandidate == null)
                {
                    // Other legitimate callers enter HostLoadGame without opening a requester.
                    // ED is excluded from savegame settings fingerprints; use the same local
                    // preparation here before this SDK operation's first readiness checkpoint.
                    var lobby = ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer;
                    var header = MapFileManager.Instance.GetHeaderFromMpSaveFileName(snapshot.SaveName);
                    if (!IsHostLobby(lobby) || lobby.currentLobby.identifier != snapshot.LobbyId || header == null ||
                        !string.Equals(System.IO.Path.GetFullPath(header.filePath), System.IO.Path.GetFullPath(snapshot.LocalPath), StringComparison.OrdinalIgnoreCase) ||
                        !PrepareSave(lobby, header))
                        throw new InvalidDataException("The direct multiplayer save could not prepare its Lord data.");
                    if (!IsCurrentPreparation(snapshot)) return;
                }
                PreparedLordSave next = saveCandidate;
                saveCandidate = null;
                if (next != null && !LordSaveFileRead.MatchesSource(next.Path, next.Hash,
                    System.IO.Path.GetFullPath(snapshot.LocalPath), snapshot.LocalHash))
                {
                    RetireSavePublication(next);
                    if (!IsCurrentPreparation(snapshot)) return;
                    throw new InvalidDataException("The direct Lord save changed while it was prepared.");
                }
                if (next == null) next = ReadPreparedAuthor(snapshot.LocalPath);
                next.Operation = snapshot.OperationId;
                if (!string.Equals(next.Hash, snapshot.LocalHash, StringComparison.OrdinalIgnoreCase))
                {
                    RetireSavePublication(next);
                    throw new InvalidDataException("The prepared Lord save file changed before operation admission.");
                }
                BindCompleteAuthorDigests(next, snapshot.LobbyId.ToString());
                preparedLordSave = next;
            }
            else if (snapshot.Phase == MultiplayerSavePreparationPhase.Ended && preparedLordSave?.Operation == snapshot.OperationId)
            {
                var ended = preparedLordSave;
                preparedLordSave = null;
                if (!ended.MissionOwned && ended.Ownership.InitializingSession.HasValue &&
                    snapshot.EndReason == MultiplayerSavePreparationEndReason.ScopeFinished)
                    provisionalLordSave = ended;
                else if (!ended.MissionOwned) RetireSavePublication(ended);
            }
            else if ((snapshot.Phase == MultiplayerSavePreparationPhase.Released || snapshot.Phase == MultiplayerSavePreparationPhase.NativeLoading) &&
                preparedLordSave?.Operation == snapshot.OperationId)
                preparedLordSave.Frozen = true;
        }

        private static bool IsCurrentPreparation(MultiplayerSavePreparationSnapshot snapshot)
        {
            var current = MultiplayerSavePreparation.Current;
            return LordSaveOwnership.IsCurrentObservation(snapshot.OperationId, snapshot.Revision, current?.OperationId, current?.Revision ?? -1);
        }

        private PreparedLordSave ReadPreparedAuthor(string path)
            => LordSaveFileRead.Read(path, hash => ReadPreparedAuthorLocked(path, hash));

        private PreparedLordSave ReadPreparedAuthorLocked(string path, string originalHash)
        {
            LordDataSnapshot author = null;
            LordPackageManifest manifest = null;
            if (MapArchive.TryLoad(path, out MapArchive archive))
                using (archive)
                {
                    byte[] data = archive.TryReadBinaryFile(SaveEntry, ignoreCase: true);
                    if (data != null) ParseSavedLordData(data, out author, out manifest);
                }
            return new PreparedLordSave { Path = System.IO.Path.GetFullPath(path), Hash = originalHash, Author = author, AuthorManifest = manifest };
        }

        private void TransferSavePreparationToMission(long session)
        {
            var current = MultiplayerSavePreparation.Current;
            if (preparedLordSave != null && current?.OperationId == preparedLordSave.Operation &&
                current.Phase == MultiplayerSavePreparationPhase.NativeLoading)
            { preparedLordSave.Ownership.Initializing(session); activeFromSave = true; }
        }

        private void CommitSavePreparationToMission(long session)
        {
            preparedLordSave?.Ownership.Started(session);
            if (provisionalLordSave?.Ownership.Started(session) == true) provisionalLordSave = null;
        }

        private void RetireFailedSaveMission(long session)
        {
            var failed = provisionalLordSave?.Ownership.FailedBeforeStarted(session) == true ? provisionalLordSave :
                preparedLordSave?.Ownership.FailedBeforeStarted(session) == true ? preparedLordSave : null;
            if (failed == null) return;
            if (ReferenceEquals(provisionalLordSave, failed)) provisionalLordSave = null;
            if (ReferenceEquals(preparedLordSave, failed)) preparedLordSave = null;
            // Restore only this failed scope; a newer candidate/publication keeps ownership.
            RetireSavePublication(failed);
        }

        private void RecordPreparedSaveFailure(string message)
        {
            if (preparedLordSave != null && !preparedLordSave.MissionOwned) preparedLordSave.Failure = message;
        }

        private void RetireSavePublication(PreparedLordSave scope)
        {
            if (scope.MissionOwned) return;
            if (saveCandidate != null && !ReferenceEquals(scope, saveCandidate) && saveCandidate.Published &&
                scope.ManifestDigest == saveCandidate.ManifestDigest && scope.SnapshotDigest == saveCandidate.SnapshotDigest) return;
            // A client may receive/apply the publication before its first readiness
            // checkpoint. Attribute it by the bound immutable author record as well.
            if (scope.ManifestDigest == null && packageManifest != null && ValidatePreparedAuthor(scope, packageManifest, active))
            { scope.ManifestDigest = packageManifest.Digest; scope.SnapshotDigest = active?.Digest; }
            bool ownsSnapshot = scope.SnapshotDigest != null && active?.Digest == scope.SnapshotDigest;
            bool ownsManifest = scope.ManifestDigest != null && packageManifest?.Digest == scope.ManifestDigest;
            if (!ownsSnapshot && !ownsManifest) return;
            var lobby = ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer;
            if (ownsSnapshot && !scope.Published && active != null &&
                LordSnapshotSlotSelection.Inspect(active, lobby?.AIVs,
                    player => IsActiveAiSlot(lobby, player), embeddedTrailSlots, pendingTrail,
                    ConfigSettings.extendedLordPaths).Matches)
            { activeFromSave = false; return; }
            fixes.Restore();
            if (ownsSnapshot) { active = null; activeFromSave = false; RefreshPresentation(null); settings.LordDataStatus = "NONE"; }
            if (ownsManifest) { packageManifest = null; localLordPaths.Clear(); settings.LordPackageStatus = string.Empty; }
            ExtendedDataModDataApi.SetNetworkSnapshot(null, lobbyId.HasValue);
            // Only the host may replace synchronized publications. Re-evaluate its CURRENT
            // ordinary lobby selection rather than resurrecting the save's previous selection.
            if (IsHostLobby(lobby)) OnLobbyOpened(lobby, "save-preparation-ended");
        }

        private LobbyLaunchGuardResult PreparedSaveGuard(LobbyLaunchGuardContext context)
        {
            if (!activation.LordDataIntegration || context.PreparedSave == null) return LobbyLaunchGuardResult.Ready;
            return PreparedSaveReadiness(context.PreparedSave);
        }

        internal LobbyLaunchGuardResult PreparedSaveReadiness(MultiplayerSavePreparationSnapshot preparation)
        {
            var scope = preparedLordSave;
            if (scope == null || scope.Operation != preparation.OperationId)
                return LobbyLaunchGuardResult.Rejected("The selected Lord save has no operation-bound preparation.");
            if (!LordSaveFileRead.MatchesSource(scope.Path, scope.Hash,
                System.IO.Path.GetFullPath(preparation.LocalPath), preparation.LocalHash))
                return LobbyLaunchGuardResult.Rejected("The selected Lord save provenance changed.");
            if (!string.IsNullOrEmpty(scope.Failure)) return LobbyLaunchGuardResult.Rejected(scope.Failure);
            if (packageManifest == null || packageManifest.SessionId != preparation.LobbyId.ToString())
                return LobbyLaunchGuardResult.Waiting("Waiting for the saved Lord package publication.");
            try
            {
                if (!ValidatePreparedAuthor(scope, packageManifest, active))
                    return scope.SeenPublication ? LobbyLaunchGuardResult.Rejected("The prepared saved Lord author values changed.") :
                        LobbyLaunchGuardResult.Waiting("Waiting for the matching saved Lord values.");
                if ((scope.ExactDigests || scope.Frozen) &&
                    (scope.ManifestDigest != null && scope.ManifestDigest != packageManifest.Digest ||
                    scope.SnapshotDigest != null && scope.SnapshotDigest != active?.Digest))
                    return LobbyLaunchGuardResult.Rejected("The prepared Lord publication changed during this save attempt.");
                scope.ManifestDigest = packageManifest.Digest;
                scope.SnapshotDigest = packageManifest.UseLocalValues ? null : active?.Digest;
                scope.SeenPublication = true;
                if (!packageManifest.UseLocalValues && packageManifest.Slots.Any(slot => slot.NeedsSnapshot) &&
                    !HasVerifiedSnapshot(out string reason)) return LobbyLaunchGuardResult.Rejected(reason);
                int requiredMask = packageManifest.Slots.Where(slot => packageManifest.UseLocalValues || slot.NeedsLocalFiles)
                    .Aggregate(0, (mask, slot) => mask | (1 << (slot.PlayerId - 1)));
                var identity = PlayerIdentityHelper.CaptureLocalPlayerId(false);
                if (!identity.IsResolved) return LobbyLaunchGuardResult.Waiting("Waiting for the local saved-Lord player identity.");
                if (!PlayerIdentityHelper.TryCaptureHumanRoster(false, true, out Dictionary<int, ulong> humans, out _, out _))
                    return LobbyLaunchGuardResult.Waiting("Waiting for saved-Lord participant reports.");
                if (!new HashSet<ulong>(humans.Values).SetEquals(preparation.HumanPeers))
                    return LobbyLaunchGuardResult.Waiting("Waiting for the saved-Lord participant slot mapping.");
                foreach (int player in humans.Keys.Where(player => preparation.IsHost || player == identity.PlayerId))
                {
                    string status = player == identity.PlayerId ? settings.LordPackageStatus :
                        player < settings.LordPackageStatusData.Length ? settings.LordPackageStatusData[player] : null;
                    if (requiredMask != 0)
                    {
                        string[] parts = (status ?? string.Empty).Split('|');
                        if (parts.Length != 3 || parts[0] != "LOCAL" || parts[1] != packageManifest.Digest)
                            return LobbyLaunchGuardResult.Waiting("Waiting for saved-Lord file confirmations.");
                        if (!int.TryParse(parts[2], out int mask) || (mask & requiredMask) != requiredMask)
                            return LobbyLaunchGuardResult.Rejected("A participant lacks the required saved Lord files.");
                    }
                    // Even an empty publication must be acknowledged: it retires any
                    // previous lobby/save overlay before the ordinary archive is loaded.
                    if (!packageManifest.UseLocalValues && scope.SnapshotDigest != null)
                    {
                        status = player == identity.PlayerId ? settings.LordDataStatus :
                            player < settings.LordDataStatusData.Length ? settings.LordDataStatusData[player] : null;
                        // Remote ERROR has no digest/operation identity and may still describe
                        // an older selection while this save's deferred READY is in flight.
                        // Current local failures remain authoritative; every peer must still
                        // confirm the exact saved snapshot before the host can release it.
                        if (player == identity.PlayerId && (status ?? string.Empty).StartsWith("ERROR|", StringComparison.Ordinal))
                            return LobbyLaunchGuardResult.Rejected("A participant rejected the saved Lord values.");
                        if (status != "READY|" + active.Digest)
                            return LobbyLaunchGuardResult.Waiting("Waiting for saved-Lord value confirmations.");
                    }
                }
                return LobbyLaunchGuardResult.Ready;
            }
            catch (Exception failure) { return LobbyLaunchGuardResult.Rejected(failure.Message); }
        }

        private bool ValidatePreparedAuthor(PreparedLordSave scope, LordPackageManifest manifest, LordDataSnapshot received)
        {
            if (LordSaveAuthorProof.AcceptsEmptyPublication(scope.Author, scope.AuthorManifest, manifest, received, fixes.Installed))
                return true;
            if (scope.AuthorManifest != null)
            {
                if (manifest.UseLocalValues != scope.AuthorManifest.UseLocalValues || manifest.Slots.Count != scope.AuthorManifest.Slots.Count) return false;
                foreach (LordPackageSlot original in scope.AuthorManifest.Slots)
                {
                    var value = manifest.Slots.SingleOrDefault(slot => slot.PlayerId == original.PlayerId);
                    if (value == null || value.LordType != original.LordType || value.LordName != original.LordName ||
                        value.ConfigName != original.ConfigName || value.ConfigChecksum != original.ConfigChecksum || value.FileDigest != original.FileDigest ||
                        value.NeedsLocalFiles != original.NeedsLocalFiles || value.EmbeddedTrail != original.EmbeddedTrail ||
                        manifest.UseLocalValues && (value.FixesDigest != original.FixesDigest ||
                            !LordSaveAuthorProof.AllowsInheritedDigest(original.FixesSourcesDigest, value.FixesSourcesDigest)))
                        return false;
                }
            }
            if (manifest.UseLocalValues) return scope.AuthorManifest?.UseLocalValues == true;
            if (scope.Author == null)
            {
                if (received == null || received.FixesInstalled != fixes.Installed) return false;
                if (received.Slots.Count == 0 && manifest.Slots.Count == 0) return true;
                scope.Header = scope.Header ?? MapFileManager.Instance.GetFileInfoFromFileName(scope.Path, scope.Path, 0, loadRestartInfo: true);
                string[] names = scope.Header?.restartMPInfo?.LordNames;
                if (names == null || names.Length != 8) throw new InvalidDataException("Legacy saved Lord identities are unavailable.");
                scope.LegacyNamedLords = names.Any(name => !string.IsNullOrWhiteSpace(name));
                if (scope.LegacyNamedLords && received.Slots.Count == 0) return false;
                ValidateSavedLords(scope.Header, received, manifest);
                return manifest.MatchesSnapshot(received, fixes.Normalize);
            }
            if (!LordSaveAuthorProof.Matches(scope.Author, received, fixes.Normalize)) return false;
            return manifest.MatchesSnapshot(received, fixes.Normalize);
        }

        private void BindCompleteAuthorDigests(PreparedLordSave scope, string session)
        {
            // A no-sync author may legitimately receive the host's empty publication, so its
            // digests are adopted from the first validated publication instead.
            if (LordSaveAuthorProof.NeedsNoSync(scope.Author, scope.AuthorManifest)) return;
            if (scope.ManifestDigest != null || scope.AuthorManifest == null || scope.Author == null ||
                scope.Author.Slots.Any(slot => slot.FixesSources == null && scope.Author.FixesInstalled || !slot.LordType.HasValue)) return;
            var slots = scope.Author.Slots.Select(slot => new LordDataSlot {
                PlayerId = slot.PlayerId, LordType = slot.LordType, LordName = slot.LordName,
                ConfigName = slot.ConfigName, ConfigChecksum = slot.ConfigChecksum, ModLordJson = slot.ModLordJson,
                Presentation = slot.Presentation, FixesJson = fixes.Normalize(slot.FixesJson), FixesSources = fixes.NormalizeSources(slot.FixesSources),
            }).ToArray();
            var expected = LordDataSnapshot.Create(session, scope.Author.FixesInstalled, slots);
            scope.ManifestDigest = UpdateSavedManifest(scope.AuthorManifest, session, slots).Digest;
            scope.SnapshotDigest = scope.AuthorManifest.UseLocalValues ? null : expected.Digest;
            scope.ExactDigests = true;
        }
    }
}
