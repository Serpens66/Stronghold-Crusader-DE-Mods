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
    /// <summary>GameModeKind in the centralized mission policy contract.</summary>
    public enum GameModeKind
    {
        /// <summary>Unknown.</summary>
        Unknown,
        /// <summary>MapEditor.</summary>
        MapEditor,
        /// <summary>Campaign.</summary>
        Campaign,
        /// <summary>StandaloneMission.</summary>
        StandaloneMission,
        /// <summary>CustomGame.</summary>
        CustomGame,
        /// <summary>VanillaTrail.</summary>
        VanillaTrail,
        /// <summary>CustomTrail.</summary>
        CustomTrail,
        /// <summary>CoopTrail.</summary>
        CoopTrail,
        /// <summary>SandsOfTime.</summary>
        SandsOfTime,
        /// <summary>Tutorial.</summary>
        Tutorial,
    }

    /// <summary>GameModeLaunchVariant in the centralized mission policy contract.</summary>
    public enum GameModeLaunchVariant
    {
        /// <summary>Standard.</summary>
        Standard,
        /// <summary>Customized.</summary>
        Customized,
        /// <summary>RestoredCustomizedSave.</summary>
        RestoredCustomizedSave,
    }

    /// <summary>GameTrailType in the centralized mission policy contract.</summary>
    public enum GameTrailType
    {
        /// <summary>FirstEdition.</summary>
        FirstEdition = 0,
        /// <summary>Warchest.</summary>
        Warchest = 1,
        /// <summary>Extreme.</summary>
        Extreme = 2,
        /// <summary>SandsOne.</summary>
        SandsOne = 11,
        /// <summary>SandsTwo.</summary>
        SandsTwo = 12,
        /// <summary>SandsThree.</summary>
        SandsThree = 13,
        /// <summary>SandsFour.</summary>
        SandsFour = 14,
        /// <summary>SandsFive.</summary>
        SandsFive = 15,
        /// <summary>SandsSix.</summary>
        SandsSix = 16,
        /// <summary>SandsSeven.</summary>
        SandsSeven = 17,
        /// <summary>SandsEight.</summary>
        SandsEight = 18,
    }

    /// <summary>GameModeHelper in the centralized mission policy contract.</summary>
    public static class GameModeHelper
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

        /// <summary>Reads the central current/pending mission snapshot. The legacy save argument does not override authoritative evidence.</summary>
        public static GameModeSnapshot Capture(bool multiplayerSave = false) =>
            APIShared.MissionLifecycleService.Snapshot;

        internal static GameModeSnapshot CaptureMission(bool multiplayer, bool restoredSave, int campaign, int trail,
            bool editor, EngineInterface.LoadMapReturnData? data, GameModeKind intent, int? coopTrail = null) =>
            CaptureCore(multiplayer && restoredSave, campaign, trail, editor, data, intent, multiplayer, coopTrail);

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
            bool editorLoad, EngineInterface.LoadMapReturnData? data = null, GameModeKind intent = GameModeKind.Unknown,
            bool realMultiplayerOverride = false, int? knownCoopTrail = null)
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
            // Entry-point evidence is authoritative. Old rosters and mode flags can
            // survive a transition and must not turn a new local game into multiplayer.
            bool platformMultiplayer = GameNetworkAPI.IsMultiplayerGame();
            bool realMultiplayer = realMultiplayerOverride;

            int gameType = data?.game_type ?? (intent == GameModeKind.Campaign ? (int)Enums.eGameTypeModes.GAMETYPE_CAMPAIGN :
                intent == GameModeKind.Tutorial ? (int)Enums.eGameTypeModes.GAMETYPE_TUTORIAL :
                intent == GameModeKind.StandaloneMission ? (int)Enums.eGameTypeModes.GAMETYPE_MAP :
                intent == GameModeKind.MapEditor ? (int)Enums.eGameTypeModes.GAMETYPE_BUILDER :
                intent != GameModeKind.Unknown ? (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER : NoGameValue);
            int skirmishGameType = data?.skirmishGameType ?? (intent == GameModeKind.CustomTrail ?
                (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL : intent == GameModeKind.CustomGame ?
                (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM : NoGameValue);
            int skirmishTrailType = data?.skirmishTrail ?? eventTrailType;
            int coopTrailId = data?.coopTrailID ?? knownCoopTrail ?? NoCoopTrail;
            bool mapEditor = editorLoad;
            bool sandsOfTime = intent == GameModeKind.SandsOfTime || IsSandsTrailType(skirmishTrailType);
            bool customTrailRestart = intent == GameModeKind.CustomTrail;
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
            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_TUTORIAL)
                return GameModeKind.Tutorial;
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

        private static ExternalCustomizedOrigin CaptureExternalCustomizedOrigin()
        {
#if SHARED_PRESET_TESTS
            return default;
#else
            try
            {
                string[] providerTypes =
                {
                    "BugfixesAndQoL.TrailCustomizationLaunchOriginApi, BugfixesAndQoL",
                    "ExtendedData.ExtendedDataLaunchOriginApi, ExtendedData",
                };
                ExternalCustomizedOrigin active = default;
                bool hasActive = false;
                bool providerAvailable = false;
                bool supportsBuiltInOrigins = false;
                foreach (string providerType in providerTypes)
                {
                    Type api = Type.GetType(providerType, throwOnError: false);
                    if (api == null)
                        continue;
                    providerAvailable = true;
                    ExternalCustomizedOrigin candidate = CaptureExternalCustomizedOrigin(api);
                    if (candidate.IsInvalid)
                        return ExternalCustomizedOrigin.InvalidProvider;
                    supportsBuiltInOrigins |= candidate.SupportsBuiltInOrigins;
                    if (candidate.Origin == ExternalCustomizedOrigin.None)
                        continue;
                    if (hasActive)
                        return ExternalCustomizedOrigin.InvalidProvider;
                    active = candidate;
                    hasActive = true;
                }
                return hasActive
                    ? active
                    : providerAvailable
                        ? ExternalCustomizedOrigin.AvailableProvider(supportsBuiltInOrigins)
                        : default;
            }
            catch
            {
                // Optional providers must never enable gameplay mods when their contracts fail.
                return ExternalCustomizedOrigin.InvalidProvider;
            }
#endif
        }

        private static ExternalCustomizedOrigin CaptureExternalCustomizedOrigin(Type api)
        {
            try
            {
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
                bool hasLaunchPending = TryReadStaticBool(api, "LaunchPending", out launchPending);
                if (!TryReadStaticInt(api, "TrailType", out int trailType) ||
                    !TryReadStaticInt(api, "TrailId", out int trailId) ||
                    !TryReadStaticInt(api, "MissionId", out int missionId) ||
                    !TryReadStaticBool(api, "RestoredFromSave", out bool restoredFromSave) ||
                    (apiVersion >= 2 && !hasLaunchPending))
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
                // Providers are optional; malformed reflection surfaces fail closed.
                return ExternalCustomizedOrigin.InvalidProvider;
            }
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

        /// <summary>Reads mission network identity, or lobby authority outside a mission. This alone never grants gameplay permission.</summary>
        public static bool IsRealMultiplayer(bool multiplayerSave = false) =>
            APIShared.MissionLifecycleService.HasContext ? Capture().IsRealMultiplayer : GameNetworkAPI.IsMultiplayerGame();

        // Keep the legacy property contract while deriving it from Vanilla's named subtype enum.
        /// <summary>IsSingleplayerSkirmish in the centralized mission policy contract.</summary>
        public static bool IsSingleplayerSkirmish(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerSkirmish;

        /// <summary>IsSingleplayerTrail in the centralized mission policy contract.</summary>
        public static bool IsSingleplayerTrail(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerTrail;

        // This broader check also covers future/utility subtypes initialized by Vanilla as skirmish mode.
        /// <summary>IsSingleplayerSkirmishMode in the centralized mission policy contract.</summary>
        public static bool IsSingleplayerSkirmishMode(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerSkirmishMode;

        /// <summary>IsMapEditor in the centralized mission policy contract.</summary>
        public static bool IsMapEditor() => Capture().IsMapEditor;

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

    /// <summary>GameModeSnapshot in the centralized mission policy contract.</summary>
    public readonly struct GameModeSnapshot
    {
        /// <summary>GameModeSnapshot in the centralized mission policy contract.</summary>
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

        /// <summary>IsRealMultiplayer in the centralized mission policy contract.</summary>
        public bool IsRealMultiplayer { get; }
        /// <summary>IsSingleplayerSkirmishMode in the centralized mission policy contract.</summary>
        public bool IsSingleplayerSkirmishMode { get; }
        /// <summary>IsSingleplayerSkirmish in the centralized mission policy contract.</summary>
        public bool IsSingleplayerSkirmish { get; }
        /// <summary>IsSingleplayerTrail in the centralized mission policy contract.</summary>
        public bool IsSingleplayerTrail { get; }
        /// <summary>IsMapEditor in the centralized mission policy contract.</summary>
        public bool IsMapEditor { get; }
        /// <summary>MultiplayerSave in the centralized mission policy contract.</summary>
        public bool MultiplayerSave { get; }
        /// <summary>DirectorAvailable in the centralized mission policy contract.</summary>
        public bool DirectorAvailable { get; }
        /// <summary>DirectorMultiplayer in the centralized mission policy contract.</summary>
        public bool DirectorMultiplayer { get; }
        /// <summary>DirectorSkirmish in the centralized mission policy contract.</summary>
        public bool DirectorSkirmish { get; }
        /// <summary>LowLevelNetworked in the centralized mission policy contract.</summary>
        public bool LowLevelNetworked { get; }
        /// <summary>PlatformMultiplayer in the centralized mission policy contract.</summary>
        public bool PlatformMultiplayer { get; }
        /// <summary>LobbyMembers in the centralized mission policy contract.</summary>
        public int LobbyMembers { get; }
        /// <summary>RealLobbyMembers in the centralized mission policy contract.</summary>
        public int RealLobbyMembers { get; }
        /// <summary>SkirmishLobbyMembers in the centralized mission policy contract.</summary>
        public int SkirmishLobbyMembers { get; }
        /// <summary>GameMembers in the centralized mission policy contract.</summary>
        public int GameMembers { get; }
        /// <summary>RealNetworkGameMembers in the centralized mission policy contract.</summary>
        public int RealNetworkGameMembers { get; }
        /// <summary>GameType in the centralized mission policy contract.</summary>
        public int GameType { get; }
        /// <summary>SkirmishGameType in the centralized mission policy contract.</summary>
        public int SkirmishGameType { get; }
        /// <summary>Native trail type: 0..2 Vanilla, 11..18 Sands of Time; -1 when absent.</summary>
        public int SkirmishTrailType { get; }
        /// <summary>Native one-based Coop trail ID (1..4); zero when absent.</summary>
        public int CoopTrailId { get; }
        /// <summary>Kind in the centralized mission policy contract.</summary>
        public GameModeKind Kind { get; }
        /// <summary>LaunchVariant in the centralized mission policy contract.</summary>
        public GameModeLaunchVariant LaunchVariant { get; }
        /// <summary>IsCustomized in the centralized mission policy contract.</summary>
        public bool IsCustomized => LaunchVariant != GameModeLaunchVariant.Standard;
        /// <summary>IsMissionContent in the centralized mission policy contract.</summary>
        public bool IsMissionContent =>
            Kind == GameModeKind.Campaign ||
            Kind == GameModeKind.StandaloneMission ||
            Kind == GameModeKind.VanillaTrail ||
            Kind == GameModeKind.CustomTrail ||
            Kind == GameModeKind.CoopTrail ||
            Kind == GameModeKind.SandsOfTime;
        /// <summary>AllowsCustomGameMods in the centralized mission policy contract.</summary>
        public bool AllowsCustomGameMods =>
            !HasConflictingCustomizedOrigin && GameModeHelper.AllowsCustomGameMods(Kind, LaunchVariant);
        /// <summary>AllowsRegularGameplayMods in the centralized mission policy contract.</summary>
        public bool AllowsRegularGameplayMods =>
            !HasConflictingCustomizedOrigin &&
            GameModeHelper.AllowsRegularGameplayMods(Kind, LaunchVariant);
        /// <summary>Positive native campaign map ID supplied to the loader; zero if not captured. This is distinct from the zero-based mission index.</summary>
        public int CampaignMapId { get; }
        /// <summary>EventTrailType in the centralized mission policy contract.</summary>
        public int EventTrailType { get; }
        /// <summary>CustomizedTrailId in the centralized mission policy contract.</summary>
        public int CustomizedTrailId { get; }
        /// <summary>CustomizedMissionId in the centralized mission policy contract.</summary>
        public int CustomizedMissionId { get; }
        /// <summary>CustomizedOriginKind in the centralized mission policy contract.</summary>
        public int CustomizedOriginKind { get; }
        /// <summary>HasConflictingCustomizedOrigin in the centralized mission policy contract.</summary>
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

        /// <summary>ToDiagnosticString in the centralized mission policy contract.</summary>
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
