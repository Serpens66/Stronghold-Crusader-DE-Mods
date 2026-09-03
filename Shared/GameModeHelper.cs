using SHCDESE.API;
using SHCDESE.EventAPI.MapLoader;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if !SHARED_PRESET_TESTS
using Steamworks;
#endif

namespace Shared
{
    internal enum GameModeKind
    {
        Unknown,
        MapEditor,
        Campaign,
        StandaloneMission,
        CustomGame,
        VanillaTrail,
        CustomTrail,
        CoopTrail,
        SandsOfTime,
    }

    internal enum GameModeLaunchVariant
    {
        Standard,
        Customized,
        RestoredCustomizedSave,
    }

    internal enum GameTrailType
    {
        FirstEdition = 0,
        Warchest = 1,
        Extreme = 2,
        SandsOne = 11,
        SandsTwo = 12,
        SandsThree = 13,
        SandsFour = 14,
        SandsFive = 15,
        SandsSix = 16,
        SandsSeven = 17,
        SandsEight = 18,
    }

    internal static class GameModeHelper
    {
        private const int NoGameValue = -1;
        private const int NoCoopTrail = 0;
        private const uint NonCampaignMapId = uint.MaxValue;
        private const int MinimumOriginApiVersion = 1;
        private const int SupportedOriginApiVersion = 2;
        private const int FirstCustomTrailId = 90;
        private const int LastCustomTrailId = 92;
        private const int FirstCoopTrailId = 0;
        private const int LastCoopTrailId = 3;
        private const int FirstMissionId = 1;
        private const int LastCoopMissionId = 10;

        public static GameModeSnapshot Capture(bool multiplayerSave = false)
        {
            return CaptureCore(
                multiplayerSave,
                campaignMapId: 0,
                eventTrailType: NoGameValue,
                editorLoad: false);
        }

        public static GameModeSnapshot Capture(MapStartEventArgs args) =>
            CaptureCore(
                args != null && args.bMultiplayerSave != 0,
                args?.CampaignMapId ?? 0,
                NoGameValue,
                editorLoad: false);

        public static GameModeSnapshot Capture(MapLoadEventArgs args) =>
            CaptureCore(
                args != null && args.bMultiplayerSave != 0,
                args != null && args.CampaignMapID != NonCampaignMapId && args.CampaignMapID <= int.MaxValue
                    ? (int)args.CampaignMapID
                    : 0,
                args?.TrailType ?? NoGameValue,
                editorLoad: false);

        public static GameModeSnapshot Capture(LoadSaveGameEventArgs args) =>
            CaptureCore(
                multiplayerSave: false,
                campaignMapId: 0,
                eventTrailType: NoGameValue,
                editorLoad: args != null && args.LoadingEditorMap);

        internal static bool AllowsCustomGameMods(
            GameModeKind kind,
            GameModeLaunchVariant launchVariant)
        {
            if (kind == GameModeKind.CustomGame)
                return true;
            if (launchVariant == GameModeLaunchVariant.Standard)
                return false;

            return kind == GameModeKind.VanillaTrail ||
                kind == GameModeKind.CustomTrail ||
                kind == GameModeKind.CoopTrail ||
                kind == GameModeKind.SandsOfTime;
        }

        internal static bool AllowsRegularGameplayMods(
            GameModeKind kind,
            GameModeLaunchVariant launchVariant) =>
            kind == GameModeKind.MapEditor || AllowsCustomGameMods(kind, launchVariant);

        private static GameModeSnapshot CaptureCore(
            bool multiplayerSave,
            int campaignMapId,
            int eventTrailType,
            bool editorLoad)
        {
            Director director = Director.instance;
            GameData gameData = GameData.Instance;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;

            int lobbyMembers = lobby?.members?.Count ?? -1;
            int realLobbyMembers = 0;
            int skirmishLobbyMembers = 0;
            if (lobby?.members != null)
            {
                foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                {
                    if (member == null)
                        continue;
                    if (member.SkirmishMember)
                        skirmishLobbyMembers++;
                    else
                        realLobbyMembers++;
                }
            }

            int gameMembers = platform?.gameMembers?.Count ?? -1;
            int realNetworkGameMembers = 0;
            if (platform?.gameMembers != null)
            {
                foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                {
                    if (member != null && !member.skirmishAI && member.steamID > 1000)
                        realNetworkGameMembers++;
                }
            }

            bool directorMultiplayer = director != null && director.MultiplayerGame;
            // SCRIPT EXTENDER BUG WORKAROUND: IsMultiplayerGame() can become false
            // during a real multiplayer map transition. Remove the additional
            // Vanilla/roster evidence only after an upstream fix is verified. After
            // every Script Extender update, revalidate these sources and precedence.
            bool platformMultiplayer = GameNetworkAPI.IsMultiplayerGame();
            bool realMultiplayer =
                multiplayerSave ||
                directorMultiplayer ||
                platformMultiplayer ||
                realLobbyMembers > 0 ||
                realNetworkGameMembers > 0;

            int gameType = gameData != null ? gameData.game_type : NoGameValue;
            int skirmishGameType = gameData != null ? gameData.SkirmishGameType : NoGameValue;
            int skirmishTrailType = gameData != null ? gameData.SkirmishTrailType : NoGameValue;
            int coopTrailId = gameData != null ? gameData.coopTrailID : NoGameValue;
            bool mapEditor =
                editorLoad ||
                gameData?.mapType == Enums.GameModes.MAP_EDITOR ||
                IsMapEditor();
            bool sandsOfTime = TryIsSandsOfTime(gameData);
            bool customTrailRestart = TryCaptureCustomTrailRestart();
            // game_type 3 is Vanilla's skirmish family. Immediately after leaving a
            // real multiplayer game, a new local skirmish can reach OnStartMap before
            // Vanilla changes SkirmishGameType from -1. Its all-local skirmish lobby
            // is the stable transition signal; no map or mission allow-list is needed.
            bool localSkirmishTransition =
                lobbyMembers > 0 &&
                skirmishLobbyMembers == lobbyMembers &&
                realLobbyMembers == 0 &&
                realNetworkGameMembers == 0;
            bool singleplayerSkirmishMode =
                !realMultiplayer &&
                !mapEditor &&
                gameType == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
                (skirmishGameType >= 0 || localSkirmishTransition);

            bool vanillaCustomized = TryCaptureVanillaCustomizedTrail(
                out int customizedTrailType,
                out int customizedTrailId);
            ExternalCustomizedOrigin externalOrigin = CaptureExternalCustomizedOrigin();
            GameModeKind observedKind = ResolveKind(
                mapEditor,
                gameType,
                skirmishGameType,
                skirmishTrailType,
                coopTrailId,
                campaignMapId,
                eventTrailType);
            GameModeKind kind = observedKind;
            if (observedKind == GameModeKind.CustomGame && externalOrigin.LaunchPending)
            {
                GameModeKind originKind = ResolveExternalOriginKind(externalOrigin.Origin);
                if (originKind != GameModeKind.Unknown)
                    kind = originKind;
            }
            if (sandsOfTime && kind != GameModeKind.MapEditor && kind != GameModeKind.CoopTrail)
                kind = GameModeKind.SandsOfTime;
            else if (customTrailRestart && (kind == GameModeKind.Unknown || kind == GameModeKind.CustomGame))
                kind = GameModeKind.CustomTrail;
            if (vanillaCustomized && customizedTrailId >= 0 && kind == GameModeKind.CustomGame)
            {
                bool builtInOriginRequired = externalOrigin.SupportsBuiltInOrigins;
                if (IsVanillaTrailType(customizedTrailType) &&
                    (!builtInOriginRequired || externalOrigin.Origin == ExternalCustomizedOrigin.VanillaTrail))
                    kind = GameModeKind.VanillaTrail;
                else if (IsSandsTrailType(customizedTrailType) &&
                    (!builtInOriginRequired || externalOrigin.Origin == ExternalCustomizedOrigin.SandsOfTime))
                    kind = GameModeKind.SandsOfTime;
            }
            GameModeLaunchVariant launchVariant = ResolveLaunchVariant(
                kind,
                vanillaCustomized,
                customizedTrailType,
                customizedTrailId,
                observedKind == GameModeKind.CustomGame,
                externalOrigin);
            bool conflictingOrigin = externalOrigin.IsInvalid ||
                (externalOrigin.Origin != ExternalCustomizedOrigin.None &&
                 (!ExternalOriginMatchesKind(externalOrigin, kind) ||
                  !ExternalOriginMatchesEvidence(
                      externalOrigin,
                      kind,
                      skirmishTrailType,
                      coopTrailId,
                      eventTrailType,
                      vanillaCustomized,
                      customizedTrailType,
                      customizedTrailId)));

            return new GameModeSnapshot(
                realMultiplayer,
                singleplayerSkirmishMode,
                singleplayerSkirmishMode && skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
                singleplayerSkirmishMode &&
                    (skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL ||
                     skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL),
                mapEditor,
                multiplayerSave,
                director != null,
                directorMultiplayer,
                director != null && director.SkirmishModeGame,
                GameNetworkAPI.IsNetworkedEnvironment(),
                platformMultiplayer,
                lobbyMembers,
                realLobbyMembers,
                skirmishLobbyMembers,
                gameMembers,
                realNetworkGameMembers,
                gameType,
                skirmishGameType,
                skirmishTrailType,
                coopTrailId,
                kind,
                launchVariant,
                campaignMapId,
                eventTrailType,
                externalOrigin.Origin != ExternalCustomizedOrigin.None
                    ? externalOrigin.TrailId
                    : customizedTrailId,
                externalOrigin.Origin != ExternalCustomizedOrigin.None
                    ? externalOrigin.MissionId
                    : customizedTrailId,
                externalOrigin.Origin,
                conflictingOrigin);
        }

        internal static GameModeKind ResolveKind(
            bool mapEditor,
            int gameType,
            int skirmishGameType,
            int skirmishTrailType,
            int coopTrailId,
            int campaignMapId = 0,
            int eventTrailType = NoGameValue)
        {
            if (mapEditor)
                return GameModeKind.MapEditor;
            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_CAMPAIGN || campaignMapId > 0)
                return GameModeKind.Campaign;
            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_MAP)
                return GameModeKind.StandaloneMission;
            if (coopTrailId > NoCoopTrail)
                return GameModeKind.CoopTrail;

            bool hasTrailEvent = eventTrailType >= 0;
            int effectiveTrailType = hasTrailEvent ? eventTrailType : skirmishTrailType;
            bool vanillaTrailMode =
                skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL;
            if ((vanillaTrailMode || hasTrailEvent) && IsSandsTrailType(effectiveTrailType))
                return GameModeKind.SandsOfTime;
            if ((vanillaTrailMode || hasTrailEvent) && IsVanillaTrailType(effectiveTrailType))
            {
                return GameModeKind.VanillaTrail;
            }
            if (skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL)
                return GameModeKind.CustomTrail;
            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
                skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM &&
                coopTrailId == NoCoopTrail)
            {
                return GameModeKind.CustomGame;
            }
            return GameModeKind.Unknown;
        }

        internal static GameModeLaunchVariant ResolveLaunchVariant(
            GameModeKind kind,
            bool vanillaCustomized,
            int customizedTrailType,
            int customizedTrailId,
            bool vanillaCustomGameContext,
            ExternalCustomizedOrigin externalOrigin)
        {
            bool vanillaMatches = vanillaCustomized &&
                vanillaCustomGameContext &&
                customizedTrailId >= 0 &&
                (!externalOrigin.SupportsBuiltInOrigins ||
                 ExternalOriginMatchesKind(externalOrigin, kind)) &&
                ((kind == GameModeKind.VanillaTrail && IsVanillaTrailType(customizedTrailType)) ||
                 (kind == GameModeKind.SandsOfTime && IsSandsTrailType(customizedTrailType)));
            bool externalMatches =
                ExternalOriginMatchesKind(externalOrigin, kind) &&
                (kind != GameModeKind.CustomGame || externalOrigin.LaunchPending);
            if (!vanillaMatches && !externalMatches)
                return GameModeLaunchVariant.Standard;
            return externalMatches && externalOrigin.RestoredFromSave
                ? GameModeLaunchVariant.RestoredCustomizedSave
                : GameModeLaunchVariant.Customized;
        }

        private static bool IsVanillaTrailType(int value) =>
            value >= (int)GameTrailType.FirstEdition && value <= (int)GameTrailType.Extreme;

        private static bool IsSandsTrailType(int value) =>
            value >= (int)GameTrailType.SandsOne && value <= (int)GameTrailType.SandsEight;

        private static GameModeKind ResolveExternalOriginKind(int origin)
        {
            switch (origin)
            {
                case ExternalCustomizedOrigin.CustomTrail: return GameModeKind.CustomTrail;
                case ExternalCustomizedOrigin.CoopTrail: return GameModeKind.CoopTrail;
                case ExternalCustomizedOrigin.VanillaTrail: return GameModeKind.VanillaTrail;
                case ExternalCustomizedOrigin.SandsOfTime: return GameModeKind.SandsOfTime;
                default: return GameModeKind.Unknown;
            }
        }

        private static bool ExternalOriginMatchesKind(ExternalCustomizedOrigin origin, GameModeKind kind) =>
            (origin.Origin == ExternalCustomizedOrigin.CustomTrail && kind == GameModeKind.CustomTrail) ||
            (origin.Origin == ExternalCustomizedOrigin.CoopTrail && kind == GameModeKind.CoopTrail) ||
            (origin.Origin == ExternalCustomizedOrigin.VanillaTrail && kind == GameModeKind.VanillaTrail) ||
            (origin.Origin == ExternalCustomizedOrigin.SandsOfTime && kind == GameModeKind.SandsOfTime);

        internal static bool ExternalOriginMatchesEvidence(
            ExternalCustomizedOrigin origin,
            GameModeKind kind,
            int skirmishTrailType,
            int coopTrailId,
            int eventTrailType,
            bool vanillaCustomized,
            int customizedTrailType,
            int customizedTrailId)
        {
            if (kind == GameModeKind.CoopTrail && coopTrailId > NoCoopTrail)
                return origin.TrailId + 1 == coopTrailId;
            if (kind != GameModeKind.VanillaTrail && kind != GameModeKind.SandsOfTime)
                return true;

            int observedTrailType = eventTrailType >= 0 ? eventTrailType : skirmishTrailType;
            if (observedTrailType >= 0 && origin.TrailType != observedTrailType)
                return false;
            if (!vanillaCustomized)
                return true;
            return origin.TrailType == customizedTrailType &&
                origin.MissionId == customizedTrailId;
        }

        private static bool TryCaptureVanillaCustomizedTrail(out int trailType, out int trailId)
        {
#if SHARED_PRESET_TESTS
            trailType = NoGameValue;
            trailId = NoGameValue;
            return false;
#else
            trailType = FRONT_Multiplayer.customizedTrailType;
            trailId = FRONT_Multiplayer.customizedTrailID;
            return FRONT_Multiplayer.customizedTrail;
#endif
        }

        private static bool TryIsSandsOfTime(GameData gameData)
        {
            try
            {
                return gameData?.IsSandsOfTime() == true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCaptureCustomTrailRestart()
        {
#if SHARED_PRESET_TESTS
            return false;
#else
            if (!MainViewModel.viewModelLoaded)
                return false;
            try
            {
                return MainViewModel.Instance?.HUDIngameMenu?.restartSkirmishMapInfo?.customTrail == true;
            }
            catch
            {
                return false;
            }
#endif
        }

        private static ExternalCustomizedOrigin CaptureExternalCustomizedOrigin()
        {
#if SHARED_PRESET_TESTS
            return default;
#else
            try
            {
                Type api = Type.GetType(
                    "CustomCustomTrail.CustomCustomTrailLaunchOriginApi, CustomCustomTrail",
                    throwOnError: false);
                if (api == null)
                    return default;
                if (!TryReadStaticInt(api, "ApiVersion", out int apiVersion) ||
                    !TryReadStaticInt(api, "Origin", out int origin))
                {
                    return ExternalCustomizedOrigin.InvalidProvider;
                }
                if (apiVersion < MinimumOriginApiVersion || apiVersion > SupportedOriginApiVersion)
                    return ExternalCustomizedOrigin.InvalidProvider;
                if (origin == ExternalCustomizedOrigin.None)
                    return ExternalCustomizedOrigin.AvailableProvider(apiVersion >= 2);
                bool knownOrigin = origin == ExternalCustomizedOrigin.CustomTrail ||
                    origin == ExternalCustomizedOrigin.CoopTrail ||
                    (apiVersion >= 2 && (origin == ExternalCustomizedOrigin.VanillaTrail ||
                                         origin == ExternalCustomizedOrigin.SandsOfTime));
                if (!knownOrigin)
                    return ExternalCustomizedOrigin.InvalidProvider;
                bool launchPending = false;
                if (!TryReadStaticInt(api, "TrailType", out int trailType) ||
                    !TryReadStaticInt(api, "TrailId", out int trailId) ||
                    !TryReadStaticInt(api, "MissionId", out int missionId) ||
                    !TryReadStaticBool(api, "RestoredFromSave", out bool restoredFromSave) ||
                    (apiVersion >= 2 && !TryReadStaticBool(api, "LaunchPending", out launchPending)))
                {
                    return ExternalCustomizedOrigin.InvalidProvider;
                }
                var result = new ExternalCustomizedOrigin(
                    origin,
                    trailType,
                    trailId,
                    missionId,
                    restoredFromSave,
                    launchPending,
                    supportsBuiltInOrigins: apiVersion >= 2);
                if ((result.Origin == ExternalCustomizedOrigin.CustomTrail &&
                        (result.MissionId < FirstMissionId ||
                         result.TrailId < FirstCustomTrailId || result.TrailId > LastCustomTrailId)) ||
                    (result.Origin == ExternalCustomizedOrigin.CoopTrail &&
                        (result.MissionId < FirstMissionId ||
                         result.TrailId < FirstCoopTrailId || result.TrailId > LastCoopTrailId ||
                         result.MissionId > LastCoopMissionId)) ||
                    (result.Origin == ExternalCustomizedOrigin.VanillaTrail &&
                        (!IsVanillaTrailType(result.TrailType) || result.TrailId < 0 || result.MissionId < 0)) ||
                    (result.Origin == ExternalCustomizedOrigin.SandsOfTime &&
                        (!IsSandsTrailType(result.TrailType) || result.TrailId < 0 || result.MissionId < 0)))
                {
                    return ExternalCustomizedOrigin.InvalidProvider;
                }
                return result;
            }
            catch
            {
                // CustomCustomTrail is optional; invalid providers must never enable gameplay mods.
                return ExternalCustomizedOrigin.InvalidProvider;
            }
#endif
        }

        private static bool TryReadStaticInt(Type type, string name, out int result)
        {
            result = NoGameValue;
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.GetIndexParameters().Length != 0)
                return false;
            object value = property.GetValue(null);
            if (value == null)
                return false;
            result = Convert.ToInt32(value);
            return true;
        }

        private static bool TryReadStaticBool(Type type, string name, out bool result)
        {
            result = false;
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool) ||
                property.GetIndexParameters().Length != 0)
            {
                return false;
            }
            result = (bool)property.GetValue(null);
            return true;
        }

        public static bool IsRealMultiplayer(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsRealMultiplayer;

        // Keep the legacy property contract while deriving it from Vanilla's named subtype enum.
        public static bool IsSingleplayerSkirmish(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerSkirmish;

        public static bool IsSingleplayerTrail(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerTrail;

        // This broader check also covers future/utility subtypes initialized by Vanilla as skirmish mode.
        public static bool IsSingleplayerSkirmishMode(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerSkirmishMode;

        public static bool IsMapEditor()
        {
            try
            {
                if (GamePlayerManagerAPI.Instance?.IsInMapEditor() == true)
                    return true;
            }
            catch
            {
                // The Script Extender singleton can be unavailable during early plugin startup.
            }

            // MainViewModel.Instance constructs the ViewModel. Reading it before the
            // game's own loaded marker is set can therefore fail inside Vanilla code.
            if (!MainViewModel.viewModelLoaded)
                return false;

            try
            {
                return MainViewModel.Instance?.IsMapEditorMode ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    internal readonly struct ExternalCustomizedOrigin
    {
        internal const int None = 0;
        internal const int CustomTrail = 1;
        internal const int CoopTrail = 2;
        internal const int VanillaTrail = 3;
        internal const int SandsOfTime = 4;

        internal static ExternalCustomizedOrigin InvalidProvider =>
            new ExternalCustomizedOrigin(-1, -1, -1, -1, false, false, isInvalid: true);

        internal static ExternalCustomizedOrigin AvailableProvider(bool supportsBuiltInOrigins) =>
            new ExternalCustomizedOrigin(
                None, -1, -1, -1, false, false,
                supportsBuiltInOrigins: supportsBuiltInOrigins);

        internal ExternalCustomizedOrigin(
            int origin,
            int trailType,
            int trailId,
            int missionId,
            bool restoredFromSave,
            bool launchPending = false,
            bool isInvalid = false,
            bool supportsBuiltInOrigins = false)
        {
            Origin = origin;
            TrailType = trailType;
            TrailId = trailId;
            MissionId = missionId;
            RestoredFromSave = restoredFromSave;
            LaunchPending = launchPending;
            IsInvalid = isInvalid;
            SupportsBuiltInOrigins = supportsBuiltInOrigins;
        }

        internal int Origin { get; }
        internal int TrailType { get; }
        internal int TrailId { get; }
        internal int MissionId { get; }
        internal bool RestoredFromSave { get; }
        internal bool LaunchPending { get; }
        internal bool IsInvalid { get; }
        internal bool SupportsBuiltInOrigins { get; }
    }

    internal readonly struct PlayerIdentityResolution
    {
        internal PlayerIdentityResolution(int playerId, bool isResolved, string error, string diagnostic)
        {
            PlayerId = playerId;
            IsResolved = isResolved;
            Error = error ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal int PlayerId { get; }
        internal bool IsResolved { get; }
        internal string Error { get; }
        internal string Diagnostic { get; }
    }

    internal static class PlayerIdentityHelper
    {
        // SCRIPT EXTENDER BUG WORKAROUND: the GameNetworkAPI local/Steam-ID methods
        // can expose provisional lobby order instead of Vanilla's final player slot.
        // Remove this multi-source resolver only after the upstream behavior is
        // demonstrably fixed. Revalidate all source semantics after every Extender update.
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;

        internal static PlayerIdentityResolution ResolveLocalPlayerId(
            bool realMultiplayer,
            bool hasInGameHumanRoster,
            int nativePlayerId,
            int gameMemberPlayerId,
            int lobbyPlayerId,
            int networkLobbyPlayerId)
        {
            bool nativeValid = IsValidPlayerId(nativePlayerId);
            bool gameMemberValid = IsValidPlayerId(gameMemberPlayerId);
            bool lobbyValid = IsValidPlayerId(lobbyPlayerId);
            bool networkValid = IsValidPlayerId(networkLobbyPlayerId);

            if (realMultiplayer && hasInGameHumanRoster)
            {
                if (nativeValid && gameMemberValid && nativePlayerId != gameMemberPlayerId)
                {
                    return Failure(
                        $"Authoritative local player ID mismatch: native={nativePlayerId}, gameMember={gameMemberPlayerId}, " +
                        $"lobby={lobbyPlayerId}, networkLobby={networkLobbyPlayerId}.");
                }

                int authoritative = nativeValid ? nativePlayerId : gameMemberPlayerId;
                if (!IsValidPlayerId(authoritative))
                {
                    return Failure(
                        $"No authoritative local player ID is available in the active multiplayer roster: " +
                        $"native={nativePlayerId}, gameMember={gameMemberPlayerId}, lobby={lobbyPlayerId}, " +
                        $"networkLobby={networkLobbyPlayerId}.");
                }
                if (lobbyValid && lobbyPlayerId != authoritative)
                {
                    return Failure(
                        $"Final lobby mapping disagrees with the authoritative local player ID: " +
                        $"authoritative={authoritative}, lobby={lobbyPlayerId}, networkLobby={networkLobbyPlayerId}.");
                }

                return Success(
                    authoritative,
                    networkValid && networkLobbyPlayerId != authoritative
                        ? $"Lobby-order player ID differs from the final in-game slot: " +
                          $"networkLobby={networkLobbyPlayerId}, final={authoritative}."
                        : string.Empty);
            }

            if (realMultiplayer)
            {
                if (lobbyValid)
                {
                    return Success(
                        lobbyPlayerId,
                        networkValid && networkLobbyPlayerId != lobbyPlayerId
                            ? $"Script Extender lobby-order player ID differs from Vanilla's final lobby mapping: " +
                              $"networkLobby={networkLobbyPlayerId}, finalLobby={lobbyPlayerId}."
                            : string.Empty);
                }
                if (networkValid)
                    return Success(networkLobbyPlayerId, "Only the provisional lobby-order player ID is available.");
                return Failure(
                    $"No local multiplayer player ID is available yet: lobby={lobbyPlayerId}, " +
                    $"networkLobby={networkLobbyPlayerId}.");
            }

            if (nativeValid)
                return Success(nativePlayerId, string.Empty);
            if (gameMemberValid)
                return Success(gameMemberPlayerId, string.Empty);
            if (lobbyValid)
                return Success(lobbyPlayerId, string.Empty);
            if (networkValid)
                return Success(networkLobbyPlayerId, string.Empty);
            return Failure("No valid local player ID is available.");
        }

        internal static PlayerIdentityResolution ResolvePlayerIdForSteamId(
            ulong steamId,
            IReadOnlyDictionary<int, ulong> playersById)
        {
            if (steamId == 0)
                return Failure("The requested Steam identity is invalid.");

            var normalized = new Dictionary<int, ulong>();
            foreach (KeyValuePair<int, ulong> player in
                playersById ?? new Dictionary<int, ulong>())
            {
                if (!TryAddPlayer(normalized, player.Key, player.Value, out string error))
                    return Failure(error);
            }

            int[] matches = normalized
                .Where(player => player.Value == steamId)
                .Select(player => player.Key)
                .ToArray();
            if (matches.Length != 1)
            {
                return Failure(
                    matches.Length == 0
                        ? $"Steam identity {steamId} is not part of the resolved human roster."
                        : $"Steam identity {steamId} is assigned to multiple player slots.");
            }
            return Success(matches[0], string.Empty);
        }

        internal static PlayerIdentityResolution ResolveAuthenticatedPerPlayerTarget(
            ulong senderSteamId,
            int payloadPlayerId,
            IReadOnlyDictionary<int, ulong> playersById)
        {
            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
                senderSteamId,
                playersById);
            if (!resolution.IsResolved || resolution.PlayerId == payloadPlayerId)
                return resolution;
            return Success(
                resolution.PlayerId,
                $"The per-player payload claimed slot {payloadPlayerId}, but authenticated " +
                $"Steam identity {senderSteamId} belongs to final slot {resolution.PlayerId}.");
        }

#if !SHARED_PRESET_TESTS
        internal static PlayerIdentityResolution CaptureLocalPlayerId(
            bool preferInGameRoster) =>
            CaptureLocalPlayerId(
                GameModeHelper.IsRealMultiplayer(),
                preferInGameRoster);

        internal static PlayerIdentityResolution CaptureLocalPlayerId(
            bool realMultiplayer,
            bool preferInGameRoster)
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            ulong localSteamId = 0;
            try
            {
                localSteamId = SteamUser.GetSteamID().m_SteamID;
            }
            catch
            {
                // Steam can be unavailable during early singleplayer initialization.
            }

            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
                .Where(member => member != null && !member.kicked && !member.skirmishAI)
                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
            int gameMemberPlayerId = humanGameMembers
                .Where(member => localSteamId != 0 && member.steamID == localSteamId)
                .Select(member => member.playerID)
                .FirstOrDefault();

            int nativePlayerId = 0;
            try
            {
                nativePlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            }
            catch
            {
                // Native player resources are not guaranteed to exist in the lobby.
            }

            int lobbyPlayerId = 0;
            try
            {
                if (localSteamId != 0 && platform?.activeLobby != null)
                    lobbyPlayerId = platform.activeLobby.getThisPlayerFromSteamID(localSteamId);
            }
            catch
            {
                // The final lobby mapping can still be under construction.
            }

            int networkLobbyPlayerId = 0;
            try
            {
                networkLobbyPlayerId = GameNetworkAPI.GetLocalPlayerId();
            }
            catch
            {
                // This source is only a final fallback and may be unavailable early.
            }

            return ResolveLocalPlayerId(
                realMultiplayer,
                preferInGameRoster && humanGameMembers.Length > 0,
                nativePlayerId,
                gameMemberPlayerId,
                lobbyPlayerId,
                networkLobbyPlayerId);
        }

        internal static PlayerIdentityResolution CapturePlayerIdForSteamId(
            ulong steamId,
            bool preferInGameRoster)
        {
            if (!TryCaptureHumanRoster(
                    preferInGameRoster,
                    out Dictionary<int, ulong> playersById,
                    out string error,
                    out string diagnostic))
                return Failure(error);

            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
                steamId,
                playersById);
            if (!resolution.IsResolved)
                return resolution;

            if (preferInGameRoster)
            {
                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
                int vanillaPlayerId = 0;
                int networkLobbyPlayerId = 0;
                try
                {
                    if (lobby != null)
                        vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
                }
                catch
                {
                    // The lobby can disappear while the in-game roster remains authoritative.
                }
                try
                {
                    networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
                        new CSteamID(steamId));
                }
                catch
                {
                    // Lobby order is diagnostic-only after the game roster exists.
                }

                if (IsValidPlayerId(vanillaPlayerId) &&
                    vanillaPlayerId != resolution.PlayerId)
                {
                    return Failure(
                        $"Final lobby mapping disagrees with the authoritative in-game player slot for " +
                        $"Steam identity {steamId}: gameMember={resolution.PlayerId}, lobby={vanillaPlayerId}, " +
                        $"networkLobby={networkLobbyPlayerId}.");
                }
                if (IsValidPlayerId(networkLobbyPlayerId) &&
                    networkLobbyPlayerId != resolution.PlayerId)
                {
                    diagnostic =
                        $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
                        $"{steamId}: networkLobby={networkLobbyPlayerId}, final={resolution.PlayerId}.";
                }
            }
            return Success(resolution.PlayerId, diagnostic);
        }

        internal static string CaptureProvisionalPlayerIdDiagnostic(
            ulong steamId,
            int finalPlayerId,
            bool inGame)
        {
            if (steamId == 0 || !IsValidPlayerId(finalPlayerId))
                return string.Empty;

            try
            {
                int provisionalPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
                    new CSteamID(steamId));
                if (!IsValidPlayerId(provisionalPlayerId) ||
                    provisionalPlayerId == finalPlayerId)
                {
                    return string.Empty;
                }

                return inGame
                    ? $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
                      $"{steamId}: networkLobby={provisionalPlayerId}, final={finalPlayerId}."
                    : $"Script Extender lobby-order player ID differs from Vanilla's final lobby mapping " +
                      $"for Steam identity {steamId}: networkLobby={provisionalPlayerId}, " +
                      $"finalLobby={finalPlayerId}.";
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            out Dictionary<int, ulong> playersById,
            out string error) =>
            TryCaptureHumanRoster(
                preferInGameRoster,
                requireAuthoritativeLobbyRoster: false,
                out playersById,
                out error,
                out _);

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            out Dictionary<int, ulong> playersById,
            out string error,
            out string diagnostic) =>
            TryCaptureHumanRoster(
                preferInGameRoster,
                requireAuthoritativeLobbyRoster: false,
                out playersById,
                out error,
                out diagnostic);

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            bool requireAuthoritativeLobbyRoster,
            out Dictionary<int, ulong> playersById,
            out string error,
            out string diagnostic)
        {
            playersById = new Dictionary<int, ulong>();
            diagnostic = string.Empty;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
                .Where(member => member != null && !member.kicked && !member.skirmishAI)
                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
            if (preferInGameRoster)
            {
                if (humanGameMembers.Length == 0)
                {
                    error = "The active in-game human roster is unavailable.";
                    return false;
                }
                foreach (Platform_Multiplayer.MPGameMember member in humanGameMembers)
                {
                    if (!TryAddPlayer(playersById, member.playerID, member.steamID, out error))
                        return false;
                }
                error = string.Empty;
                return true;
            }

            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (lobby?.members == null)
            {
                error = "The active human lobby roster is unavailable.";
                return false;
            }

            var diagnostics = new List<string>();
            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || member.dummyToBeKicked ||
                    (member.SkirmishMember && !member.SkirmishHumanMember))
                    continue;
                ulong steamId = member.id.m_SteamID;
                int vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
                int networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
                if (requireAuthoritativeLobbyRoster && !IsValidPlayerId(vanillaPlayerId))
                {
                    error =
                        $"Vanilla has not assigned a final player slot to lobby member {steamId} yet.";
                    return false;
                }
                int playerId = IsValidPlayerId(vanillaPlayerId)
                    ? vanillaPlayerId
                    : networkLobbyPlayerId;
                if (IsValidPlayerId(vanillaPlayerId) &&
                    IsValidPlayerId(networkLobbyPlayerId) &&
                    vanillaPlayerId != networkLobbyPlayerId)
                {
                    diagnostics.Add(
                        $"steamId={steamId}: networkLobby={networkLobbyPlayerId}, finalLobby={vanillaPlayerId}");
                }
                else if (!IsValidPlayerId(vanillaPlayerId) &&
                         IsValidPlayerId(networkLobbyPlayerId))
                {
                    diagnostics.Add(
                        $"steamId={steamId}: only provisional networkLobby={networkLobbyPlayerId} is available");
                }
                if (!TryAddPlayer(playersById, playerId, steamId, out error))
                    return false;
            }

            if (playersById.Count == 0)
            {
                error = "The active lobby contains no resolved human players.";
                return false;
            }
            diagnostic = diagnostics.Count == 0
                ? string.Empty
                : "Lobby player-ID source differences: " + string.Join("; ", diagnostics) + ".";
            error = string.Empty;
            return true;
        }
#endif

        private static bool TryAddPlayer(
            IDictionary<int, ulong> playersById,
            int playerId,
            ulong steamId,
            out string error)
        {
            if (!IsValidPlayerId(playerId) || steamId == 0)
            {
                error = $"A human player has an invalid final identity: playerId={playerId}, steamId={steamId}.";
                return false;
            }
            if (playersById.TryGetValue(playerId, out ulong existingSteamId) && existingSteamId != steamId)
            {
                error = $"Final player slot {playerId} is assigned to multiple Steam identities.";
                return false;
            }
            if (playersById.Any(player => player.Key != playerId && player.Value == steamId))
            {
                error = $"Steam identity {steamId} is assigned to multiple final player slots.";
                return false;
            }
            playersById[playerId] = steamId;
            error = string.Empty;
            return true;
        }

        private static PlayerIdentityResolution Success(int playerId, string diagnostic) =>
            new PlayerIdentityResolution(playerId, true, string.Empty, diagnostic);

        private static PlayerIdentityResolution Failure(string error) =>
            new PlayerIdentityResolution(0, false, error, string.Empty);

        private static bool IsValidPlayerId(int playerId) =>
            playerId >= FirstPlayerId && playerId <= LastPlayerId;
    }

    internal readonly struct GameModeSnapshot
    {
        public GameModeSnapshot(
            bool isRealMultiplayer,
            bool isSingleplayerSkirmishMode,
            bool isSingleplayerSkirmish,
            bool isSingleplayerTrail,
            bool isMapEditor,
            bool multiplayerSave,
            bool directorAvailable,
            bool directorMultiplayer,
            bool directorSkirmish,
            bool lowLevelNetworked,
            bool platformMultiplayer,
            int lobbyMembers,
            int realLobbyMembers,
            int skirmishLobbyMembers,
            int gameMembers,
            int realNetworkGameMembers,
            int gameType,
            int skirmishGameType,
            int skirmishTrailType,
            int coopTrailId,
            GameModeKind kind,
            GameModeLaunchVariant launchVariant,
            int campaignMapId,
            int eventTrailType,
            int customizedTrailId,
            int customizedMissionId,
            int customizedOriginKind,
            bool hasConflictingCustomizedOrigin)
        {
            IsRealMultiplayer = isRealMultiplayer;
            IsSingleplayerSkirmishMode = isSingleplayerSkirmishMode;
            IsSingleplayerSkirmish = isSingleplayerSkirmish;
            IsSingleplayerTrail = isSingleplayerTrail;
            IsMapEditor = isMapEditor;
            MultiplayerSave = multiplayerSave;
            DirectorAvailable = directorAvailable;
            DirectorMultiplayer = directorMultiplayer;
            DirectorSkirmish = directorSkirmish;
            LowLevelNetworked = lowLevelNetworked;
            PlatformMultiplayer = platformMultiplayer;
            LobbyMembers = lobbyMembers;
            RealLobbyMembers = realLobbyMembers;
            SkirmishLobbyMembers = skirmishLobbyMembers;
            GameMembers = gameMembers;
            RealNetworkGameMembers = realNetworkGameMembers;
            GameType = gameType;
            SkirmishGameType = skirmishGameType;
            SkirmishTrailType = skirmishTrailType;
            CoopTrailId = coopTrailId;
            Kind = kind;
            LaunchVariant = launchVariant;
            CampaignMapId = campaignMapId;
            EventTrailType = eventTrailType;
            CustomizedTrailId = customizedTrailId;
            CustomizedMissionId = customizedMissionId;
            CustomizedOriginKind = customizedOriginKind;
            HasConflictingCustomizedOrigin = hasConflictingCustomizedOrigin;
        }

        public bool IsRealMultiplayer { get; }
        public bool IsSingleplayerSkirmishMode { get; }
        public bool IsSingleplayerSkirmish { get; }
        public bool IsSingleplayerTrail { get; }
        public bool IsMapEditor { get; }
        public bool MultiplayerSave { get; }
        public bool DirectorAvailable { get; }
        public bool DirectorMultiplayer { get; }
        public bool DirectorSkirmish { get; }
        public bool LowLevelNetworked { get; }
        public bool PlatformMultiplayer { get; }
        public int LobbyMembers { get; }
        public int RealLobbyMembers { get; }
        public int SkirmishLobbyMembers { get; }
        public int GameMembers { get; }
        public int RealNetworkGameMembers { get; }
        public int GameType { get; }
        public int SkirmishGameType { get; }
        public int SkirmishTrailType { get; }
        public int CoopTrailId { get; }
        public GameModeKind Kind { get; }
        public GameModeLaunchVariant LaunchVariant { get; }
        public bool IsCustomized => LaunchVariant != GameModeLaunchVariant.Standard;
        public bool IsMissionContent =>
            Kind == GameModeKind.Campaign ||
            Kind == GameModeKind.StandaloneMission ||
            Kind == GameModeKind.VanillaTrail ||
            Kind == GameModeKind.CustomTrail ||
            Kind == GameModeKind.CoopTrail ||
            Kind == GameModeKind.SandsOfTime;
        public bool AllowsCustomGameMods =>
            !HasConflictingCustomizedOrigin && GameModeHelper.AllowsCustomGameMods(Kind, LaunchVariant);
        public bool AllowsRegularGameplayMods =>
            !HasConflictingCustomizedOrigin &&
            GameModeHelper.AllowsRegularGameplayMods(Kind, LaunchVariant);
        public int CampaignMapId { get; }
        public int EventTrailType { get; }
        public int CustomizedTrailId { get; }
        public int CustomizedMissionId { get; }
        public int CustomizedOriginKind { get; }
        public bool HasConflictingCustomizedOrigin { get; }

#if SHARED_PRESET_TESTS
        internal GameModeSnapshot WithModeEvidenceForTests(
            GameModeKind kind,
            GameModeLaunchVariant launchVariant,
            int eventTrailType,
            bool hasConflictingCustomizedOrigin = false) =>
            new GameModeSnapshot(
                IsRealMultiplayer,
                IsSingleplayerSkirmishMode,
                IsSingleplayerSkirmish,
                IsSingleplayerTrail,
                IsMapEditor,
                MultiplayerSave,
                DirectorAvailable,
                DirectorMultiplayer,
                DirectorSkirmish,
                LowLevelNetworked,
                PlatformMultiplayer,
                LobbyMembers,
                RealLobbyMembers,
                SkirmishLobbyMembers,
                GameMembers,
                RealNetworkGameMembers,
                GameType,
                SkirmishGameType,
                SkirmishTrailType,
                CoopTrailId,
                kind,
                launchVariant,
                CampaignMapId,
                eventTrailType,
                CustomizedTrailId,
                CustomizedMissionId,
                CustomizedOriginKind,
                hasConflictingCustomizedOrigin);
#endif

        public string ToDiagnosticString()
        {
            return
                $"realMultiplayer={IsRealMultiplayer}, singleplayerSkirmishMode={IsSingleplayerSkirmishMode}, " +
                $"singleplayerSkirmish={IsSingleplayerSkirmish}, singleplayerTrail={IsSingleplayerTrail}, " +
                $"mapEditor={IsMapEditor}, multiplayerSave={MultiplayerSave}, directorAvailable={DirectorAvailable}, " +
                $"directorMultiplayer={DirectorMultiplayer}, directorSkirmish={DirectorSkirmish}, " +
                $"lowLevelNetworked={LowLevelNetworked}, platformMultiplayer={PlatformMultiplayer}, " +
                $"lobbyMembers={LobbyMembers}, realLobbyMembers={RealLobbyMembers}, " +
                $"skirmishLobbyMembers={SkirmishLobbyMembers}, " +
                $"gameMembers={GameMembers}, realNetworkGameMembers={RealNetworkGameMembers}, " +
                $"gameType={GameType}, skirmishGameType={SkirmishGameType}, skirmishTrailType={SkirmishTrailType}, " +
                $"coopTrailId={CoopTrailId}, kind={Kind}, launchVariant={LaunchVariant}, " +
                $"allowsCustomGameMods={AllowsCustomGameMods}, " +
                $"allowsRegularGameplayMods={AllowsRegularGameplayMods}, campaignMapId={CampaignMapId}, " +
                $"eventTrailType={EventTrailType}, customizedTrailId={CustomizedTrailId}, " +
                $"customizedMissionId={CustomizedMissionId}, customizedOriginKind={CustomizedOriginKind}, " +
                $"conflictingCustomizedOrigin={HasConflictingCustomizedOrigin}";
        }
    }
}
