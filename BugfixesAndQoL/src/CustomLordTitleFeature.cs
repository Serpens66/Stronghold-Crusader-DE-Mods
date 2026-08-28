using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class CustomLordTitleFeature : IDisposable
    {
        private delegate string LobbyMemberNameGetterDelegate(Platform_Multiplayer.MPLobbyMember self);
        private delegate string GetComputerNameDelegate(int computerOpponent, int computerName);

        private sealed class CachedTitles
        {
            internal string[] RawTitles;
            internal CustomLordResolvedTitle[] ResolvedTitles;
        }

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<string, CachedTitles> titleCache =
            new Dictionary<string, CachedTitles>(StringComparer.OrdinalIgnoreCase);
        private readonly Hook lobbyNameHook;
        private readonly LobbyMemberNameGetterDelegate lobbyNameOriginal;
        private readonly Hook getComputerNameHook;
        private readonly GetComputerNameDelegate getComputerNameOriginal;
        private string cachedLocale = string.Empty;
        private bool upstreamFixObserved;
        private bool rewriteFailureLogged;
        private bool disposed;

        internal CustomLordTitleFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Hook installedLobby = null;
            Hook installedIngame = null;
            try
            {
                MethodInfo lobbyGetter = typeof(Platform_Multiplayer.MPLobbyMember)
                    .GetProperty(nameof(Platform_Multiplayer.MPLobbyMember.Name), BindingFlags.Public | BindingFlags.Instance)
                    ?.GetGetMethod(nonPublic: true) ??
                    throw new MissingMethodException(typeof(Platform_Multiplayer.MPLobbyMember).FullName, "get_Name");
                installedLobby = new Hook(lobbyGetter, new LobbyMemberNameGetterDelegate(LobbyMemberNameHook));
                lobbyNameOriginal = installedLobby.GenerateTrampoline<LobbyMemberNameGetterDelegate>();

                MethodInfo getComputerName = typeof(OnScreenText).GetMethod(
                    nameof(OnScreenText.getComputerName),
                    BindingFlags.Public | BindingFlags.Static) ??
                    throw new MissingMethodException(typeof(OnScreenText).FullName, nameof(OnScreenText.getComputerName));
                installedIngame = new Hook(getComputerName, new GetComputerNameDelegate(GetComputerNameHook));
                getComputerNameOriginal = installedIngame.GenerateTrampoline<GetComputerNameDelegate>();

                lobbyNameHook = installedLobby;
                getComputerNameHook = installedIngame;
            }
            catch
            {
                installedIngame?.Dispose();
                installedLobby?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Custom Lord title hooks installed; includes TEMPORARY Script Extender title-index workaround.");
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableCustomLordExtendedPackages;

        internal void ApplySetting()
        {
            titleCache.Clear();
            cachedLocale = string.Empty;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            titleCache.Clear();
            getComputerNameHook?.Undo();
            getComputerNameHook?.Dispose();
            lobbyNameHook?.Undo();
            lobbyNameHook?.Dispose();
        }

        private string LobbyMemberNameHook(Platform_Multiplayer.MPLobbyMember self)
        {
            string vanillaName = lobbyNameOriginal(self);
            try
            {
                if (!IsEnabled || self == null || !self.SkirmishMember || self.SkirmishHumanMember ||
                    string.IsNullOrWhiteSpace(self.customLordName))
                {
                    return vanillaName;
                }

                int subtype = self.GetLordSubType();
                if (!TryGetTitles(self.customLordName, out CachedTitles titles) || !IsValidSubtype(subtype))
                    return vanillaName;

                string title = titles.ResolvedTitles[subtype].ColumnTitle;
                return string.IsNullOrWhiteSpace(title) ? vanillaName : title;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(log, $"Custom Lord lobby title resolution failed: {exception.Message}");
                return vanillaName;
            }
        }

        private string GetComputerNameHook(int computerOpponent, int computerName)
        {
            string upstreamName = getComputerNameOriginal(computerOpponent, computerName);
            try
            {
                if (!IsEnabled || !IsValidSubtype(computerName))
                    return upstreamName;

                GameAIManagerAPI api = GameAIManagerAPI.Instance;
                if (!api.GetSlotIndexByExtendedLordEnum((Enums.AILords)computerOpponent, out int playerSlot))
                    return upstreamName;

                string lordName = api.GetGenericSlotLordName(playerSlot);
                if (string.IsNullOrWhiteSpace(lordName) || !TryGetTitles(lordName, out CachedTitles titles))
                    return upstreamName;

                string correctRawSuffix = titles.RawTitles[computerName];
                string upstreamRawSuffix = IsValidSubtype(playerSlot)
                    ? titles.RawTitles[playerSlot]
                    : string.Empty;

                // TEMPORARY SCRIPT EXTENDER WORKAROUND:
                // Current SHCDESE passes its extended player-slot index to GetLordTitle instead of
                // Vanilla's computerName subtype. Remove only this compatibility detection once the
                // upstream OnScreenText hook uses computerName; keep lobby and duplicate fallbacks.
                if (!upstreamFixObserved &&
                    !string.Equals(correctRawSuffix, upstreamRawSuffix, StringComparison.Ordinal) &&
                    CustomLordTitlePolicy.HasExactSuffix(upstreamName, correctRawSuffix))
                {
                    upstreamFixObserved = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "Script Extender already supplied the correct Custom Lord subtype title; temporary index workaround is bypassed.");
                }

                if (CustomLordTitlePolicy.TryRewriteFullName(
                        upstreamName,
                        upstreamRawSuffix,
                        correctRawSuffix,
                        titles.ResolvedTitles[computerName],
                        out string rewritten))
                {
                    return rewritten;
                }

                if (!rewriteFailureLogged)
                {
                    rewriteFailureLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Custom Lord title compatibility hook could not identify the Script Extender title suffix; the original name is preserved.");
                }
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(log, $"Custom Lord ingame title resolution failed: {exception.Message}");
            }

            return upstreamName;
        }

        private bool TryGetTitles(string lordName, out CachedTitles titles)
        {
            titles = null;
            if (string.IsNullOrWhiteSpace(lordName))
                return false;

            GameAIManagerAPI api = GameAIManagerAPI.Instance;
            if (!api.IsSupportedCustomLord(lordName))
                return false;

            // Use the same live locale source as GetLordTitle so runtime language changes invalidate both caches together.
            string locale = GameAssetManagerAPI.Instance.CurrentLanguage ?? string.Empty;
            if (!string.Equals(cachedLocale, locale, StringComparison.OrdinalIgnoreCase))
            {
                titleCache.Clear();
                cachedLocale = locale;
            }

            if (titleCache.TryGetValue(lordName, out titles))
                return true;

            string[] rawTitles = new string[CustomLordTitlePolicy.TitleSlotCount];
            string[] ordinalTitles = new string[CustomLordTitlePolicy.TitleSlotCount];
            for (int slot = 0; slot < CustomLordTitlePolicy.TitleSlotCount; slot++)
            {
                rawTitles[slot] = api.GetLordTitle(lordName, slot) ?? string.Empty;
                ordinalTitles[slot] = Translate.Instance.lookUpText(
                    Enums.eTextSections.TEXT_CUSTOMISATION,
                    (Enums.eTextValues)(23 + slot)) ?? string.Empty;
            }

            titles = new CachedTitles
            {
                RawTitles = rawTitles,
                ResolvedTitles = CustomLordTitlePolicy.ResolveAll(rawTitles, ordinalTitles)
            };
            titleCache[lordName] = titles;
            return true;
        }

        private static bool IsValidSubtype(int subtype)
        {
            return subtype >= 0 && subtype < CustomLordTitlePolicy.TitleSlotCount;
        }
    }
}
