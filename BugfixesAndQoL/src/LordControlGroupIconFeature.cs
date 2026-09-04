// Feature: Replace the Lord's internal Archer placeholder with a dedicated control-group icon.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LordControlGroupIconFeature : IDisposable
    {
        private const string IconSourceName = "BugfixesAndQoLLordControlGroupIconSource";
        private delegate void PopulateDelegate(HUD_ControlGroups self);
        private delegate void ButtonClickedDelegate(HUD_ControlGroups self, string command);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int* controlGroupRecords;
        private readonly FieldInfo troopImagesField;
        private readonly FieldInfo troopValuesField;
        private readonly FieldInfo troopExtraValuesField;
        private readonly FieldInfo deleteButtonsField;
        private readonly FieldInfo selectButtonsField;
        private readonly FieldInfo troopRowIdsField;
        private readonly Brush rowIdColourBlack;
        private readonly Brush rowIdColourLight;
        private readonly MethodInfo getTroopSpriteMethod;
        private Hook populateHook;
        private PopulateDelegate populateOriginal;
        private Hook buttonClickedHook;
        private ButtonClickedDelegate buttonClickedOriginal;
        private HUD_ControlGroups pendingRefreshPanel;
        private int pendingRefreshFrame = -1;
        private bool callbackErrorLogged;
        private bool active;
        private bool disposed;

        internal LordControlGroupIconFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ulong controlGroupRecordsAddress)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (controlGroupRecordsAddress == 0)
                throw new ArgumentOutOfRangeException(nameof(controlGroupRecordsAddress));
            controlGroupRecords = (int*)controlGroupRecordsAddress;

            MethodInfo populateMethod = typeof(HUD_ControlGroups).GetMethod(
                "populate",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (populateMethod == null || populateMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(HUD_ControlGroups).FullName, "populate()");
            MethodInfo buttonClickedMethod = typeof(HUD_ControlGroups).GetMethod(
                "ButtonClicked",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            if (buttonClickedMethod == null || buttonClickedMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(HUD_ControlGroups).FullName, "ButtonClicked(string)");

            troopImagesField = RequirePrivateField("RefTroopImages", typeof(Image[,]));
            troopValuesField = RequirePrivateField("RefTroopValues", typeof(TextBlock[,]));
            troopExtraValuesField = RequirePrivateField("RefTroopExtraValues", typeof(TextBlock[]));
            deleteButtonsField = RequireField("RefDeleteButtons", typeof(Button[]));
            selectButtonsField = RequireField("RefSelectButtons", typeof(Button[]));
            troopRowIdsField = RequireField("RefTroopRowID", typeof(TextBlock[]));
            rowIdColourBlack = RequireStaticField("RowIDColour_Black", typeof(SolidColorBrush)).GetValue(null) as Brush;
            rowIdColourLight = RequireStaticField("RowIDColour_Light", typeof(SolidColorBrush)).GetValue(null) as Brush;
            if (rowIdColourBlack == null || rowIdColourLight == null)
                throw new InvalidOperationException("Vanilla's control-group row brushes are unavailable.");
            getTroopSpriteMethod = typeof(HUD_ControlGroups).GetMethod(
                "GetTroopSprite",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int) },
                null);
            if (getTroopSpriteMethod == null || getTroopSpriteMethod.ReturnType != typeof(ImageSource))
                throw new MissingMethodException(typeof(HUD_ControlGroups).FullName, "GetTroopSprite(int)");

            try
            {
                populateHook = new Hook(populateMethod, (PopulateDelegate)PopulateHook);
                populateOriginal = populateHook.GenerateTrampoline<PopulateDelegate>();
                buttonClickedHook = new Hook(buttonClickedMethod, (ButtonClickedDelegate)ButtonClickedHook);
                buttonClickedOriginal = buttonClickedHook.GenerateTrampoline<ButtonClickedDelegate>();
                Application.onBeforeRender += OnBeforeRender;
                active = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            // Stop callbacks before touching the detour so a partial removal cannot
            // reactivate against an independently restored native patch.
            active = false;
            Application.onBeforeRender -= OnBeforeRender;
            ClearPendingRefresh();
            Exception firstFailure = null;
            TryDisposeHook(ref buttonClickedHook, ref firstFailure);
            TryDisposeHook(ref populateHook, ref firstFailure);
            buttonClickedOriginal = null;
            populateOriginal = null;
            disposed = true;
            if (firstFailure != null)
                throw new InvalidOperationException("The Lord control-group UI hooks could not be removed completely.", firstFailure);
        }

        private void ButtonClickedHook(HUD_ControlGroups self, string command)
        {
            buttonClickedOriginal(self, command);
            if (disposed || !active || !settings.EnableMod || !settings.EnableLordUnitControls ||
                !LordControlGroupIconPolicy.IsGroupMutationCommand(command))
            {
                return;
            }

            // Re-run Vanilla immediately, then once more on the next render in case the
            // GameAction write is deferred. The populate hook reconciles the authoritative
            // native group membership instead of waiting for a new PlayState snapshot.
            pendingRefreshPanel = self;
            pendingRefreshFrame = Time.frameCount + 1;
            RefreshPanel(self);
        }

        private void OnBeforeRender()
        {
            HUD_ControlGroups panel = pendingRefreshPanel;
            if (disposed || !active || panel == null || Time.frameCount < pendingRefreshFrame)
                return;

            MainViewModel main = MainViewModel.Instance;
            if (!settings.EnableMod || !settings.EnableLordUnitControls ||
                main?.Show_HUD_ControlGroups != true || !ReferenceEquals(main.HUDControlGroups, panel))
            {
                ClearPendingRefresh();
                return;
            }

            ClearPendingRefresh();
            RefreshPanel(panel);
        }

        private void ClearPendingRefresh()
        {
            pendingRefreshPanel = null;
            pendingRefreshFrame = -1;
        }

        private void RefreshPanel(HUD_ControlGroups panel)
        {
            try
            {
                panel.Update();
            }
            catch (Exception ex)
            {
                ReportCallbackError(ex);
            }
        }

        private void PopulateHook(HUD_ControlGroups self)
        {
            populateOriginal(self);
            if (disposed || !active || !settings.EnableMod || !settings.EnableLordUnitControls)
                return;

            try
            {
                ApplyLordIcons(self);
            }
            catch (Exception ex)
            {
                ReportCallbackError(ex);
            }
        }

        private void ReportCallbackError(Exception ex)
        {
            if (callbackErrorLogged)
                return;

            callbackErrorLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL Lord control-group refresh failed closed; Vanilla summary remains visible: {ex}");
        }

        private void ApplyLordIcons(HUD_ControlGroups panel)
        {
            if (!TryGetControlledLord(out int lordUnitId, out int lordGlobalId))
                return;

            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state == null)
                return;
            Image iconHolder = ((FrameworkElement)panel).FindName(IconSourceName) as Image;
            ImageSource lordIcon = iconHolder?.Source;

            var troopImages = troopImagesField.GetValue(panel) as Image[,];
            var troopValues = troopValuesField.GetValue(panel) as TextBlock[,];
            var troopExtraValues = troopExtraValuesField.GetValue(panel) as TextBlock[];
            var deleteButtons = deleteButtonsField.GetValue(panel) as Button[];
            var selectButtons = selectButtonsField.GetValue(panel) as Button[];
            var troopRowIds = troopRowIdsField.GetValue(panel) as TextBlock[];
            if (troopImages == null || troopValues == null || troopExtraValues == null ||
                deleteButtons == null || selectButtons == null || troopRowIds == null ||
                troopImages.GetLength(0) < ControlGroupNativeDefinition.ControlGroupCount ||
                troopImages.GetLength(1) < LordControlGroupIconPolicy.VisibleSlotCount ||
                troopValues.GetLength(0) < ControlGroupNativeDefinition.ControlGroupCount ||
                troopValues.GetLength(1) < LordControlGroupIconPolicy.VisibleSlotCount ||
                troopExtraValues.Length < ControlGroupNativeDefinition.ControlGroupCount ||
                deleteButtons.Length < ControlGroupNativeDefinition.ControlGroupCount ||
                selectButtons.Length < ControlGroupNativeDefinition.ControlGroupCount ||
                troopRowIds.Length < ControlGroupNativeDefinition.ControlGroupCount)
            {
                throw new InvalidOperationException("The control-group HUD references differ from Vanilla's ten-by-four layout.");
            }

            var types = new int[LordControlGroupIconPolicy.VisibleSlotCount];
            var counts = new int[LordControlGroupIconPolicy.VisibleSlotCount];
            for (int group = 0; group < ControlGroupNativeDefinition.ControlGroupCount; group++)
            {
                NativeGroupSnapshot native = ReadNativeGroup(group, lordUnitId, lordGlobalId);
                int managedTotal = state.control_groups_total != null &&
                    group < state.control_groups_total.Length
                    ? state.control_groups_total[group]
                    : 0;
                if (native.Total == 0)
                {
                    if (managedTotal > 0)
                    {
                        HideGroup(
                            group,
                            troopImages,
                            troopValues,
                            troopExtraValues,
                            deleteButtons,
                            selectButtons,
                            troopRowIds);
                    }
                    continue;
                }
                if (!native.ContainsLord || lordIcon == null)
                    continue;

                int summaryOffset = checked(group * LordControlGroupIconPolicy.VisibleSlotCount);
                for (int slot = 0; slot < LordControlGroupIconPolicy.VisibleSlotCount; slot++)
                {
                    bool hasManagedSlot = state.control_groups_count != null &&
                        state.control_groups_type != null &&
                        summaryOffset + slot < state.control_groups_count.Length &&
                        summaryOffset + slot < state.control_groups_type.Length;
                    types[slot] = hasManagedSlot ? state.control_groups_type[summaryOffset + slot] : 0;
                    counts[slot] = hasManagedSlot ? state.control_groups_count[summaryOffset + slot] : 0;
                }

                if (native.Total == 1)
                    Array.Clear(counts, 0, counts.Length);
                int summarizedArchers = GetSummaryCount(
                    types,
                    counts,
                    LordControlGroupIconPolicy.EuropeanArcherSummaryType);
                bool summaryAlreadyIncludesLord = summarizedArchers > native.EuropeanArcherCount;
                LordControlGroupIconPolicy.InsertLord(types, counts, summaryAlreadyIncludesLord);
                RenderGroup(
                    panel,
                    group,
                    native.Total,
                    types,
                    counts,
                    lordIcon,
                    troopImages,
                    troopValues,
                    troopExtraValues,
                    deleteButtons,
                    selectButtons,
                    troopRowIds);
            }
        }

        private NativeGroupSnapshot ReadNativeGroup(int group, int lordUnitId, int lordGlobalId)
        {
            int recordOffset = checked(
                group * ControlGroupNativeDefinition.ControlGroupCapacity *
                ControlGroupNativeDefinition.ControlGroupRecordIntCount);
            int* records = controlGroupRecords + recordOffset;
            int total = 0;
            int europeanArchers = 0;
            bool containsLord = false;
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            if (units == null)
                return default(NativeGroupSnapshot);
            for (int index = 0; index < ControlGroupNativeDefinition.ControlGroupCapacity; index++)
            {
                int* record = records + index * ControlGroupNativeDefinition.ControlGroupRecordIntCount;
                int unitId = record[0];
                if (unitId <= 0 || !units.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || (int)unit->r_GlobalId != record[1])
                {
                    continue;
                }

                total++;
                if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARCHER)
                    europeanArchers++;
                if (unitId == lordUnitId && record[1] == lordGlobalId)
                    containsLord = true;
            }
            return new NativeGroupSnapshot(total, europeanArchers, containsLord);
        }

        private void RenderGroup(
            HUD_ControlGroups panel,
            int group,
            int total,
            int[] types,
            int[] counts,
            ImageSource lordIcon,
            Image[,] troopImages,
            TextBlock[,] troopValues,
            TextBlock[] troopExtraValues,
            Button[] deleteButtons,
            Button[] selectButtons,
            TextBlock[] troopRowIds)
        {
            PropEx.SetButtonVisibility(deleteButtons[group], Visibility.Visible);
            PropEx.SetButtonVisibility(selectButtons[group], Visibility.Visible);
            troopRowIds[group].Foreground = rowIdColourBlack;
            for (int slot = 0; slot < LordControlGroupIconPolicy.VisibleSlotCount; slot++)
            {
                Image image = troopImages[group, slot];
                TextBlock value = troopValues[group, slot];
                if (image == null || value == null)
                    throw new InvalidOperationException("A control-group HUD slot is not initialized.");
                if (counts[slot] > 0)
                {
                    image.Source = types[slot] == LordControlGroupIconPolicy.LordVisualType
                        ? lordIcon
                        : GetTroopSprite(panel, types[slot]);
                    image.Visibility = Visibility.Visible;
                    value.Text = counts[slot].ToString();
                    value.Visibility = Visibility.Visible;
                }
                else
                {
                    image.Visibility = Visibility.Hidden;
                    value.Visibility = Visibility.Hidden;
                }
            }

            int extra = LordControlGroupIconPolicy.CalculateExtraCount(total, counts);
            TextBlock extraValue = troopExtraValues[group];
            if (extraValue == null)
                throw new InvalidOperationException("A control-group remainder field is not initialized.");
            extraValue.Text = extra > 0 ? "+" + extra : string.Empty;
            extraValue.Visibility = extra > 0 ? Visibility.Visible : Visibility.Hidden;
        }

        private void HideGroup(
            int group,
            Image[,] troopImages,
            TextBlock[,] troopValues,
            TextBlock[] troopExtraValues,
            Button[] deleteButtons,
            Button[] selectButtons,
            TextBlock[] troopRowIds)
        {
            for (int slot = 0; slot < LordControlGroupIconPolicy.VisibleSlotCount; slot++)
            {
                troopImages[group, slot].Visibility = Visibility.Hidden;
                troopValues[group, slot].Visibility = Visibility.Hidden;
            }
            PropEx.SetButtonVisibility(deleteButtons[group], Visibility.Hidden);
            PropEx.SetButtonVisibility(selectButtons[group], Visibility.Hidden);
            troopExtraValues[group].Visibility = Visibility.Hidden;
            troopRowIds[group].Foreground = rowIdColourLight;
        }

        private static int GetSummaryCount(int[] types, int[] counts, int wantedType)
        {
            for (int slot = 0; slot < LordControlGroupIconPolicy.VisibleSlotCount; slot++)
            {
                if (counts[slot] > 0 && types[slot] == wantedType)
                    return counts[slot];
            }
            return 0;
        }

        private ImageSource GetTroopSprite(HUD_ControlGroups panel, int type) =>
            getTroopSpriteMethod.Invoke(panel, new object[] { type }) as ImageSource;

        private static void TryDisposeHook(ref Hook hook, ref Exception firstFailure)
        {
            Hook current = hook;
            hook = null;
            if (current == null)
                return;

            try
            {
                current.Undo();
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }
            try
            {
                current.Dispose();
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }
        }

        private static FieldInfo RequirePrivateField(string name, Type expectedType)
        {
            FieldInfo field = typeof(HUD_ControlGroups).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != expectedType)
            {
                throw new MissingFieldException(
                    typeof(HUD_ControlGroups).FullName,
                    $"{name}: {expectedType.FullName}");
            }
            return field;
        }

        private static FieldInfo RequireField(string name, Type expectedType)
        {
            FieldInfo field = typeof(HUD_ControlGroups).GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType != expectedType)
                throw new MissingFieldException(typeof(HUD_ControlGroups).FullName, $"{name}: {expectedType.FullName}");
            return field;
        }

        private static FieldInfo RequireStaticField(string name, Type expectedType)
        {
            FieldInfo field = typeof(HUD_ControlGroups).GetField(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType != expectedType)
                throw new MissingFieldException(typeof(HUD_ControlGroups).FullName, $"{name}: {expectedType.FullName}");
            return field;
        }

        private readonly struct NativeGroupSnapshot
        {
            internal NativeGroupSnapshot(int total, int europeanArcherCount, bool containsLord)
            {
                Total = total;
                EuropeanArcherCount = europeanArcherCount;
                ContainsLord = containsLord;
            }

            internal int Total { get; }
            internal int EuropeanArcherCount { get; }
            internal bool ContainsLord { get; }
        }

        private static bool TryGetControlledLord(out int unitId, out int globalId)
        {
            unitId = -1;
            globalId = -1;
            int playerId = Shared.GameModeHelper.IsMapEditor()
                ? (EditorDirector.instance?.ActivePlayerID ?? -1)
                : (GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1);
            if (playerId < 1 || playerId > 8 ||
                GamePlayerManagerAPI.Instance == null || GameUnitManagerAPI.Instance == null)
            {
                return false;
            }

            unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD)
            {
                return false;
            }

            globalId = (int)unit->r_GlobalId;
            return globalId != 0;
        }
    }
}
