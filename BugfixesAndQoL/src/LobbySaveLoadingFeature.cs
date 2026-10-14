using BepInEx.Logging;
using CrusaderDE;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Noesis;
using Steamworks;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    // Static ownership outlives startup cleanup. Installed hooks are never torn down.
    internal static class LobbySaveLoadingFeature
    {
        private delegate void SetupDelegate(FRONT_Multiplayer self);
        private static ILHook soloHook;
        private static Hook visibilityHook;
        private static SetupDelegate setupOriginal;
        private static FieldInfo readyField, loadButtonField;
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel settings;
        private static bool soloMarker, visibilityMarker, soloFailure, visibilityFailure;

        internal static void Initialize(ManualLogSource logger, BugfixesAndQoLViewModel options)
        {
            log = logger;
            settings = options;
            // Each installation is independent: an unknown IL body cannot suppress visibility repair.
            if (soloHook == null && !soloFailure)
            {
                ILHook candidate = null;
                try
                {
                    readyField = RequireField("MPLocalReady", typeof(bool));
                    MethodInfo update = RequireMethod("Update", BindingFlags.Public);
                    using (var definition = new DynamicMethodDefinition(update))
                    using (var context = new ILContext(definition.Definition))
                    {
                        context.ReferenceBag = RuntimeILReferenceBag.Instance;
                        context.Invoke(il => LobbySaveLoadingIl.Patch(il, ResolveSoloLoad));
                        definition.Generate(); // Compile the entire transformed body before installing.
                    }
                    candidate = new ILHook(update, il => LobbySaveLoadingIl.Patch(il, ResolveSoloLoad),
                        new ILHookConfig { ManualApply = true, ID = "BugfixesAndQoL.SoloMultiplayerSaveLoading" });
                    candidate.Apply();
                    soloHook = candidate;
                    candidate = null; // Publication ends rollback ownership, including logging failures.
                    Info("SOLO_MP_SAVE_LOADING installed; Vanilla save/remapping path retained.");
                }
                catch (Exception error)
                {
                    candidate?.Dispose(); // Only an unpublished initialization candidate.
                    soloFailure = true;
                    Error("SOLO_MP_SAVE_LOADING_INSTALL", error);
                }
            }
            if (visibilityHook == null && !visibilityFailure)
            {
                Hook candidate = null;
                try
                {
                    loadButtonField = RequireField("RefLoadButton", typeof(Button));
                    MethodInfo setup = RequireMethod("ShowSetupScreen", BindingFlags.NonPublic);
                    candidate = new Hook(setup, (SetupDelegate)AfterSetup,
                        new HookConfig { ManualApply = true });
                    setupOriginal = candidate.GenerateTrampoline<SetupDelegate>();
                    candidate.Apply();
                    visibilityHook = candidate;
                    candidate = null;
                    Info("SKIRMISH_LOAD_VISIBILITY installed for the process lifetime.");
                }
                catch (Exception error)
                {
                    candidate?.Dispose();
                    visibilityFailure = true;
                    Error("SKIRMISH_LOAD_VISIBILITY_INSTALL", error);
                }
            }
        }

        private static bool ResolveSoloLoad(bool vanilla, FRONT_Multiplayer self)
        {
            if (vanilla || soloFailure || !settings.EnableMod || !settings.EnableSoloMultiplayerSaveLoading ||
                !LobbySaveLoadingPolicy.IsNormalMultiplayer(FRONT_Multiplayer.skirmishGame,
                    FRONT_Multiplayer.coopGame, FRONT_Multiplayer.customCoopGame))
                return vanilla;
            try
            {
                var lobby = self.currentLobby;
                if (lobby == null || !lobby.isHost ||
                    !LobbySaveLoadingPolicy.IsNormalLobby(lobby.id.m_SteamID, lobby.gameTypeCoop,
                        lobby.coopTrailGame, self.singlePlayerCoop, self.trailMakerMode,
                        FRONT_Multiplayer.customizedTrail, self.extremeTrailCustomised) ||
                    !self.panelActive || !MainViewModel.viewModelLoaded ||
                    !MainViewModel.Instance.Show_MPGameCreation ||
                    !ReferenceEquals(lobby, Platform_Multiplayer.Instance.activeLobby))
                    return vanilla;
                ulong local = SteamUser.GetSteamID().m_SteamID;
                bool valid = LobbySaveLoadingPolicy.IsSoloReadyRoster(lobby.members, lobby.numLobbyMembers, local);
                bool result = LobbySaveLoadingPolicy.ResolveSoloLoad(vanilla, true, false, false,
                    lobby.isHost, valid && (bool)readyField.GetValue(self), valid,
                    lobby.numLobbyMembers, lobby.iMaxPlayers, 1);
                if (result && !soloMarker)
                {
                    soloMarker = true;
                    Info("SOLO_MP_SAVE_LOADING callback confirmed after startup cleanup.");
                }
                return result;
            }
            catch (Exception error)
            {
                if (!soloFailure)
                {
                    soloFailure = true;
                    Error("SOLO_MP_SAVE_LOADING_CALLBACK", error);
                }
                return vanilla;
            }
        }

        private static void AfterSetup(FRONT_Multiplayer self)
        {
            setupOriginal(self); // Includes other managed hooks; Vanilla executes exactly once.
            if (visibilityFailure || !LobbySaveLoadingPolicy.ShouldRestoreVisibility(
                settings.EnableMod && settings.FixSkirmishLoadButtonVisibility,
                FRONT_Multiplayer.skirmishGame, FRONT_Multiplayer.coopGame)) return;
            try
            {
                var button = loadButtonField.GetValue(self) as Button;
                if (button == null) throw new InvalidOperationException("Setup has no live Vanilla load button.");
                button.Visibility = Visibility.Visible;
                if (!visibilityMarker)
                {
                    visibilityMarker = true;
                    Info("SKIRMISH_LOAD_VISIBILITY callback confirmed after startup cleanup.");
                }
            }
            catch (Exception error)
            {
                if (!visibilityFailure)
                {
                    visibilityFailure = true;
                    Error("SKIRMISH_LOAD_VISIBILITY_CALLBACK", error);
                }
            }
        }

        private static void Info(string message)
        {
            // Logging cannot change activation or interrupt Vanilla's UI assignment.
            try { Shared.DebugLogHelper.LogInfo(log, message); } catch { }
        }

        private static void Error(string id, Exception error)
        {
            try { Shared.DebugLogHelper.LogErrorLimited(log, id, error.ToString()); } catch { }
        }

        internal static FieldInfo RequireField(string name, Type type)
        {
            FieldInfo field = typeof(FRONT_Multiplayer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != type) throw new MissingFieldException("FRONT_Multiplayer." + name);
            return field;
        }

        internal static MethodInfo RequireMethod(string name, BindingFlags visibility)
        {
            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(name, BindingFlags.Instance | visibility,
                null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void)) throw new MissingMethodException("FRONT_Multiplayer." + name);
            return method;
        }
    }
}
