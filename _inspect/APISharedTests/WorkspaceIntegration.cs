using System;
using APIShared;
using Shared;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace APISharedTests
{
    internal static class Program
    {
        private static void TestWorkspaceIntegration()
        {
            string workspace = FindWorkspaceRoot();
            string plugin = File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "Core", "APISharedPlugin.cs"));
            string project = File.ReadAllText(Path.Combine(workspace, "APIShared", "APIShared.csproj"));
            string unitHud = string.Join("\n", Array.ConvertAll(Directory.GetFiles(Path.Combine(workspace, "APIShared", "src", "Presentation", "UnitHud"), "*.cs"), File.ReadAllText));
            string lobbyState = File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "Lobby", "LobbyStateCapability.cs"));
            string sharedPreset = string.Join("\n", Directory.GetFiles(Path.Combine(workspace, "APIShared", "src", "ModSettings"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            string bugfixLord = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "LordUnitHudRegistration.cs"));
            string bugfixGatehouse = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "GatehouseDistanceOriginRegistration.cs"));
            string extraGatehouse = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "GatehouseAutomationRuntime.cs"));
            string extraProject = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "ExtraFeatures.csproj"));
            string extraAiv = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "AIDefenseRepairRuntime.cs"));
            string activeAiv = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "src", "AivPlacementOracle.cs"));
            string activeRuntime = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "src", "ActiveAIVDetectionRuntime.cs"));
            string activePlugin = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "src", "ActiveAIVDetectorPlugin.cs"));
            string activeProject = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "ActiveAIVDetector.csproj"));
            string bugfixControlGroups = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "ControlGroupDisbandCleanupRuntime.cs"));
            string bugfixNativeDefinition = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "ControlGroupNativeDefinition.cs"));
            string bugfixPlugin = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLPlugin.cs"));
            string bugfixProject = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "BugfixesAndQoL.csproj"));
            string castlePlanner = File.ReadAllText(Path.Combine(workspace, "CastlePlanner", "src", "CastlePlannerRuntime.cs"));
            string castlePlugin = File.ReadAllText(Path.Combine(workspace, "CastlePlanner", "src", "CastlePlannerPlugin.cs"));
            string castleProject = File.ReadAllText(Path.Combine(workspace, "CastlePlanner", "CastlePlanner.csproj"));
            string customPlugin = File.ReadAllText(Path.Combine(workspace, "ExtendedData", "src", "ExtendedDataPlugin.cs"));
            string customProject = File.ReadAllText(Path.Combine(workspace, "ExtendedData", "ExtendedData.csproj"));
            string extremePlugin = File.ReadAllText(Path.Combine(workspace, "ExtremePowers", "src", "ExtremePowersPlugin.cs"));
            string extremeProject = File.ReadAllText(Path.Combine(workspace, "ExtremePowers", "ExtremePowers.csproj"));
            string releaseConfig = File.ReadAllText(Path.Combine(workspace, "Shared", "Tools", "Release", "release-projects.json"));
            Func<string, string, bool> apiDependencyMatchesRelease = (source, consumer) =>
            {
                Match dependency = Regex.Match(source,
                    @"BepInDependency\((?:ApiSharedGuid|""APIShared_Serp"")\s*,\s*""(?<version>[^""]+)""\)");
                string directory = Path.Combine(workspace, consumer == "ActiveAIVDetector" ? "Helpers/ActiveAIVDetector" : consumer);
                string info = Path.Combine(directory, "info.json");
                if (!File.Exists(info)) info = Directory.GetFiles(Path.Combine(directory, "BepInEx", "plugins"), "info.json", SearchOption.AllDirectories).Single();
                var metadata = (Dictionary<string, object>)DependencyFreeJson.Parse(File.ReadAllText(info));
                var requirements = ((List<object>)metadata["Dependencies"])
                    .Cast<Dictionary<string, object>>().Where(item => (string)item["GUID"] == "APIShared_Serp").ToArray();
                return dependency.Success && requirements.Length == 1 &&
                    dependency.Groups["version"].Value == (string)requirements[0]["MinimumVersion"];
            };
            string releaseScript = File.ReadAllText(Path.Combine(workspace, "Shared", "Tools", "Release", "Release-Mod.ps1"));
            string nexusScript = File.ReadAllText(Path.Combine(workspace, "Shared", "Tools", "Release", "NexusRelease.Common.ps1"));
            string steamScript = File.ReadAllText(Path.Combine(workspace, "Shared", "Tools", "Steam", "Create-SteamModPack.ps1"));
            string hostPlugin = File.ReadAllText(Path.Combine(workspace, "SerpsModsHost", "src", "SerpsModsHostPlugin.cs"));
            string randomRuntime = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "RandomEventsRuntime.cs"));
            string randomRegistry = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "ScenarioSignpostRegistry.cs"));
            string randomPlacement = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "SignpostPlacementService.cs"));
            string randomPlugin = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "RandomEventsPlugin.cs"));
            string randomManifest = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "info.json"));
            string hunterRoutes =
                File.ReadAllText(Path.Combine(workspace, "ImprovedHunters", "src", "HunterPclReachability.cs")) + "\n" +
                File.ReadAllText(Path.Combine(workspace, "ImprovedHunters", "src", "HunterActiveTargetReachability.cs")) + "\n" +
                File.ReadAllText(Path.Combine(workspace, "ImprovedHunters", "src", "HunterPclReachabilityDiagnostic.cs"));
            string sourceManifest = File.ReadAllText(Path.Combine(workspace, "APIShared", "info.json"));
            string buildingPatchPath = Path.Combine(workspace, "APIShared", "Patches", "Assets", "GUI", "XAMLResources", "HUD_Buildings.xaml");
            string packagedBuildingPatchPath = Path.Combine(workspace, "APIShared", "BepInEx", "plugins", "APIShared_Serp", "Patches", "Assets", "GUI", "XAMLResources", "HUD_Buildings.xaml");
            string buildingPatch = File.ReadAllText(buildingPatchPath);
            string troopPatch = File.ReadAllText(Path.Combine(workspace, "APIShared", "Patches", "Assets", "GUI", "XAMLResources", "HUD_Troops.xaml"));
            Match minimumMatch = Regex.Match(sourceManifest,
                @"""MinimumScriptExtenderVersion""\s*:\s*""([^""]*)""");
            string minimumExtenderVersion = minimumMatch.Success ? minimumMatch.Groups[1].Value : string.Empty;
            Match versionMatch = Regex.Match(sourceManifest, @"""Version""\s*:\s*""([^""]+)""");
            string modVersion = versionMatch.Success ? versionMatch.Groups[1].Value : string.Empty;

            Assert(string.IsNullOrEmpty(minimumExtenderVersion) ||
                plugin.Contains($"[BepInDependency(ScriptExtenderGuid, \"{minimumExtenderVersion}\")]"),
                "plugin dependency matches the source manifest minimum");
            Assert(plugin.Contains("OnLibraryLoaded(CrusaderLibraryLoadContext context)"),
                "plugin consumes CrusaderLibraryLoadContext");
            Assert(plugin.Contains("context.ModuleHandle.ToInt64()") && plugin.Contains("context.Memory"),
                "plugin passes the Script Extender module and memory view");
            Assert(!plugin.Contains("IntPtr libraryHandle") && !plugin.Contains("ReadOnlySpan<byte> memory"),
                "old LibraryLoaded callback is absent");
            Assert(!project.Contains("Zhuqiaomon") && !project.Contains("PolyHook"),
                "project has no obsolete native dependency");
            Assert(!project.Contains("SelectedUnitCommandCapability") &&
                typeof(IApiShared).GetMethod("TryGetSelectedUnitCommand", BindingFlags.Public | BindingFlags.Instance) == null,
                "the redundant selected-unit broker must not remain in APIShared");
            int menuContextStart = sharedPreset.IndexOf("private static SettingsMenuContext CaptureSettingsMenuContext()", StringComparison.Ordinal);
            int menuContextEnd = menuContextStart >= 0
                ? sharedPreset.IndexOf("private static SettingsMenuContext ResolveSettingsMenuContext(", menuContextStart, StringComparison.Ordinal)
                : -1;
            string menuContext = menuContextStart >= 0 && menuContextEnd > menuContextStart
                ? sharedPreset.Substring(menuContextStart, menuContextEnd - menuContextStart)
                : string.Empty;
            int menuReadyGuard = menuContext.IndexOf("if (!CrusaderDE.MainViewModel.viewModelLoaded)", StringComparison.Ordinal);
            int menuSingletonRead = menuContext.IndexOf("CrusaderDE.MainViewModel.Instance", StringComparison.Ordinal);
            int menuNeutralReturn = menuReadyGuard >= 0
                ? menuContext.IndexOf("return SettingsMenuContext.Other;", menuReadyGuard, StringComparison.Ordinal)
                : -1;
            Assert(menuReadyGuard >= 0 && menuReadyGuard < menuSingletonRead &&
                menuNeutralReturn > menuReadyGuard && menuNeutralReturn < menuSingletonRead &&
                sharedPreset.Contains("Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>") &&
                sharedPreset.Contains("viewModel.System_RefreshSettingsAccess();"),
                "settings menu context must not construct the Vanilla ViewModel before readiness and must refresh when the hub changes");
            Assert(!project.Contains("LocalScriptExtenderBuildOutput") &&
                !project.Contains("LocalScriptExtenderModOutput"),
                "APIShared must default to the installed Script Extender without dead local fallbacks");
            Assert(Count(unitHud, "setupTroopsOriginal(panel)") == 1 &&
                Count(unitHud, "populateGroupsOriginal(panel)") == 1 &&
                Count(unitHud, "gameActionOriginal(command, value1, value2, value3)") == 1 &&
                Count(unitHud, "updateSpritesOriginal(self, colour, arabic)") == 1 &&
                Count(unitHud, "updateSpritesOriginal(main, lastSpriteColour, lastSpriteArabic)") == 2,
                "central HUD sprite handling retains the hook, explicit refresh and activation-restoration paths");
            string beforeRenderMethod = ExtractSourceMethod(unitHud, "private void OnBeforeRender()");
            int idleGuard = beforeRenderMethod.IndexOf("if (activeSurfaces == UnitHudSurface.None && !hasActionButtons && !pendingPresentation && !refreshRequested && recruitmentLease == null) return;", StringComparison.Ordinal);
            Assert(idleGuard >= 0 && idleGuard < beforeRenderMethod.IndexOf("Time.frameCount", StringComparison.Ordinal),
                "idle guard returns before Unity access and keeps action-only consumers active");
            var actionPatch = new XmlDocument(); actionPatch.LoadXml(troopPatch);
            XmlNode actionHost = actionPatch.SelectSingleNode("//*[local-name()='Canvas' and @*[local-name()='Name']='APISharedTroopActionButtonsHost']");
            Assert(actionHost?.ParentNode?.ParentNode is XmlElement actionOperation &&
                actionOperation.GetAttribute("XPath") == "//n:Grid[@Name='TroopSelectionControls']",
                "own-troop actions inherit foreign-HUD suppression from the Vanilla selection container");
            Assert(beforeRenderMethod.IndexOf("ExpireRecruitment();", StringComparison.Ordinal) < beforeRenderMethod.IndexOf("MainViewModel.viewModelLoaded", StringComparison.Ordinal),
                "pending tickets expire even when the HUD is unavailable");
            int selectionRefreshStart = beforeRenderMethod.IndexOf("if ((refresh && HasCategories(UnitHudSurface.TroopSelection)) || troopSelectionChanged)", StringComparison.Ordinal);
            int explicitRefreshStart = beforeRenderMethod.IndexOf("if (refresh)", selectionRefreshStart + 1, StringComparison.Ordinal);
            string selectionRefreshBlock = selectionRefreshStart >= 0 && explicitRefreshStart > selectionRefreshStart
                ? beforeRenderMethod.Substring(selectionRefreshStart, explicitRefreshStart - selectionRefreshStart)
                : string.Empty;
            string explicitRefreshBlock = explicitRefreshStart >= 0
                ? beforeRenderMethod.Substring(explicitRefreshStart)
                : string.Empty;
            Assert(selectionRefreshBlock.Contains("SetupSelectedTroops()") &&
                !selectionRefreshBlock.Contains("HUDControlGroups") &&
                !selectionRefreshBlock.Contains("updateSpritesOriginal") &&
                !selectionRefreshBlock.Contains("ApplyImageOverrides"),
                "selection-only refreshes must update the troop HUD without touching control groups or global images");
            Assert(explicitRefreshBlock.Contains("HUDControlGroups?.Update()") &&
                explicitRefreshBlock.Contains("updateSpritesOriginal(main, lastSpriteColour, lastSpriteArabic)") &&
                explicitRefreshBlock.Contains("ApplyImageOverrides(main, lastSpriteColour, lastSpriteArabic)"),
                "explicit refreshes must retain control-group and legitimate global image updates");
            string ensureButtonsMethod = ExtractSourceMethod(unitHud, "private void EnsureCategoryButtons(");
            string hideButtonsMethod = ExtractSourceMethod(unitHud, "private void HideCategoryButtons()");
            Assert(ensureButtonsMethod.Contains("var resolvedHosts = new Grid[TroopSlotCount]") &&
                ensureButtonsMethod.Contains("var resolvedButtons = new Button[TroopSlotCount]") &&
                ensureButtonsMethod.Contains("var resolvedTints = new Border[TroopSlotCount]") &&
                ensureButtonsMethod.IndexOf("categoryHosts = resolvedHosts", StringComparison.Ordinal) >
                ensureButtonsMethod.IndexOf("foreach (Button button in resolvedButtons)", StringComparison.Ordinal),
                "troop category hosts, buttons, and tints must be resolved completely before the cache is published");
            Assert(hideButtonsMethod.Contains("if (host != null)") &&
                unitHud.Contains("lock (sync) visibleSlots.Clear();"),
                "troop HUD failure cleanup must tolerate incomplete caches and clear stale slot snapshots");
            string armyMethod = ExtractSourceMethod(unitHud, "private void ApplyArmyReport(");
            Assert(armyMethod.IndexOf("APISharedArmyCategoriesHost", StringComparison.Ordinal) >= 0 &&
                armyMethod.IndexOf("APISharedArmyCategoriesHost", StringComparison.Ordinal) < armyMethod.IndexOf("main.AllTroops[", StringComparison.Ordinal) &&
                armyMethod.IndexOf("RenderArmyHosts(host, custom);", StringComparison.Ordinal) < armyMethod.IndexOf("main.AllTroops[desired.Key]", StringComparison.Ordinal),
                "army HUD host must be resolved before Vanilla troop counts are changed");
            Assert(unitHud.Contains("ManualApply = true") &&
                unitHud.Contains("updateSpritesOriginal = updateSpritesHook.GenerateTrampoline") &&
                unitHud.Contains("updateSpritesHook.Apply();") &&
                unitHud.IndexOf("updateSpritesOriginal = updateSpritesHook.GenerateTrampoline", StringComparison.Ordinal) <
                    unitHud.IndexOf("updateSpritesHook.Apply();", StringComparison.Ordinal),
                "HUD hooks must not become callable before their trampolines are published");
            Assert(unitHud.Contains("ButtonCreateTroop") && unitHud.Contains("Enums.GameActionCommand.MakeTroop") &&
                unitHud.Contains("GameActionEvents.TryRegister(") && unitHud.Contains("accepted: BeforeRecruitmentOriginal") && !unitHud.Contains("recruitmentGameActionOriginal"),
                "recruitment variants must observe Vanilla's MakeTroop action");
            Assert(unitHud.Contains("APIShared.Internal.MissionEvents.Ended") && unitHud.Contains("activeRecruitment.Clear()") &&
                unitHud.Contains("APISharedUnitDetailHost"),
                "recruitment map reset or unit-detail host is missing");
            Assert(unitHud.Contains("if (updateSpritesActive)") &&
                unitHud.Contains("IsImageOverrideContextReady()") &&
                !unitHud.Contains("main.UpdateUITroopSprites(lastSpriteColour"),
                "image overrides lack startup, reentrancy, or refresh-loop protection");
            string renderMethod = beforeRenderMethod;
            int loadedGuard = renderMethod.IndexOf("if (!MainViewModel.viewModelLoaded) return;", StringComparison.Ordinal);
            int singletonRead = renderMethod.IndexOf("MainViewModel main = MainViewModel.Instance;", StringComparison.Ordinal);
            int hudGuard = renderMethod.IndexOf("if (main?.HUDmain == null) return;", StringComparison.Ordinal);
            int refreshConsume = renderMethod.IndexOf("refreshRequested = false;", StringComparison.Ordinal);
            Assert(loadedGuard >= 0 && loadedGuard < singletonRead &&
                singletonRead < hudGuard && hudGuard < refreshConsume,
                "render HUD readiness guards must precede the lazy singleton getter and refresh consumption");
            Assert(renderMethod.Contains("TryApplyFrameArea(\"hover presentation\", () => ApplyHover(main));") &&
                renderMethod.Contains("TryApplyFrameArea(\"army-report presentation\", () => ApplyArmyReport(main), HideArmyHosts);") &&
                renderMethod.Contains("TryApplyFrameArea(\"recruitment presentation\", () => ApplyRecruitmentPresentation(main), HideRecruitmentControls);") &&
                renderMethod.Contains("TryApplyFrameArea(\"unit-detail presentation\", () => ApplyUnitDetails(main), HideUnitDetailControls);") &&
                unitHud.Contains("private void ApplyHover(MainViewModel main)") &&
                unitHud.Contains("private void ApplyArmyReport(MainViewModel main)"),
                "independent render HUD areas must reuse the readiness-checked view model and fail closed separately");
            Assert(unitHud.Contains("private void HideArmyHosts()") &&
                unitHud.Contains("private void HideRecruitmentControls()") &&
                unitHud.Contains("private void HideUnitDetailControls()") &&
                unitHud.Contains("if (archerVariantHost != null)") &&
                unitHud.Contains("if (unitDetailHost != null)"),
                "HUD area cleanup must tolerate missing XAML controls");
            Assert(unitHud.Contains("loggedCallbackFailures.Add(area)") && !unitHud.Contains("callbackErrorLogged"),
                "HUD callback failures must be deduplicated independently by area");

            var patchDocument = new XmlDocument();
            patchDocument.LoadXml(buildingPatch);
            var recruitmentRoots = new HashSet<string>(StringComparer.Ordinal);
            int structuralOperations = 0;
            foreach (XmlNode operation in patchDocument.DocumentElement.ChildNodes)
            {
                if (operation.NodeType != XmlNodeType.Element || operation.LocalName != "Operation") continue;
                string operationType = operation.Attributes?["Type"]?.Value;
                if (operationType != "Add" && operationType != "InsertBefore" && operationType != "InsertAfter" && operationType != "Replace") continue;
                structuralOperations++;
                var contentNodes = new List<XmlNode>();
                foreach (XmlNode child in operation.ChildNodes)
                    if (child.NodeType == XmlNodeType.Element && child.LocalName == "Content") contentNodes.Add(child);
                var contentRoots = new List<XmlNode>();
                if (contentNodes.Count == 1)
                    foreach (XmlNode child in contentNodes[0].ChildNodes)
                        if (child.NodeType == XmlNodeType.Element) contentRoots.Add(child);
                Assert(contentNodes.Count == 1 && contentRoots.Count == 1,
                    "every structural XAML patch operation must expose exactly one Content root");
                if (operation.Attributes?["XPath"]?.Value == "//n:Grid[@Name='BarracksPanel']" && contentRoots.Count == 1)
                {
                    // Simulate Script Extender 2.5.0: only the first direct Content element survives.
                    string xamlName = contentRoots[0].Attributes?["x:Name"]?.Value;
                    if (!string.IsNullOrEmpty(xamlName)) recruitmentRoots.Add(xamlName);
                }
            }
            Assert(structuralOperations > 0 && recruitmentRoots.SetEquals(new[]
            {
                "APISharedArcherVariantHost"
            }), "Script Extender merge simulation must retain the single recruitment host");
            Assert(buildingPatch.Contains("APISharedArcherVariantPrevious") &&
                buildingPatch.Contains("APISharedArcherVariantNext") &&
                buildingPatch.Contains("Margin=\"4,0,0,91\"") &&
                buildingPatch.Contains("Margin=\"64,0,0,91\"") &&
                buildingPatch.Contains("Opacity=\"0.58\"") &&
                buildingPatch.Contains("Value=\"0.78\"") &&
                buildingPatch.Contains("Value=\"0.90\""),
                "recruitment arrows lack the confirmed names, positions, or translucent states");
            Assert(buildingPatch == File.ReadAllText(packagedBuildingPatchPath),
                "source and packaged APIShared building XAML patches must match");
            for (int slot = 1; slot <= 8; slot++)
            {
                Assert(troopPatch.Contains("APISharedUnitHudSlotHost" + slot) &&
                    troopPatch.Contains("APISharedUnitHudSlot" + slot) &&
                    troopPatch.Contains("APISharedUnitHudSlotTint" + slot),
                    "troop category slot " + slot + " lacks a colocated button and non-interactive tint overlay");
            }
            Assert(unitHud.Contains("UnitHudImageSlot.UIBuildingsO011") &&
                unitHud.Contains("UnitHudImageSlot.UIBuildingsO012") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsK007") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsK008") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsO016") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsO017") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsO018") &&
                Enum.GetValues(typeof(UnitHudImageSlot)).Length == 7,
                "typed image-override allowlist is incomplete");
            Assert(unitHud.Contains("cache.Mask = source == null ? null : new ImageBrush(source)") &&
                unitHud.Contains("ConditionalWeakTable<Border, TintCache>") &&
                unitHud.Contains(": (float)tint.Alpha / byte.MaxValue") &&
                unitHud.Contains("troopPanel?.FindName(\"ArchersSelected\")") &&
                unitHud.Contains("source ?? main?.UIButtonsK023") &&
                unitHud.Contains("main?.HUDBuildingPanel?.RefRecruitArcherButton") &&
                unitHud.Contains("source ?? main?.UIButtonsO001") &&
                unitHud.Contains("PropEx.GetSprite2(vanilla)") &&
                unitHud.Contains("parent.Children.Insert(imageIndex + 1, tint)") &&
                !unitHud.Contains("button.Content = CreateTint"),
                "surface-specific Vanilla Archer icons or the overlay tint are incorrect");
            Assert(bugfixLord.Contains("TryRegisterCategory"),
                "BugfixesAndQoL does not register with the central HUD API");
            Assert(bugfixGatehouse.Contains("TryGetGatehouseDistanceOrigin") &&
                bugfixGatehouse.Contains("GatehouseDistanceOrigin.BuildingBoundsCenter") &&
                bugfixGatehouse.Contains("GatehouseDistanceOrigin.VanillaBuildingBegin"),
                "BugfixesAndQoL must exclusively select the gatehouse distance origin through APIShared");
            Assert(extraGatehouse.Contains("TryGetGatehouseTiming") &&
                extraGatehouse.Contains("new GatehouseTimingSettings") &&
                !extraProject.Contains("GatehouseTimingPatch.cs"),
                "ExtraFeatures must exclusively apply gatehouse timing through APIShared");
            Assert(extraAiv.Contains("TryGetAivBuildStep") && extraAiv.Contains("TryRegisterObserver") &&
                !extraAiv.Contains("ExecuteBuildStepDelegate") && !extraAiv.Contains("executeBuildStepHook"),
                "ExtraFeatures must consume the shared AIV build-step broker without a local fallback detour");
            Assert(activeRuntime.Contains("TryGetAivBuildStep") && activeRuntime.Contains("TryRegisterObserver") &&
                !activeAiv.Contains("ExecuteBuildStepDelegate") && !activeAiv.Contains("executeBuildStepHook") &&
                activeProject.Contains("<Reference Include=\"APIShared\">") && activeProject.Contains("<Private>false</Private>") &&
                apiDependencyMatchesRelease(activePlugin, "ActiveAIVDetector"),
                "ActiveAIVDetector prebuild tracing must use APIShared as a thin hard dependency");
            Assert(bugfixControlGroups.Contains("TryRemoveUnitFromControlGroups") &&
                !bugfixControlGroups.Contains("ControlGroupStorage") &&
                !bugfixNativeDefinition.Contains("ControlGroupStorage") &&
                apiDependencyMatchesRelease(bugfixPlugin, "BugfixesAndQoL"),
                "native control-group storage must only be resolved and mutated inside APIShared");
            int bindStart = castlePlanner.IndexOf("private void BindNativeFunctions(", StringComparison.Ordinal);
            int hookStart = castlePlanner.IndexOf("private void InstallHumanStartPreparationHook(", StringComparison.Ordinal);
            string castleBindings = bindStart >= 0 && hookStart > bindStart
                ? castlePlanner.Substring(bindStart, hookStart - bindStart)
                : string.Empty;
            Assert(castleBindings.Contains("setPlacement = Bind<SetPlacementDelegate>") &&
                castleBindings.Contains("testSpecificCandidate = Bind<TestSpecificCandidateDelegate>") &&
                castleBindings.Contains("prepareLayout = Bind<PrepareLayoutDelegate>") &&
                castleBindings.Contains("executeToPercentage = Bind<ExecuteToPercentageDelegate>") &&
                !castleBindings.Contains("AddDetour") && !castleBindings.Contains("AddContextHook"),
                "CastlePlanner AIV targets 0x54EC0, 0x54DE0, 0x53D00, and 0x55F50 must remain bind-only");
            Assert(Count(lobbyState, "Application.onBeforeRender += OnBeforeRender") == 1 &&
                lobbyState.Contains("ObserveDirtyNow();") &&
                Count(lobbyState, "getActiveLobbyMembersOriginal(self, coopGame)") == 1 &&
                Count(lobbyState, "leaveLobbyOriginal(self, startGame)") == 1 &&
                lobbyState.Contains("private const int FallbackFrames = 15") &&
                lobbyState.Contains("APIShared.Internal.MissionEvents.Initialization") &&
                lobbyState.Contains("APIShared.Internal.MissionEvents.Ended"),
                "APIShared must own exactly one managed lobby observer with one-call detours and map-aware fallback polling");
            Assert(sharedPreset.Contains("TryGetLobbyState") &&
                sharedPreset.Contains("API_SHARED_LOBBY_OBSERVER") &&
                !sharedPreset.Contains("Application.onBeforeRender"),
                "the APIShared-owned preset coordinator must consume the lobby capability without a local render poller");
            string[] lobbyObserverProjects = FindRuntimeProjectFiles(workspace)
                .Where(path => File.ReadAllText(path).Contains("API_SHARED_LOBBY_OBSERVER"))
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert(lobbyObserverProjects.SequenceEqual(new[] { "APIShared" }, StringComparer.Ordinal),
                "only APIShared may compile the shared lobby bridge");
            Assert(bugfixProject.Contains("<Reference Include=\"APIShared\">") && bugfixProject.Contains("<Private>false</Private>") &&
                castleProject.Contains("<Reference Include=\"APIShared\">") && castleProject.Contains("<Private>false</Private>") &&
                customProject.Contains("<Reference Include=\"APIShared\">") && customProject.Contains("<Private>false</Private>") &&
                extremeProject.Contains("<Reference Include=\"APIShared\">") && extremeProject.Contains("<Private>false</Private>") &&
                apiDependencyMatchesRelease(bugfixPlugin, "BugfixesAndQoL") &&
                apiDependencyMatchesRelease(castlePlugin, "CastlePlanner") &&
                apiDependencyMatchesRelease(customPlugin, "ExtendedData") &&
                apiDependencyMatchesRelease(extremePlugin, "ExtremePowers"),
                "all known active preset consumers must compile against and hard-depend on APIShared");
            Assert(apiDependencyMatchesRelease(activePlugin, "ActiveAIVDetector") &&
                apiDependencyMatchesRelease(bugfixPlugin, "BugfixesAndQoL") &&
                apiDependencyMatchesRelease(castlePlugin, "CastlePlanner") &&
                apiDependencyMatchesRelease(customPlugin, "ExtendedData") &&
                apiDependencyMatchesRelease(extremePlugin, "ExtremePowers") &&
                apiDependencyMatchesRelease(File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "ExtraFeaturesPlugin.cs")), "ExtraFeatures"),
                "release inventory must declare each consumer's actual APIShared minimum");
            Assert(releaseScript.Contains("Profile = 'Thin'") &&
                !releaseScript.Contains("Profile = 'Bundle'") &&
                !releaseScript.Contains("with-APIShared") &&
                releaseScript.Contains("Get-PublishedApiSharedRelease") &&
                releaseScript.Contains("APIShared.dll") && releaseScript.Contains("SHCDESE.dll") &&
                releaseScript.Contains("RedBird"),
                "release staging must emit only thin artifacts, validate the published APIShared release, and reject private runtime copies");
            Assert(!nexusScript.Contains("[ValidateSet('Thin','Bundle')]") &&
                nexusScript.Contains("Nexus akzeptiert nur Thin-Artefakte") &&
                nexusScript.Contains("Assert-NexusRetiredFileChainsInactive"),
                "Nexus validation must accept only thin artifacts and reject active retired bundle chains");
            Assert(steamScript.Contains("Infrastructure") && steamScript.Contains("releaseConfig.ApiShared.Guid") &&
                steamScript.Contains("APIShared.dll") && releaseConfig.Contains("\"Guid\": \"APIShared_Serp\""),
                "Steam staging must model APIShared as one separately validated infrastructure dependency");
            Assert(hostPlugin.Contains("List<PackModRecord> assetMods = ScriptExtenderCompatibility.SelectRuntimePackRecords(manifest);") &&
                hostPlugin.Contains("foreach (PackModRecord mod in assetMods)") &&
                hostPlugin.Contains("expected={assetMods.Count}"),
                "Steam host must register infrastructure assets before active child assets and include them in diagnostics");
            string randomPathing = randomRuntime + "\n" + randomRegistry + "\n" + randomPlacement;
            Assert(Count(randomPathing, "GetPathComponentGrid()") > 0 &&
                !randomPathing.Contains("TileManager.PathConnectionGrid") &&
                Count(randomPathing, "pathConnections[") == Count(randomPathing, "pathConnections.Length"),
                "RandomEvents must use the public path-component grid and guard every indexed access by span length");
            Match randomMinimumMatch = Regex.Match(randomManifest,
                @"""MinimumScriptExtenderVersion""\s*:\s*""([^""]*)""");
            string randomMinimum = randomMinimumMatch.Success ? randomMinimumMatch.Groups[1].Value : string.Empty;
            Assert(string.IsNullOrEmpty(randomMinimum) ||
                randomPlugin.Contains($"[BepInDependency(ScriptExtenderGuid, \"{randomMinimum}\")]"),
                "RandomEvents source dependency must match its manifest minimum");
            MatchCollection orderedRouteCalls = Regex.Matches(
                hunterRoutes,
                @"FindNextComponentTowardDestination\s*\(\s*(?<root>inputs|context)\.PlayerId\s*,\s*\k<root>\.(?:SourcePcl)\s*,\s*\k<root>\.(?:TargetPcl)\s*,");
            int routeCallCount = Count(hunterRoutes, "FindNextComponentTowardDestination(");
            Assert(routeCallCount > 0 && orderedRouteCalls.Count == routeCallCount,
                "route queries must retain player, current component, destination component, mode argument order");
            Assert(modVersion.Length > 0 && sourceManifest.Contains("\"NetworkMode\": 1"),
                "source manifest declares a version and gameplay mode");
        }

        private static string FindWorkspaceRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "APIShared")) &&
                    File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Workspace root was not found.");
        }

        private static IEnumerable<string> FindRuntimeProjectFiles(string workspace)
        {
            foreach (string directory in Directory.GetDirectories(workspace))
            {
                foreach (string project in Directory.GetFiles(
                    directory,
                    "*.csproj",
                    SearchOption.TopDirectoryOnly))
                {
                    yield return project;
                }

                string name = Path.GetFileName(directory);
                if (!string.Equals(name, "Helpers", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (string child in Directory.GetDirectories(directory))
                    foreach (string project in Directory.GetFiles(
                        child,
                        "*.csproj",
                        SearchOption.TopDirectoryOnly))
                    {
                        yield return project;
                    }
            }
        }

        private static int Main()
        {
            try { TestWorkspaceIntegration(); Console.WriteLine("PASS: APISharedTests workspace integration."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static string ExtractSourceMethod(string source, string signature)
        {
            int start=source.IndexOf(signature,StringComparison.Ordinal);
            if(start<0) throw new InvalidOperationException("Missing method: "+signature);
            int opening=source.IndexOf('{',start),depth=0;
            for(int i=opening;i<source.Length;i++)
            {
                if(source[i]=='{')depth++;
                if(source[i]=='}' && --depth==0) return source.Substring(start,i-start+1);
            }
            throw new InvalidOperationException("Unterminated method: "+signature);
        }
        private static int Count(string value, string fragment)
        {
            int count=0, position=0;
            while ((position=value.IndexOf(fragment,position,StringComparison.Ordinal))>=0)
            { count++; position+=fragment.Length; }
            return count;
        }
    }
}
