using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace APIShared
{
    internal sealed unsafe class UnitHudPresentationService
    {
        private const int TroopSlotCount = 8;
        private const int GroupCount = 10;
        private const int GroupCapacity = 10000;
        private const int GroupRecordWidth = 2;
        private const int GroupVisibleSlots = 4;
        private const int ControlGroupStoragePatternRva = 0x186338;
        private const int ControlGroupStorageDisplacementOffset = 0x10;
        private const int ControlGroupStorageNextInstructionOffset = 0x14;
        private const int ControlGroupStorageRva = 0x36D78D0;
        private const int TunnelSummaryType = 33;
        private const int EuropeanTroopSummaryStart = 0;
        private const int MonkSummaryType = 9;
        private const int SiegeEngineSummaryStart = 10;
        private const int PortableSiegeSummaryStart = 13;
        private const int ArabicTroopSummaryStart = 17;
        private const string ControlGroupStoragePattern =
            "48 8D 1D ? ? ? ? 48 8B F8 48 8B E9 48 8D 05 ? ? ? ? BE 0A 00 00 00 45 33 F6";

        private delegate void SetupTroopsDelegate(HUD_Troops self);
        private delegate void TroopClickDelegate(MainViewModel self, object parameter);
        private delegate void PopulateGroupsDelegate(HUD_ControlGroups self);
        private delegate void GameActionDelegate(Enums.KeyFunctions command, int value1, int value2, int value3);
        private delegate void UpdateSpritesDelegate(MainViewModel self, int colour, bool arabic);
        private delegate void CreateTroopDelegate(MainViewModel self, object parameter);
        private delegate void EnterCreateTroopDelegate(MainViewModel self, object parameter);
        private delegate int RecruitmentGameActionDelegate(Enums.GameActionCommand command, int structureId, int state, int value2);

        private static readonly FieldInfo SelectedCountsField = RequireField(typeof(HUD_Troops), "SelectedChimpArray");
        private static readonly FieldInfo SelectedTypeCountField = RequireField(typeof(HUD_Troops), "NoSelectedChimpTypes");
        private static readonly FieldInfo CurrentPageField = RequireField(typeof(HUD_Troops), "currentPage");
        private static readonly FieldInfo PagesField = RequireField(typeof(HUD_Troops), "pages");
        private static readonly FieldInfo TroopPositionsField = RequireField(typeof(HUD_Troops), "SelTroopPositions");
        private static readonly FieldInfo GroupImagesField = RequireField(typeof(HUD_ControlGroups), "RefTroopImages");
        private static readonly FieldInfo GroupValuesField = RequireField(typeof(HUD_ControlGroups), "RefTroopValues");
        private static readonly FieldInfo GroupExtraField = RequireField(typeof(HUD_ControlGroups), "RefTroopExtraValues");
        private static readonly MethodInfo GroupSpriteMethod = RequireMethod(typeof(HUD_ControlGroups), "GetTroopSprite", new[] { typeof(int) });

        private readonly object sync = new object();
        private readonly List<CategoryRegistration> categories = new List<CategoryRegistration>();
        private readonly List<InteractionRegistration> interactions = new List<InteractionRegistration>();
        private readonly List<ImageRegistration> imageOverrides = new List<ImageRegistration>();
        private readonly List<RecruitmentRegistration> recruitment = new List<RecruitmentRegistration>();
        private readonly HashSet<string> loggedCategoryConflicts = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> loggedCallbackFailures = new HashSet<string>(StringComparer.Ordinal);
        private readonly ManualLogSource log;
        private readonly string binaryHash;
        private readonly int* groupRecords;
        private readonly bool groupRecordsAvailable;
        private readonly List<UnitHudSlotSnapshot> visibleSlots = new List<UnitHudSlotSnapshot>();
        private Hook setupTroopsHook;
        private Hook leftClickHook;
        private Hook rightClickHook;
        private Hook populateGroupsHook;
        private Hook gameActionHook;
        private Hook updateSpritesHook;
        private Hook createTroopHook;
        private Hook enterCreateTroopHook;
        private Hook recruitmentGameActionHook;
        private SetupTroopsDelegate setupTroopsOriginal;
        private TroopClickDelegate leftClickOriginal;
        private TroopClickDelegate rightClickOriginal;
        private PopulateGroupsDelegate populateGroupsOriginal;
        private GameActionDelegate gameActionOriginal;
        private UpdateSpritesDelegate updateSpritesOriginal;
        private CreateTroopDelegate createTroopOriginal;
        private EnterCreateTroopDelegate enterCreateTroopOriginal;
        private RecruitmentGameActionDelegate recruitmentGameActionOriginal;
        private IDisposable mapUnloadSubscription;
        private HUD_Troops activeTroopPanel;
        private Button[] categoryButtons;
        private bool refreshRequested;
        private int lastFrame = -1;
        private int lastSpriteColour;
        private bool lastSpriteArabic;
        private bool hasSpriteContext;
        private readonly Dictionary<int, string> activeRecruitment = new Dictionary<int, string>();
        private RecruitmentLease recruitmentLease;
        private long nextRecruitmentTicketId;
        private Button archerVariantSelector;
        private Border archerVariantTint;
        private Grid unitDetailHost;
        private Image unitDetailImage;
        private Border unitDetailTint;
        private TextBlock unitDetailDescription;
        [ThreadStatic]
        private static bool updateSpritesActive;
        [ThreadStatic]
        private static int createTroopContextType;

        private UnitHudPresentationService(string hash, ManualLogSource logger, int* records, bool recordsAvailable)
        {
            binaryHash = hash ?? string.Empty;
            log = logger;
            groupRecords = records;
            groupRecordsAvailable = recordsAvailable;
        }

        internal static bool TryCreate(
            string hash,
            long moduleBase,
            ReadOnlySpan<byte> memory,
            ManualLogSource log,
            out UnitHudPresentationService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            var installed = new List<Hook>();
            try
            {
                int* records = null;
                bool recordAccess = false;
                string groupReason = "Native control-group records unavailable; that surface remains Vanilla.";
                if (string.Equals(hash, ApiSharedRuntime.SupportedHash, StringComparison.OrdinalIgnoreCase) && moduleBase != 0)
                {
                    int match = FindUnique(memory, ControlGroupStoragePattern);
                    if (match == ControlGroupStoragePatternRva)
                    {
                        int displacement = BitConverter.ToInt32(memory.Slice(match + ControlGroupStorageDisplacementOffset, sizeof(int)).ToArray(), 0);
                        int target = checked(match + ControlGroupStorageNextInstructionOffset + displacement);
                        long required = (long)target + GroupCount * GroupCapacity * GroupRecordWidth * sizeof(int);
                        if (target == ControlGroupStorageRva && required <= memory.Length)
                        {
                            records = (int*)(moduleBase + target);
                            recordAccess = true;
                            groupReason = "Control-group record layout validated.";
                        }
                    }
                }

                var candidate = new UnitHudPresentationService(hash, log, records, recordAccess);
                candidate.Install(installed);
                service = candidate;
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.UnitHudPresentation,
                    NativeCapabilityState.Available,
                    hash,
                    groupReason);
                NativeApiLog.Info(log, $"Unit HUD presentation installed; controlGroups={recordAccess}, build={hash}.");
                return true;
            }
            catch (Exception ex)
            {
                for (int i = installed.Count - 1; i >= 0; i--)
                {
                    try { installed[i].Undo(); } catch { }
                    try { installed[i].Dispose(); } catch { }
                }
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.UnitHudPresentation,
                    NativeCapabilityState.Faulted,
                    hash,
                    ex.Message);
                NativeApiLog.Error(log, $"Unit HUD presentation failed before publication; Vanilla remains active: {ex}");
                return false;
            }
        }

        internal IUnitHudPresentationCapability Bind(string ownerGuid) => new Binding(this, ownerGuid);

        private void Install(List<Hook> installed)
        {
            setupTroopsHook = PrepareHook(RequireMethod(typeof(HUD_Troops), "SetupSelectedTroops", Type.EmptyTypes), (SetupTroopsDelegate)SetupTroopsHook, "APIShared.UnitHud.SetupSelectedTroops", installed);
            setupTroopsOriginal = setupTroopsHook.GenerateTrampoline<SetupTroopsDelegate>();
            setupTroopsHook.Apply();
            leftClickHook = PrepareHook(RequireMethod(typeof(MainViewModel), "TroopsLeftClickCommand", new[] { typeof(object) }), (TroopClickDelegate)LeftClickHook, "APIShared.UnitHud.TroopsLeftClick", installed);
            leftClickOriginal = leftClickHook.GenerateTrampoline<TroopClickDelegate>();
            leftClickHook.Apply();
            rightClickHook = PrepareHook(RequireMethod(typeof(MainViewModel), "TroopsRightClickCommand", new[] { typeof(object) }), (TroopClickDelegate)RightClickHook, "APIShared.UnitHud.TroopsRightClick", installed);
            rightClickOriginal = rightClickHook.GenerateTrampoline<TroopClickDelegate>();
            rightClickHook.Apply();
            populateGroupsHook = PrepareHook(RequireMethod(typeof(HUD_ControlGroups), "populate", Type.EmptyTypes), (PopulateGroupsDelegate)PopulateGroupsHook, "APIShared.UnitHud.PopulateControlGroups", installed);
            populateGroupsOriginal = populateGroupsHook.GenerateTrampoline<PopulateGroupsDelegate>();
            populateGroupsHook.Apply();
            gameActionHook = PrepareHook(RequireMethod(typeof(EngineInterface), "GameAction", new[] { typeof(Enums.KeyFunctions), typeof(int), typeof(int), typeof(int) }), (GameActionDelegate)GameActionHook, "APIShared.UnitHud.ControlGroupGameAction", installed);
            gameActionOriginal = gameActionHook.GenerateTrampoline<GameActionDelegate>();
            gameActionHook.Apply();
            updateSpritesHook = PrepareHook(RequireMethod(typeof(MainViewModel), "UpdateUITroopSprites", new[] { typeof(int), typeof(bool) }), (UpdateSpritesDelegate)UpdateSpritesHook, "APIShared.UnitHud.UpdateUITroopSprites", installed);
            updateSpritesOriginal = updateSpritesHook.GenerateTrampoline<UpdateSpritesDelegate>();
            updateSpritesHook.Apply();
            createTroopHook = PrepareHook(RequireMethod(typeof(MainViewModel), "ButtonCreateTroop", new[] { typeof(object) }), (CreateTroopDelegate)CreateTroopHook, "APIShared.UnitHud.ButtonCreateTroop", installed);
            createTroopOriginal = createTroopHook.GenerateTrampoline<CreateTroopDelegate>();
            createTroopHook.Apply();
            enterCreateTroopHook = PrepareHook(RequireMethod(typeof(MainViewModel), "ButtonEnterCreateTroop", new[] { typeof(object) }), (EnterCreateTroopDelegate)EnterCreateTroopHook, "APIShared.UnitHud.ButtonEnterCreateTroop", installed);
            enterCreateTroopOriginal = enterCreateTroopHook.GenerateTrampoline<EnterCreateTroopDelegate>();
            enterCreateTroopHook.Apply();
            recruitmentGameActionHook = PrepareHook(RequireMethod(typeof(EngineInterface), "GameAction", new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) }), (RecruitmentGameActionDelegate)RecruitmentGameActionHook, "APIShared.UnitHud.RecruitmentGameAction", installed);
            recruitmentGameActionOriginal = recruitmentGameActionHook.GenerateTrampoline<RecruitmentGameActionDelegate>();
            recruitmentGameActionHook.Apply();
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(_ => ResetRecruitment());
            UnityEngine.Application.onBeforeRender += OnBeforeRender;
        }

        private static Hook PrepareHook(MethodInfo method, Delegate callback, string id, List<Hook> installed)
        {
            var config = new HookConfig { ManualApply = true, ID = id };
            var hook = new Hook(method, callback, config);
            installed.Add(hook);
            return hook;
        }

        private bool RegisterCategory(string owner, UnitHudCategoryDefinition definition, UnitHudCategoryMatcher matcher, out NativeCapabilityDiagnostic diagnostic)
        {
            if (definition == null || matcher == null || string.IsNullOrWhiteSpace(definition.CategoryId) || string.IsNullOrWhiteSpace(definition.DisplayName) ||
                definition.BaseUnitType < 0 || definition.Surfaces == UnitHudSurface.None || (definition.Surfaces & ~UnitHudSurface.All) != 0)
                return Fail("Category definition, matcher, IDs, base type and surfaces are required.", out diagnostic);
            lock (sync)
            {
                if (categories.Any(x => x.Owner == owner && x.Definition.CategoryId == definition.CategoryId))
                    return Fail("The owner already registered this category ID.", out diagnostic);
                categories.Add(new CategoryRegistration(owner, definition, matcher));
                SortRegistrations();
                refreshRequested = true;
            }
            diagnostic = Available("Category registered for the process lifetime.");
            return true;
        }

        private bool RegisterInteraction(string owner, string id, UnitHudInteractionHandler handler, out NativeCapabilityDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(id) || handler == null)
                return Fail("Interaction ID and handler are required.", out diagnostic);
            lock (sync)
            {
                if (interactions.Any(x => x.Owner == owner && x.Id == id))
                    return Fail("The owner already registered this interaction ID.", out diagnostic);
                interactions.Add(new InteractionRegistration(owner, id, handler));
                interactions.Sort((a, b) => Compare(a.Owner, a.Id, b.Owner, b.Id));
            }
            diagnostic = Available("Interaction observer registered for the process lifetime.");
            return true;
        }

        private bool RegisterImage(string owner, UnitHudImageOverrideDefinition definition, UnitHudImageOverrideResolver resolver, out NativeCapabilityDiagnostic diagnostic)
        {
            if (definition == null || resolver == null || string.IsNullOrWhiteSpace(definition.OverrideId) || !Enum.IsDefined(typeof(UnitHudImageSlot), definition.Slot))
                return Fail("A valid image override definition and resolver are required.", out diagnostic);
            lock (sync)
            {
                if (imageOverrides.Any(x => x.Owner == owner && x.Definition.OverrideId == definition.OverrideId))
                    return Fail("The owner already registered this image override ID.", out diagnostic);
                imageOverrides.Add(new ImageRegistration(owner, definition, resolver));
                imageOverrides.Sort((a, b) =>
                {
                    int result = a.Definition.Priority.CompareTo(b.Definition.Priority);
                    return result != 0 ? result : Compare(a.Owner, a.Definition.OverrideId, b.Owner, b.Definition.OverrideId);
                });
                refreshRequested = true;
            }
            diagnostic = Available("Image override registered for the process lifetime.");
            return true;
        }

        private bool RegisterRecruitment(string owner, string categoryId, UnitHudRecruitmentHandler handler, out NativeCapabilityDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(categoryId) || handler == null)
                return Fail("Recruitment category ID and handler are required.", out diagnostic);
            lock (sync)
            {
                CategoryRegistration category = categories.FirstOrDefault(x => x.Owner == owner && x.Definition.CategoryId == categoryId);
                if (category == null || !HasSurface(category, UnitHudSurface.Recruitment))
                    return Fail("Recruitment requires an existing owner-local category with the Recruitment surface.", out diagnostic);
                if (category.Definition.BaseUnitType != (int)eChimps.CHIMP_TYPE_ARCHER)
                    return Fail("The current UI implementation supports recruitment variants only for the European Archer.", out diagnostic);
                if (recruitment.Any(x => x.Category.Key == category.Key))
                    return Fail("The owner already registered recruitment for this category.", out diagnostic);
                recruitment.Add(new RecruitmentRegistration(category, handler));
                recruitment.Sort((a, b) =>
                {
                    int result = a.Category.Definition.Order.CompareTo(b.Category.Definition.Order);
                    return result != 0 ? result : StringComparer.Ordinal.Compare(a.Category.Key, b.Category.Key);
                });
                refreshRequested = true;
            }
            diagnostic = Available("Recruitment variant registered for the process lifetime.");
            return true;
        }

        private bool CompleteRecruitment(string owner, UnitHudRecruitmentTicket ticket, int matchedCount, string reason, out NativeCapabilityDiagnostic diagnostic)
        {
            if (ticket == null || ticket.TicketId <= 0 || matchedCount < 0 || ticket.OwnerGuid != owner)
                return Fail("A valid owner-bound recruitment ticket and non-negative matched count are required.", out diagnostic);
            lock (sync)
            {
                if (recruitmentLease == null || recruitmentLease.Ticket.TicketId != ticket.TicketId || recruitmentLease.Ticket.OwnerGuid != owner)
                    return Fail("The recruitment ticket is no longer active.", out diagnostic);
                recruitmentLease = null;
                refreshRequested = true;
            }
            diagnostic = Available($"Recruitment completed with {matchedCount} matched units: {reason ?? string.Empty}");
            return true;
        }

        private void SortRegistrations() => categories.Sort((a, b) =>
        {
            int result = a.Definition.BaseUnitType.CompareTo(b.Definition.BaseUnitType);
            if (result == 0) result = a.Definition.Order.CompareTo(b.Definition.Order);
            return result != 0 ? result : Compare(a.Owner, a.Definition.CategoryId, b.Owner, b.Definition.CategoryId);
        });

        private static int Compare(string ownerA, string idA, string ownerB, string idB)
        {
            int result = StringComparer.Ordinal.Compare(ownerA, ownerB);
            return result != 0 ? result : StringComparer.Ordinal.Compare(idA, idB);
        }

        private bool Fail(string reason, out NativeCapabilityDiagnostic diagnostic)
        {
            diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.UnitHudPresentation, NativeCapabilityState.ValidationFailed, ApiSharedRuntime.SupportedHash, reason);
            return false;
        }

        private static NativeCapabilityDiagnostic Available(string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.UnitHudPresentation, NativeCapabilityState.Available, ApiSharedRuntime.SupportedHash, reason);

        private void SetupTroopsHook(HUD_Troops panel)
        {
            setupTroopsOriginal(panel);
            if (!HasCategories(UnitHudSurface.TroopSelection))
                return;
            try { RenderTroopCategories(panel); }
            catch (Exception ex)
            {
                HideCategoryButtons();
                lock (sync) visibleSlots.Clear();
                LogCallbackFailure("troop HUD", ex);
            }
        }

        private void RenderTroopCategories(HUD_Troops panel)
        {
            EnsureCategoryButtons(panel);
            int[] vanillaCounts = SelectedCountsField.GetValue(panel) as int[];
            TranslateTransform[] positions = TroopPositionsField.GetValue(panel) as TranslateTransform[];
            if (vanillaCounts == null || positions == null || positions.Length < TroopSlotCount)
                throw new InvalidOperationException("Vanilla troop HUD fields have an unexpected layout.");

            List<UnitHudUnitSnapshot> selected = CaptureSelectedUnits();
            List<DisplayEntry> entries = BuildEntries(selected, vanillaCounts, UnitHudSurface.TroopSelection);
            int pages = Math.Max(1, (entries.Count + TroopSlotCount - 1) / TroopSlotCount);
            int page = Math.Max(0, Math.Min((int)CurrentPageField.GetValue(panel), pages - 1));
            PagesField.SetValue(panel, pages);
            CurrentPageField.SetValue(panel, page);
            SelectedTypeCountField.SetValue(panel, entries.Count);
            panel.HideAllSelectedTroops();
            panel.HideAllSelectedTroopsNumbers();
            HideCategoryButtons();
            SetPageButtons(panel, page, pages);

            var snapshots = new List<UnitHudSlotSnapshot>();
            for (int slot = 0; slot < TroopSlotCount; slot++)
            {
                int index = page * TroopSlotCount + slot;
                if (index >= entries.Count) break;
                DisplayEntry entry = entries[index];
                if (entry.Category == null)
                {
                    positions[slot].Y = panel.SetSelectedTroopVisible(entry.VanillaType);
                    panel.SetSelectedTroopPosition(entry.VanillaType, slot);
                    snapshots.Add(new UnitHudSlotSnapshot(slot, entry.VanillaType, null));
                }
                else
                {
                    Button button = categoryButtons[slot];
                    button.Tag = entry.Category.Key;
                    button.ToolTip = ResolveText(entry.Category, UnitHudTextKind.DisplayName);
                    button.RenderTransform = positions[slot];
                    ImageSource source = ResolveCategoryImage(entry.Category, panel);
                    ApplyButtonImage(button, source);
                    button.Content = CreateTint(entry.Category.Definition.Tint, source);
                    button.Visibility = Visibility.Visible;
                    UnitHudCategorySnapshot snapshot = Snapshot(entry.Category, entry.Units);
                    snapshots.Add(new UnitHudSlotSnapshot(slot, -1, snapshot));
                }
                panel.ShowSelectedTroopsNumber(slot, entry.Count);
            }
            lock (sync)
            {
                visibleSlots.Clear();
                visibleSlots.AddRange(snapshots);
            }
        }

        private static Border CreateTint(UnitHudTint tint, ImageSource source) => new Border
        {
            Background = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, tint.Red, tint.Green, tint.Blue)),
            OpacityMask = source == null ? null : new ImageBrush(source),
            Opacity = tint.Alpha == 0 || tint.Red == byte.MaxValue && tint.Green == byte.MaxValue && tint.Blue == byte.MaxValue
                ? 0.0f
                : 0.22f * tint.Alpha / byte.MaxValue,
            IsHitTestVisible = false
        };

        private void EnsureCategoryButtons(HUD_Troops panel)
        {
            if (ReferenceEquals(activeTroopPanel, panel) && categoryButtons != null)
                return;

            var resolvedButtons = new Button[TroopSlotCount];
            for (int i = 0; i < resolvedButtons.Length; i++)
            {
                var button = panel.FindName("APISharedUnitHudSlot" + (i + 1)) as Button;
                if (button == null) throw new MissingMemberException("APISharedUnitHudSlot" + (i + 1));
                resolvedButtons[i] = button;
            }
            foreach (Button button in resolvedButtons)
            {
                button.PreviewMouseDown -= OnCategoryMouseDown;
                button.PreviewMouseDown += OnCategoryMouseDown;
            }
            activeTroopPanel = panel;
            categoryButtons = resolvedButtons;
        }

        private void HideCategoryButtons()
        {
            if (categoryButtons == null) return;
            foreach (Button button in categoryButtons)
                if (button != null) button.Visibility = Visibility.Collapsed;
        }

        private void OnCategoryMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (!(sender is Button button) || !(button.Tag is string key) || string.IsNullOrEmpty(key) || args == null)
                return;
            UnitHudMouseButton mouse;
            if (args.ChangedButton == MouseButton.Left) mouse = UnitHudMouseButton.Left;
            else if (args.ChangedButton == MouseButton.Right) mouse = UnitHudMouseButton.Right;
            else if (args.ChangedButton == MouseButton.Middle) mouse = UnitHudMouseButton.Middle;
            else return;
            CategoryRegistration category = GetCategory(key);
            if (category == null) return;
            List<UnitHudUnitSnapshot> matches = CaptureSelectedUnits().Where(x => ReferenceEquals(Classify(x, UnitHudSurface.TroopSelection), category)).ToList();
            UnitHudCategorySnapshot snapshot = Snapshot(category, matches);
            if (mouse == UnitHudMouseButton.Left)
                SetSelection(matches.Select(x => x.GameId));
            else if (mouse == UnitHudMouseButton.Right)
            {
                var ids = new HashSet<int>(matches.Select(x => x.GameId));
                SetSelection(CaptureSelectedUnits().Where(x => !ids.Contains(x.GameId)).Select(x => x.GameId));
            }
            NotifyInteraction(new UnitHudInteractionContext(mouse, snapshot));
            args.Handled = true;
        }

        private void LeftClickHook(MainViewModel self, object parameter) => HandleVanillaClick(self, parameter, true);
        private void RightClickHook(MainViewModel self, object parameter) => HandleVanillaClick(self, parameter, false);

        private void HandleVanillaClick(MainViewModel self, object parameter, bool left)
        {
            int type;
            try { type = (int)self.getChimpEnum(parameter as string); }
            catch
            {
                if (left) leftClickOriginal(self, parameter);
                else rightClickOriginal(self, parameter);
                return;
            }
            if (!HasDerivedCategory(type))
            {
                if (left) leftClickOriginal(self, parameter);
                else rightClickOriginal(self, parameter);
                return;
            }
            List<UnitHudUnitSnapshot> selected = CaptureSelectedUnits();
            if (left)
                SetSelection(selected.Where(x => x.VanillaType == type && !IsClaimed(x, UnitHudSurface.TroopSelection)).Select(x => x.GameId));
            else
                SetSelection(selected.Where(x => x.VanillaType != type || IsClaimed(x, UnitHudSurface.TroopSelection)).Select(x => x.GameId));
        }

        private static void SetSelection(IEnumerable<int> ids) => EngineInterface.TroopSelectionChanged(ids.Where(x => x > 0).Distinct().ToArray());

        private void PopulateGroupsHook(HUD_ControlGroups panel)
        {
            populateGroupsOriginal(panel);
            if (!groupRecordsAvailable || !HasCategories(UnitHudSurface.ControlGroups)) return;
            try { RenderGroups(panel); }
            catch (Exception ex) { LogCallbackFailure("control-group HUD", ex); }
        }

        private void GameActionHook(Enums.KeyFunctions command, int value1, int value2, int value3)
        {
            gameActionOriginal(command, value1, value2, value3);
            if (command < Enums.KeyFunctions.GroupTroops0 || command > Enums.KeyFunctions.GroupTroops9) return;
            try
            {
                MainViewModel main = MainViewModel.Instance;
                if (main?.Show_HUD_ControlGroups == true) main.HUDControlGroups?.Update();
            }
            catch (Exception ex) { LogCallbackFailure("control-group refresh", ex); }
        }

        private void CreateTroopHook(MainViewModel self, object parameter)
        {
            int type;
            try { type = (int)self.getChimpEnum(parameter as string); }
            catch { createTroopOriginal(self, parameter); return; }
            lock (sync)
            {
                if (recruitmentLease != null && recruitmentLease.Ticket.BaseUnitType == type)
                {
                    refreshRequested = true;
                    return;
                }
            }
            int previous = createTroopContextType;
            createTroopContextType = type;
            try { createTroopOriginal(self, parameter); }
            finally { createTroopContextType = previous; }
        }

        private void EnterCreateTroopHook(MainViewModel self, object parameter)
        {
            enterCreateTroopOriginal(self, parameter);
            try
            {
                int type = (int)self.getChimpEnum("CHIMP_TYPE_" + ((parameter as string) ?? string.Empty).ToUpperInvariant());
                ApplyRecruitmentText(self, GetActiveRecruitment(type));
            }
            catch (Exception ex) { LogCallbackFailure("recruitment tooltip", ex); }
        }

        private int RecruitmentGameActionHook(Enums.GameActionCommand command, int structureId, int state, int value2)
        {
            if (command == Enums.GameActionCommand.MakeTroop && createTroopContextType == state)
                TryBeginRecruitment(state, structureId);
            return recruitmentGameActionOriginal(command, structureId, state, value2);
        }

        private void TryBeginRecruitment(int baseType, int amount)
        {
            RecruitmentRegistration registration = GetActiveRecruitment(baseType);
            if (registration == null || amount <= 0) return;
            int playerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            if (playerId <= 0) { ResetActiveRecruitment(baseType); return; }
            UnitHudRecruitmentTicket ticket;
            lock (sync)
            {
                if (recruitmentLease != null) return;
                ticket = new UnitHudRecruitmentTicket(++nextRecruitmentTicketId, registration.Category.Owner,
                    registration.Category.Definition.CategoryId, playerId, baseType, amount);
            }
            bool accepted = false;
            try { accepted = registration.Handler(ticket); }
            catch (Exception ex) { LogCallbackFailure("recruitment " + registration.Category.Key, ex); }
            lock (sync)
            {
                if (accepted) recruitmentLease = new RecruitmentLease(ticket, Time.realtimeSinceStartup + 10f);
                else
                {
                    activeRecruitment.Remove(baseType);
                    refreshRequested = true;
                }
            }
        }

        private RecruitmentRegistration GetActiveRecruitment(int baseType)
        {
            lock (sync)
            {
                if (!activeRecruitment.TryGetValue(baseType, out string key)) return null;
                return recruitment.FirstOrDefault(x => x.Category.Key == key);
            }
        }

        private void ResetActiveRecruitment(int baseType)
        {
            lock (sync) { activeRecruitment.Remove(baseType); refreshRequested = true; }
        }

        private void ResetRecruitment()
        {
            lock (sync)
            {
                activeRecruitment.Clear();
                recruitmentLease = null;
                refreshRequested = true;
            }
        }

        private void RenderGroups(HUD_ControlGroups panel)
        {
            Image[,] images = GroupImagesField.GetValue(panel) as Image[,];
            TextBlock[,] values = GroupValuesField.GetValue(panel) as TextBlock[,];
            TextBlock[] extras = GroupExtraField.GetValue(panel) as TextBlock[];
            if (images == null || values == null || extras == null || images.GetLength(0) < GroupCount || images.GetLength(1) < GroupVisibleSlots)
                throw new InvalidOperationException("Vanilla control-group HUD fields have an unexpected layout.");
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            for (int group = 0; group < GroupCount; group++)
            {
                var counts = new Dictionary<string, GroupEntry>(StringComparer.Ordinal);
                int total = 0;
                int* start = groupRecords + group * GroupCapacity * GroupRecordWidth;
                for (int index = 0; index < GroupCapacity; index++)
                {
                    int unitId = start[index * GroupRecordWidth];
                    int globalId = start[index * GroupRecordWidth + 1];
                    if (!TryCapture(unitId, out UnitHudUnitSnapshot snapshot) || unchecked((int)snapshot.GlobalId) != globalId) continue;
                    total++;
                    CategoryRegistration category = Classify(snapshot, UnitHudSurface.ControlGroups);
                    int summaryType = ToSummaryType(snapshot.VanillaType);
                    string key = category != null ? category.Key : "v:" + summaryType;
                    if (summaryType < 0 && category == null) continue;
                    if (!counts.TryGetValue(key, out GroupEntry entry)) counts[key] = entry = new GroupEntry(category, summaryType, snapshot.VanillaType);
                    entry.Count++;
                }
                GroupEntry[] visible = counts.Values.Where(x => x.Count > 0).OrderBy(x => x.BaseType).ThenBy(x => x.Category == null ? 0 : 1).ThenBy(x => x.SortKey, StringComparer.Ordinal).Take(GroupVisibleSlots).ToArray();
                int shown = 0;
                for (int slot = 0; slot < GroupVisibleSlots; slot++)
                {
                    if (slot < visible.Length)
                    {
                        GroupEntry entry = visible[slot];
                        images[group, slot].Source = entry.Category != null ? ResolveCategoryImage(entry.Category, null) : GroupSpriteMethod.Invoke(panel, new object[] { entry.SummaryType }) as ImageSource;
                        images[group, slot].Visibility = Visibility.Visible;
                        values[group, slot].Text = entry.Count.ToString();
                        values[group, slot].Visibility = Visibility.Visible;
                        shown += entry.Count;
                    }
                    else { images[group, slot].Visibility = Visibility.Hidden; values[group, slot].Visibility = Visibility.Hidden; }
                }
                int remainder = Math.Max(0, total - shown);
                extras[group].Text = remainder > 0 ? "+" + remainder : string.Empty;
                extras[group].Visibility = remainder > 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void UpdateSpritesHook(MainViewModel self, int colour, bool arabic)
        {
            if (updateSpritesActive)
                return;
            updateSpritesActive = true;
            try
            {
                updateSpritesOriginal(self, colour, arabic);
                lastSpriteColour = colour;
                lastSpriteArabic = arabic;
                hasSpriteContext = true;
                if (IsImageOverrideContextReady()) ApplyImageOverrides(self, colour, arabic);
            }
            finally { updateSpritesActive = false; }
        }

        private void ApplyImageOverrides(MainViewModel self, int colour, bool arabic)
        {
            ImageRegistration[] registrations;
            lock (sync) registrations = imageOverrides.ToArray();
            foreach (UnitHudImageSlot slot in Enum.GetValues(typeof(UnitHudImageSlot)))
            {
                ImageSource vanilla = GetImage(self, slot);
                ImageSource current = vanilla;
                foreach (ImageRegistration registration in registrations.Where(x => x.Definition.Slot == slot))
                {
                    try
                    {
                        ImageSource next = registration.Resolver(new UnitHudImageOverrideContext(colour, arabic, slot, vanilla, current));
                        if (next != null) current = next;
                    }
                    catch (Exception ex) { LogCallbackFailure("image override " + registration.Owner + ":" + registration.Definition.OverrideId, ex); }
                }
                if (!ReferenceEquals(current, vanilla)) SetImage(self, slot, current);
            }
        }

        private static bool IsImageOverrideContextReady()
        {
            int localPlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            if (localPlayerId > 0) return true;
            return EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID > 0;
        }

        private void OnBeforeRender()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (!refreshRequested && !HasCategories(UnitHudSurface.All)) return;

            // Instance is a lazy constructor. Vanilla marks the view model loaded before
            // HUD_Main assigns HUDmain, so both signals are required before HUD work starts.
            if (!MainViewModel.viewModelLoaded) return;
            MainViewModel main = MainViewModel.Instance;
            if (main?.HUDmain == null) return;

            bool refresh;
            lock (sync)
            {
                refresh = refreshRequested;
                refreshRequested = false;
            }
            TryApplyFrameArea("refresh", () =>
            {
                if (refresh)
                {
                    if (main?.Show_HUD_Troops == true) main.HUDTroopPanel?.SetupSelectedTroops();
                    if (main?.Show_HUD_ControlGroups == true) main.HUDControlGroups?.Update();
                    if (main != null && hasSpriteContext && IsImageOverrideContextReady())
                    {
                        // A refresh bypasses the public detour entry, preventing resolver-driven
                        // refresh requests or later hook chains from recursively re-entering us.
                        updateSpritesActive = true;
                        try
                        {
                            updateSpritesOriginal(main, lastSpriteColour, lastSpriteArabic);
                            ApplyImageOverrides(main, lastSpriteColour, lastSpriteArabic);
                        }
                        finally { updateSpritesActive = false; }
                    }
                }
            });
            TryApplyFrameArea("hover presentation", () => ApplyHover(main));
            TryApplyFrameArea("army-report presentation", () => ApplyArmyReport(main), HideArmyHosts);
            TryApplyFrameArea("recruitment presentation", () => ApplyRecruitmentPresentation(main), HideRecruitmentControls);
            TryApplyFrameArea("unit-detail presentation", () => ApplyUnitDetails(main), HideUnitDetailControls);
        }

        private void TryApplyFrameArea(string area, Action action, Action failureCleanup = null)
        {
            try { action(); }
            catch (Exception ex)
            {
                try { failureCleanup?.Invoke(); }
                catch (Exception cleanupEx) { LogCallbackFailure(area + " cleanup", cleanupEx); }
                LogCallbackFailure(area, ex);
            }
        }

        private void ApplyHover(MainViewModel main)
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state == null || main == null || !TryCapture(state.in_chimp, out UnitHudUnitSnapshot unit)) return;
            CategoryRegistration category = Classify(unit, UnitHudSurface.UnitHover);
            if (category != null) main.ChimpTypeText = ResolveText(category, UnitHudTextKind.DisplayName);
        }

        private void ApplyRecruitmentPresentation(MainViewModel main)
        {
            if (!HasCategories(UnitHudSurface.Recruitment) || main?.HUDBuildingPanel == null) return;
            EnsureRecruitmentControls(main);
            lock (sync)
            {
                if (recruitmentLease != null && Time.realtimeSinceStartup >= recruitmentLease.ExpiresAt)
                {
                    NativeApiLog.Error(log, $"Unit HUD recruitment ticket {recruitmentLease.Ticket.TicketId} timed out; recruitment lock released.");
                    recruitmentLease = null;
                }
            }
            RecruitmentRegistration active = GetActiveRecruitment((int)eChimps.CHIMP_TYPE_ARCHER);
            bool any = RecruitmentCopy((int)eChimps.CHIMP_TYPE_ARCHER).Length > 0;
            archerVariantSelector.Visibility = any && main.Show_BarracksArcher ? Visibility.Visible : Visibility.Collapsed;
            archerVariantSelector.Content = active == null ? "A" : ResolveText(active.Category, UnitHudTextKind.ShortLabel);
            archerVariantSelector.ToolTip = active == null ? "Vanilla Archer" :
                ResolveText(active.Category, UnitHudTextKind.DisplayName) + "\n" + ResolveText(active.Category, UnitHudTextKind.Description);
            archerVariantSelector.IsEnabled = recruitmentLease == null;
            if (active == null)
            {
                archerVariantTint.Visibility = Visibility.Collapsed;
                return;
            }
            ImageSource source = ResolveCategoryImage(active.Category, null);
            archerVariantTint.OpacityMask = source == null ? null : new ImageBrush(source);
            archerVariantTint.Background = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue,
                active.Category.Definition.Tint.Red, active.Category.Definition.Tint.Green, active.Category.Definition.Tint.Blue));
            archerVariantTint.Opacity = 0.22f * active.Category.Definition.Tint.Alpha / byte.MaxValue;
            archerVariantTint.Visibility = main.Show_BarracksArcher ? Visibility.Visible : Visibility.Collapsed;
            if (main.lastTroopBuildChimp == Enums.eChimps.CHIMP_TYPE_ARCHER) ApplyRecruitmentText(main, active);
        }

        private void ApplyRecruitmentText(MainViewModel main, RecruitmentRegistration active)
        {
            if (active == null || main == null) return;
            string suffix = main.lastTroopsAmountToMake > 1 ? " x" + main.lastTroopsAmountToMake : string.Empty;
            main.TroopNameCostText = ResolveText(active.Category, UnitHudTextKind.DisplayName) + suffix;
        }

        private void EnsureRecruitmentControls(MainViewModel main)
        {
            Button selector = main.HUDBuildingPanel.FindName("APISharedArcherVariantSelector") as Button;
            Border tint = main.HUDBuildingPanel.FindName("APISharedArcherVariantTint") as Border;
            if (selector == null || tint == null) throw new MissingMemberException("APIShared Archer recruitment controls");
            if (!ReferenceEquals(archerVariantSelector, selector))
            {
                if (archerVariantSelector != null) archerVariantSelector.PreviewMouseDown -= OnRecruitmentSelectorMouseDown;
                archerVariantSelector = selector;
                archerVariantSelector.PreviewMouseDown += OnRecruitmentSelectorMouseDown;
            }
            archerVariantTint = tint;
        }

        private void HideRecruitmentControls()
        {
            if (archerVariantSelector != null) archerVariantSelector.Visibility = Visibility.Collapsed;
            if (archerVariantTint != null) archerVariantTint.Visibility = Visibility.Collapsed;
        }

        private void OnRecruitmentSelectorMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (args == null || (args.ChangedButton != MouseButton.Left && args.ChangedButton != MouseButton.Right)) return;
            const int baseType = (int)eChimps.CHIMP_TYPE_ARCHER;
            RecruitmentRegistration[] choices = RecruitmentCopy(baseType);
            if (choices.Length == 0) return;
            lock (sync)
            {
                if (recruitmentLease != null) { args.Handled = true; return; }
                int current = -1;
                if (activeRecruitment.TryGetValue(baseType, out string key)) current = Array.FindIndex(choices, x => x.Category.Key == key);
                int next = args.ChangedButton == MouseButton.Left
                    ? current + 1
                    : current < 0 ? choices.Length - 1 : current - 1;
                if (next < 0 || next >= choices.Length) activeRecruitment.Remove(baseType);
                else activeRecruitment[baseType] = choices[next].Category.Key;
                refreshRequested = true;
            }
            args.Handled = true;
        }

        private RecruitmentRegistration[] RecruitmentCopy(int baseType)
        {
            lock (sync) return recruitment.Where(x => x.Category.Definition.BaseUnitType == baseType).ToArray();
        }

        private void ApplyUnitDetails(MainViewModel main)
        {
            if (main?.HUDBuildingPanel == null || !HasCategories(UnitHudSurface.UnitDetails)) return;
            EnsureDetailControls(main);
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            CategoryRegistration category = state != null && TryCapture(state.in_chimp, out UnitHudUnitSnapshot unit)
                ? Classify(unit, UnitHudSurface.UnitDetails)
                : null;
            if (category == null)
            {
                unitDetailHost.Visibility = Visibility.Collapsed;
                return;
            }
            ImageSource source = ResolveCategoryImage(category, null);
            unitDetailImage.Source = source;
            unitDetailTint.OpacityMask = source == null ? null : new ImageBrush(source);
            unitDetailTint.Background = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue,
                category.Definition.Tint.Red, category.Definition.Tint.Green, category.Definition.Tint.Blue));
            unitDetailTint.Opacity = 0.22f * category.Definition.Tint.Alpha / byte.MaxValue;
            unitDetailDescription.Text = ResolveText(category, UnitHudTextKind.Description);
            unitDetailHost.Visibility = Visibility.Visible;
        }

        private void EnsureDetailControls(MainViewModel main)
        {
            Grid host = main.HUDBuildingPanel.FindName("APISharedUnitDetailHost") as Grid;
            Image image = main.HUDBuildingPanel.FindName("APISharedUnitDetailImage") as Image;
            Border tint = main.HUDBuildingPanel.FindName("APISharedUnitDetailTint") as Border;
            TextBlock description = main.HUDBuildingPanel.FindName("APISharedUnitDetailDescription") as TextBlock;
            if (host == null || image == null || tint == null || description == null) throw new MissingMemberException("APIShared unit detail controls");
            unitDetailHost = host; unitDetailImage = image; unitDetailTint = tint; unitDetailDescription = description;
        }

        private void HideUnitDetailControls()
        {
            if (unitDetailHost != null) unitDetailHost.Visibility = Visibility.Collapsed;
        }

        private string ResolveText(CategoryRegistration category, UnitHudTextKind kind)
        {
            UnitHudTextProfile profile = category.Definition.TextProfile;
            try
            {
                string resolved = profile?.Resolver?.Invoke(kind);
                if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
            }
            catch (Exception ex) { LogCallbackFailure("text resolver " + category.Key, ex); }
            if (kind == UnitHudTextKind.ShortLabel && !string.IsNullOrWhiteSpace(profile?.ShortLabelFallback)) return profile.ShortLabelFallback;
            if (kind == UnitHudTextKind.Description) return profile?.DescriptionFallback ?? string.Empty;
            return !string.IsNullOrWhiteSpace(profile?.DisplayNameFallback) ? profile.DisplayNameFallback : category.Definition.DisplayName;
        }

        private void ApplyArmyReport(MainViewModel main)
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (main == null || state == null || state.troop_counts == null || !HasCategories(UnitHudSurface.ArmyReport)) return;
            Panel host = main.HUDBuildingPanel?.FindName("APISharedArmyCategoriesHost") as Panel;
            if (host == null) throw new MissingMemberException("APISharedArmyCategoriesHost");
            int local = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            var custom = new Dictionary<string, List<UnitHudUnitSnapshot>>(StringComparer.Ordinal);
            var reductions = new Dictionary<int, int>();
            var desiredCounts = new Dictionary<int, int>();
            foreach (int baseType in CategoryCopy().Where(x => HasSurface(x, UnitHudSurface.ArmyReport)).Select(x => x.Definition.BaseUnitType).Distinct())
            {
                int reportIndex = ToArmyReportIndex(baseType);
                if (reportIndex > 0 && reportIndex < main.AllTroops.Count && reportIndex - 1 < state.troop_counts.Length)
                    desiredCounts[reportIndex] = state.troop_counts[reportIndex - 1];
            }
            foreach (int unitId in GameUnitManagerAPI.Instance.GetAllAliveUnits())
            {
                if (!TryCapture(unitId, out UnitHudUnitSnapshot unit) || unit.OwnerPlayerId != local) continue;
                CategoryRegistration category = Classify(unit, UnitHudSurface.ArmyReport);
                if (category == null) continue;
                if (!custom.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> list)) custom[category.Key] = list = new List<UnitHudUnitSnapshot>();
                list.Add(unit);
                reductions[unit.VanillaType] = reductions.TryGetValue(unit.VanillaType, out int count) ? count + 1 : 1;
            }
            foreach (KeyValuePair<int, int> reduction in reductions)
            {
                int reportIndex = ToArmyReportIndex(reduction.Key);
                if (desiredCounts.TryGetValue(reportIndex, out int vanillaCount))
                    desiredCounts[reportIndex] = Math.Max(0, vanillaCount - reduction.Value);
            }
            RenderArmyHosts(host, custom);
            foreach (KeyValuePair<int, int> desired in desiredCounts)
                main.AllTroops[desired.Key] = desired.Value;
        }

        private readonly Dictionary<string, Noesis.Grid> armyEntries = new Dictionary<string, Noesis.Grid>(StringComparer.Ordinal);
        private void HideArmyHosts()
        {
            foreach (Noesis.Grid grid in armyEntries.Values)
                if (grid != null) grid.Visibility = Visibility.Collapsed;
        }

        private void RenderArmyHosts(Panel host, Dictionary<string, List<UnitHudUnitSnapshot>> custom)
        {
            foreach (Noesis.Grid grid in armyEntries.Values) grid.Visibility = Visibility.Collapsed;
            foreach (CategoryRegistration category in CategoryCopy().Where(x => HasSurface(x, UnitHudSurface.ArmyReport)))
            {
                if (!custom.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> units) || units.Count == 0) continue;
                if (!armyEntries.TryGetValue(category.Key, out Noesis.Grid grid))
                {
                    grid = new Noesis.Grid { Width = 64, Height = 76, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
                    var image = new Image { Width = 52, Height = 52, VerticalAlignment = VerticalAlignment.Top, Source = ResolveCategoryImage(category, null) };
                    var count = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Foreground = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, 174, 214, byte.MaxValue)), FontSize = 18 };
                    grid.Children.Add(image); grid.Children.Add(count);
                    host.Children.Add(grid);
                    armyEntries[category.Key] = grid;
                }
                ((TextBlock)grid.Children[1]).Text = units.Count.ToString();
                grid.Margin = new Thickness(4, 0, 4, 0);
                grid.Visibility = Visibility.Visible;
            }
        }

        private List<DisplayEntry> BuildEntries(List<UnitHudUnitSnapshot> selected, int[] vanillaCounts, UnitHudSurface surface)
        {
            var claimed = new Dictionary<string, List<UnitHudUnitSnapshot>>(StringComparer.Ordinal);
            var reduction = new int[vanillaCounts.Length];
            foreach (UnitHudUnitSnapshot unit in selected)
            {
                CategoryRegistration category = Classify(unit, surface);
                if (category == null) continue;
                if (!claimed.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> list)) claimed[category.Key] = list = new List<UnitHudUnitSnapshot>();
                list.Add(unit);
                if (unit.VanillaType >= 0 && unit.VanillaType < reduction.Length) reduction[unit.VanillaType]++;
            }
            var result = new List<DisplayEntry>();
            CategoryRegistration[] registrations = CategoryCopy();
            for (int type = 0; type < vanillaCounts.Length; type++)
            {
                int normal = Math.Max(0, vanillaCounts[type] - reduction[type]);
                if (normal > 0) result.Add(new DisplayEntry(type, normal));
                foreach (CategoryRegistration category in registrations.Where(x => x.Definition.BaseUnitType == type && HasSurface(x, surface)))
                    if (claimed.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> units) && units.Count > 0) result.Add(new DisplayEntry(category, units));
            }
            return result;
        }

        private List<UnitHudUnitSnapshot> CaptureSelectedUnits()
        {
            var result = new List<UnitHudUnitSnapshot>();
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state?.selectedChimps == null) return result;
            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            var seen = new HashSet<int>();
            for (int i = 0; i < count; i++)
                if (seen.Add(state.selectedChimps[i]) && TryCapture(state.selectedChimps[i], out UnitHudUnitSnapshot snapshot)) result.Add(snapshot);
            return result;
        }

        private static bool TryCapture(int unitId, out UnitHudUnitSnapshot snapshot)
        {
            snapshot = null;
            if (unitId <= 0 || GameUnitManagerAPI.Instance == null || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null || unit->r_AliveState != AliveState.IsAlive || unit->r_GlobalId == 0) return false;
            snapshot = new UnitHudUnitSnapshot(unitId, unit->r_GlobalId, (int)unit->r_UnitChimp, unit->r_ControllableForPlayerId, true);
            return true;
        }

        private CategoryRegistration Classify(UnitHudUnitSnapshot unit, UnitHudSurface surface)
        {
            CategoryRegistration match = null;
            foreach (CategoryRegistration category in CategoryCopy())
            {
                if (!HasSurface(category, surface) || category.Definition.BaseUnitType != unit.VanillaType || !Claims(category, unit)) continue;
                if (match != null)
                {
                    string conflict = unit.GameId + ":" + unit.GlobalId + ":" + surface + ":" + match.Key + ":" + category.Key;
                    lock (sync)
                    {
                        if (loggedCategoryConflicts.Add(conflict))
                            NativeApiLog.Error(log, $"Unit HUD category conflict for unit={unit.GameId}, global={unit.GlobalId}: {match.Key} and {category.Key}; Vanilla category retained.");
                    }
                    return null;
                }
                match = category;
            }
            return match;
        }

        private bool Claims(CategoryRegistration category, UnitHudUnitSnapshot unit)
        {
            try { return category.Matcher(unit); }
            catch (Exception ex) { LogCallbackFailure("category matcher " + category.Key, ex); return false; }
        }

        private bool IsClaimed(UnitHudUnitSnapshot unit, UnitHudSurface surface) => Classify(unit, surface) != null;
        private bool HasDerivedCategory(int type) => CategoryCopy().Any(x => x.Definition.BaseUnitType == type && HasSurface(x, UnitHudSurface.TroopSelection));
        private bool HasCategories(UnitHudSurface surface) => CategoryCopy().Any(x => HasSurface(x, surface));
        private static bool HasSurface(CategoryRegistration category, UnitHudSurface surface) => (category.Definition.Surfaces & surface) != 0;
        private CategoryRegistration[] CategoryCopy() { lock (sync) return categories.ToArray(); }
        private CategoryRegistration GetCategory(string key) => CategoryCopy().FirstOrDefault(x => x.Key == key);
        private UnitHudCategorySnapshot Snapshot(CategoryRegistration category, IList<UnitHudUnitSnapshot> units) => new UnitHudCategorySnapshot(category.Owner, category.Definition.CategoryId, ResolveText(category, UnitHudTextKind.DisplayName), units.ToArray());

        private IReadOnlyList<UnitHudCategorySnapshot> CaptureSelectedCategories()
        {
            var grouped = new Dictionary<string, List<UnitHudUnitSnapshot>>(StringComparer.Ordinal);
            foreach (UnitHudUnitSnapshot unit in CaptureSelectedUnits())
            {
                CategoryRegistration category = Classify(unit, UnitHudSurface.TroopSelection);
                if (category == null) continue;
                if (!grouped.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> items)) grouped[category.Key] = items = new List<UnitHudUnitSnapshot>();
                items.Add(unit);
            }
            return CategoryCopy().Where(x => grouped.ContainsKey(x.Key)).Select(x => Snapshot(x, grouped[x.Key])).ToArray();
        }

        private IReadOnlyList<UnitHudControlGroupSnapshot> CaptureControlGroups()
        {
            lock (sync)
            {
                if (!groupRecordsAvailable) return Array.Empty<UnitHudControlGroupSnapshot>();
                var result = new List<UnitHudControlGroupSnapshot>(GroupCount);
                for (int group = 0; group < GroupCount; group++)
                {
                    var members = new List<UnitHudUnitSnapshot>();
                    int* start = groupRecords + group * GroupCapacity * GroupRecordWidth;
                    for (int index = 0; index < GroupCapacity; index++)
                    {
                        int gameId = start[index * GroupRecordWidth];
                        int globalId = start[index * GroupRecordWidth + 1];
                        if (TryCapture(gameId, out UnitHudUnitSnapshot unit) &&
                            unchecked((int)unit.GlobalId) == globalId)
                        {
                            members.Add(unit);
                        }
                    }
                    result.Add(new UnitHudControlGroupSnapshot(group, members.ToArray()));
                }
                return result;
            }
        }

        private bool RemoveUnitFromControlGroups(
            int unitId,
            out int removedCount,
            out NativeCapabilityDiagnostic diagnostic)
        {
            removedCount = 0;
            if (unitId <= 0)
            {
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.UnitHudPresentation,
                    NativeCapabilityState.ValidationFailed,
                    binaryHash,
                    "A positive one-based unit game ID is required.");
                return false;
            }
            lock (sync)
            {
                if (!groupRecordsAvailable)
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.UnitHudPresentation,
                        NativeCapabilityState.UnsupportedBuild,
                        binaryHash,
                        "Native control-group storage is unavailable for this build.");
                    return false;
                }
                for (int group = 0; group < GroupCount; group++)
                {
                    int* start = groupRecords + group * GroupCapacity * GroupRecordWidth;
                    for (int index = 0; index < GroupCapacity; index++)
                    {
                        int* record = start + index * GroupRecordWidth;
                        if (record[0] != unitId)
                            continue;
                        record[0] = -1;
                        removedCount++;
                    }
                }
            }
            diagnostic = new NativeCapabilityDiagnostic(
                NativeCapabilityIds.UnitHudPresentation,
                NativeCapabilityState.Available,
                binaryHash,
                $"Removed unit ID {unitId} from {removedCount} native control-group records.");
            return true;
        }

        private void NotifyInteraction(UnitHudInteractionContext context)
        {
            InteractionRegistration[] copy; lock (sync) copy = interactions.ToArray();
            foreach (InteractionRegistration item in copy) try { item.Handler(context); } catch (Exception ex) { LogCallbackFailure("interaction " + item.Owner + ":" + item.Id, ex); }
        }

        private ImageSource ResolveCategoryImage(CategoryRegistration category, HUD_Troops panel)
        {
            try
            {
                ImageSource source = category.Definition.ImageResolver?.Invoke();
                if (source != null) return source;
            }
            catch (Exception ex) { LogCallbackFailure("category image " + category.Key, ex); }
            int summary = ToSummaryType(category.Definition.BaseUnitType);
            if (summary >= 0 && MainViewModel.Instance?.HUDControlGroups != null)
                return GroupSpriteMethod.Invoke(MainViewModel.Instance.HUDControlGroups, new object[] { summary }) as ImageSource;
            return MainViewModel.Instance?.UIBuildingsO001;
        }

        private static void ApplyButtonImage(Button button, ImageSource source)
        {
            PropEx.SetSprite1(button, source); PropEx.SetSprite2(button, source); PropEx.SetSprite3(button, source); PropEx.SetSprite4(button, source);
        }

        private static void SetPageButtons(HUD_Troops panel, int page, int pages)
        {
            Button next = panel.FindName("ButtonTroopPanelPage1") as Button;
            Button previous = panel.FindName("ButtonTroopPanelPage2") as Button;
            if (next != null) PropEx.SetButtonVisibility(next, pages > 1 && page < pages - 1 ? Visibility.Visible : Visibility.Hidden);
            if (previous != null) PropEx.SetButtonVisibility(previous, pages > 1 && page > 0 ? Visibility.Visible : Visibility.Hidden);
        }

        private static int ToSummaryType(int unitType)
        {
            if (unitType == (int)eChimps.CHIMP_TYPE_TUNNELER) return TunnelSummaryType;
            if (unitType >= (int)eChimps.CHIMP_TYPE_ARCHER && unitType <= (int)eChimps.CHIMP_TYPE_ENGINEER)
                return unitType - (int)eChimps.CHIMP_TYPE_ARCHER + EuropeanTroopSummaryStart;
            if (unitType == (int)eChimps.CHIMP_TYPE_MONK) return MonkSummaryType;
            if (unitType >= (int)eChimps.CHIMP_TYPE_CATAPULT && unitType <= (int)eChimps.CHIMP_TYPE_MANGONEL)
                return unitType - (int)eChimps.CHIMP_TYPE_CATAPULT + SiegeEngineSummaryStart;
            if (unitType >= (int)eChimps.CHIMP_TYPE_SIEGE_TOWER && unitType <= (int)eChimps.CHIMP_TYPE_BALLISTA)
                return unitType - (int)eChimps.CHIMP_TYPE_SIEGE_TOWER + PortableSiegeSummaryStart;
            if (unitType >= (int)eChimps.CHIMP_TYPE_ARAB_BOW && unitType <= (int)eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER)
                return unitType - (int)eChimps.CHIMP_TYPE_ARAB_BOW + ArabicTroopSummaryStart;
            return -1;
        }

        private static int ToArmyReportIndex(int unitType)
        {
            int summary = ToSummaryType(unitType);
            return summary >= 0 ? summary + 1 : -1;
        }

        private static ImageSource GetImage(MainViewModel main, UnitHudImageSlot slot)
        {
            switch (slot)
            {
                case UnitHudImageSlot.UIBuildingsO011: return main.UIBuildingsO011;
                case UnitHudImageSlot.UIBuildingsO012: return main.UIBuildingsO012;
                case UnitHudImageSlot.UIButtonsK007: return main.UIButtonsK007;
                case UnitHudImageSlot.UIButtonsK008: return main.UIButtonsK008;
                case UnitHudImageSlot.UIButtonsO016: return main.UIButtonsO016;
                case UnitHudImageSlot.UIButtonsO017: return main.UIButtonsO017;
                case UnitHudImageSlot.UIButtonsO018: return main.UIButtonsO018;
                default: return null;
            }
        }

        private static void SetImage(MainViewModel main, UnitHudImageSlot slot, ImageSource image)
        {
            switch (slot)
            {
                case UnitHudImageSlot.UIBuildingsO011: main.UIBuildingsO011 = image; break;
                case UnitHudImageSlot.UIBuildingsO012: main.UIBuildingsO012 = image; break;
                case UnitHudImageSlot.UIButtonsK007: main.UIButtonsK007 = image; break;
                case UnitHudImageSlot.UIButtonsK008: main.UIButtonsK008 = image; break;
                case UnitHudImageSlot.UIButtonsO016: main.UIButtonsO016 = image; break;
                case UnitHudImageSlot.UIButtonsO017: main.UIButtonsO017 = image; break;
                case UnitHudImageSlot.UIButtonsO018: main.UIButtonsO018 = image; break;
            }
        }

        private void LogCallbackFailure(string area, Exception ex)
        {
            lock (sync)
                if (!loggedCallbackFailures.Add(area)) return;
            NativeApiLog.Error(log, $"Unit HUD {area} failed closed; unaffected Vanilla presentation remains active: {ex}");
        }

        private static FieldInfo RequireField(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingFieldException(type.FullName, name);
        private static MethodInfo RequireMethod(Type type, string name, Type[] parameters) => type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);

        private static int FindUnique(ReadOnlySpan<byte> memory, string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int found = -1;
            for (int offset = 0; offset <= memory.Length - tokens.Length; offset++)
            {
                bool match = true;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i] == "?") continue;
                    if (memory[offset + i] != Convert.ToByte(tokens[i], 16)) { match = false; break; }
                }
                if (!match) continue;
                if (found >= 0) return -2;
                found = offset;
            }
            return found;
        }

        private sealed class Binding : IUnitHudPresentationCapability
        {
            private readonly UnitHudPresentationService service;
            private readonly string owner;
            internal Binding(UnitHudPresentationService service, string owner) { this.service = service; this.owner = owner; }
            public bool TryRegisterCategory(UnitHudCategoryDefinition definition, UnitHudCategoryMatcher matcher, out NativeCapabilityDiagnostic diagnostic) => service.RegisterCategory(owner, definition, matcher, out diagnostic);
            public bool TryRegisterInteraction(string registrationId, UnitHudInteractionHandler handler, out NativeCapabilityDiagnostic diagnostic) => service.RegisterInteraction(owner, registrationId, handler, out diagnostic);
            public bool TryRegisterImageOverride(UnitHudImageOverrideDefinition definition, UnitHudImageOverrideResolver resolver, out NativeCapabilityDiagnostic diagnostic) => service.RegisterImage(owner, definition, resolver, out diagnostic);
            public bool TryRegisterRecruitment(string categoryId, UnitHudRecruitmentHandler handler, out NativeCapabilityDiagnostic diagnostic) => service.RegisterRecruitment(owner, categoryId, handler, out diagnostic);
            public bool TryCompleteRecruitment(UnitHudRecruitmentTicket ticket, int matchedCount, string reason, out NativeCapabilityDiagnostic diagnostic) => service.CompleteRecruitment(owner, ticket, matchedCount, reason, out diagnostic);
            public IReadOnlyList<UnitHudSlotSnapshot> GetVisibleTroopSlots() { lock (service.sync) return service.visibleSlots.ToArray(); }
            public IReadOnlyList<UnitHudCategorySnapshot> GetSelectedCategories() => service.CaptureSelectedCategories();
            public IReadOnlyList<UnitHudControlGroupSnapshot> GetControlGroups() => service.CaptureControlGroups();
            public bool TryRemoveUnitFromControlGroups(int unitId, out int removedCount, out NativeCapabilityDiagnostic diagnostic) =>
                service.RemoveUnitFromControlGroups(unitId, out removedCount, out diagnostic);
            public void RequestRefresh() { lock (service.sync) service.refreshRequested = true; }
        }

        private sealed class CategoryRegistration
        {
            internal CategoryRegistration(string owner, UnitHudCategoryDefinition definition, UnitHudCategoryMatcher matcher) { Owner = owner; Definition = definition; Matcher = matcher; }
            internal string Owner { get; }
            internal UnitHudCategoryDefinition Definition { get; }
            internal UnitHudCategoryMatcher Matcher { get; }
            internal string Key => Owner + ":" + Definition.CategoryId;
        }
        private sealed class InteractionRegistration
        {
            internal InteractionRegistration(string owner, string id, UnitHudInteractionHandler handler) { Owner = owner; Id = id; Handler = handler; }
            internal string Owner { get; } internal string Id { get; } internal UnitHudInteractionHandler Handler { get; }
        }
        private sealed class ImageRegistration
        {
            internal ImageRegistration(string owner, UnitHudImageOverrideDefinition definition, UnitHudImageOverrideResolver resolver) { Owner = owner; Definition = definition; Resolver = resolver; }
            internal string Owner { get; } internal UnitHudImageOverrideDefinition Definition { get; } internal UnitHudImageOverrideResolver Resolver { get; }
        }
        private sealed class RecruitmentRegistration
        {
            internal RecruitmentRegistration(CategoryRegistration category, UnitHudRecruitmentHandler handler) { Category = category; Handler = handler; }
            internal CategoryRegistration Category { get; } internal UnitHudRecruitmentHandler Handler { get; }
        }
        private sealed class RecruitmentLease
        {
            internal RecruitmentLease(UnitHudRecruitmentTicket ticket, float expiresAt) { Ticket = ticket; ExpiresAt = expiresAt; }
            internal UnitHudRecruitmentTicket Ticket { get; } internal float ExpiresAt { get; }
        }
        private sealed class DisplayEntry
        {
            internal DisplayEntry(int type, int count) { VanillaType = type; Count = count; }
            internal DisplayEntry(CategoryRegistration category, List<UnitHudUnitSnapshot> units) { VanillaType = category.Definition.BaseUnitType; Category = category; Units = units; Count = units.Count; }
            internal int VanillaType { get; } internal int Count { get; } internal CategoryRegistration Category { get; } internal List<UnitHudUnitSnapshot> Units { get; }
        }
        private sealed class GroupEntry
        {
            internal GroupEntry(CategoryRegistration category, int summaryType, int baseType) { Category = category; SummaryType = summaryType; BaseType = baseType; }
            internal CategoryRegistration Category { get; } internal int SummaryType { get; } internal int BaseType { get; } internal int Count { get; set; }
            internal string SortKey => Category != null ? "1:" + Category.Key : "0:" + SummaryType.ToString("D2");
        }
    }
}
