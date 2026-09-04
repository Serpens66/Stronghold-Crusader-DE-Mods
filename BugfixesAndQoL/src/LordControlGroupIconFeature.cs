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

namespace BugfixesAndQoL
{
    internal sealed unsafe class LordControlGroupIconFeature : IDisposable
    {
        private const string IconSourceName = "BugfixesAndQoLLordControlGroupIconSource";
        private delegate void PopulateDelegate(HUD_ControlGroups self);
        private delegate void ButtonClickedDelegate(HUD_ControlGroups self, string command);
        private delegate void SetGameStateDelegate(GameData self, EngineInterface.PlayState gameState);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int* controlGroupRecords;
        private readonly FieldInfo troopImagesField;
        private readonly FieldInfo troopValuesField;
        private readonly FieldInfo troopExtraValuesField;
        private readonly MethodInfo getTroopSpriteMethod;
        private Hook populateHook;
        private PopulateDelegate populateOriginal;
        private Hook buttonClickedHook;
        private ButtonClickedDelegate buttonClickedOriginal;
        private Hook setGameStateHook;
        private SetGameStateDelegate setGameStateOriginal;
        private HUD_ControlGroups pendingRefreshPanel;
        private bool hasObservedPatchedGameState;
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

            MethodInfo populateMethod = RequireMethod(
                typeof(HUD_ControlGroups),
                "populate",
                BindingFlags.Instance | BindingFlags.NonPublic,
                Type.EmptyTypes);
            MethodInfo buttonClickedMethod = RequireMethod(
                typeof(HUD_ControlGroups),
                "ButtonClicked",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                new[] { typeof(string) });
            MethodInfo setGameStateMethod = RequireMethod(
                typeof(GameData),
                "setGameState",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                new[] { typeof(EngineInterface.PlayState) });

            troopImagesField = RequirePrivateField("RefTroopImages", typeof(Image[,]));
            troopValuesField = RequirePrivateField("RefTroopValues", typeof(TextBlock[,]));
            troopExtraValuesField = RequirePrivateField("RefTroopExtraValues", typeof(TextBlock[]));
            getTroopSpriteMethod = RequireMethod(
                typeof(HUD_ControlGroups),
                "GetTroopSprite",
                BindingFlags.Instance | BindingFlags.NonPublic,
                new[] { typeof(int) },
                typeof(ImageSource));

            try
            {
                populateHook = new Hook(populateMethod, (PopulateDelegate)PopulateHook);
                populateOriginal = populateHook.GenerateTrampoline<PopulateDelegate>();
                buttonClickedHook = new Hook(buttonClickedMethod, (ButtonClickedDelegate)ButtonClickedHook);
                buttonClickedOriginal = buttonClickedHook.GenerateTrampoline<ButtonClickedDelegate>();
                setGameStateHook = new Hook(setGameStateMethod, (SetGameStateDelegate)SetGameStateHook);
                setGameStateOriginal = setGameStateHook.GenerateTrampoline<SetGameStateDelegate>();
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

            // Stop callbacks before removing any detour so partial teardown cannot
            // reactivate against an independently restored native patch.
            active = false;
            ClearPendingRefresh();
            Exception firstFailure = null;
            TryDisposeHook(ref setGameStateHook, ref firstFailure);
            TryDisposeHook(ref buttonClickedHook, ref firstFailure);
            TryDisposeHook(ref populateHook, ref firstFailure);
            setGameStateOriginal = null;
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

            // GameAction mutates native group storage but does not replace lastGameState.
            // Coalesce mutations and redraw only after Vanilla installs its next snapshot.
            pendingRefreshPanel = self;
        }

        private void SetGameStateHook(GameData self, EngineInterface.PlayState gameState)
        {
            setGameStateOriginal(self, gameState);

            try
            {
                bool firstPatchedGameState = !hasObservedPatchedGameState;
                hasObservedPatchedGameState = true;
                HUD_ControlGroups panel = pendingRefreshPanel;
                if (disposed || !active)
                    return;

                MainViewModel main = MainViewModel.Instance;
                if (panel == null && firstPatchedGameState && main?.Show_HUD_ControlGroups == true)
                    panel = main.HUDControlGroups;
                if (panel == null)
                    return;
                if (!settings.EnableMod || !settings.EnableLordUnitControls ||
                    main?.Show_HUD_ControlGroups != true || !ReferenceEquals(main.HUDControlGroups, panel))
                {
                    ClearPendingRefresh();
                    return;
                }

                ClearPendingRefresh();
                RefreshPanel(panel);
            }
            catch (Exception ex)
            {
                ClearPendingRefresh();
                ReportCallbackError(ex);
            }
        }

        private void ClearPendingRefresh() => pendingRefreshPanel = null;

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
            // A snapshot that predates the native summary patch may contain real Archers
            // without the Lord contribution. Leave that initial state entirely to Vanilla.
            if (!hasObservedPatchedGameState ||
                !TryGetControlledLord(out int lordUnitId, out int lordGlobalId))
                return;

            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state?.control_groups_total == null || state.control_groups_type == null ||
                state.control_groups_count == null ||
                state.control_groups_total.Length < ControlGroupNativeDefinition.ControlGroupCount ||
                state.control_groups_type.Length < ControlGroupNativeDefinition.ControlGroupCount * LordControlGroupIconPolicy.VisibleSlotCount ||
                state.control_groups_count.Length < ControlGroupNativeDefinition.ControlGroupCount * LordControlGroupIconPolicy.VisibleSlotCount)
            {
                return;
            }

            Image iconHolder = ((FrameworkElement)panel).FindName(IconSourceName) as Image;
            ImageSource lordIcon = iconHolder?.Source;
            if (lordIcon == null)
                return;

            var troopImages = troopImagesField.GetValue(panel) as Image[,];
            var troopValues = troopValuesField.GetValue(panel) as TextBlock[,];
            var troopExtraValues = troopExtraValuesField.GetValue(panel) as TextBlock[];
            if (troopImages == null || troopValues == null || troopExtraValues == null ||
                troopImages.GetLength(0) < ControlGroupNativeDefinition.ControlGroupCount ||
                troopImages.GetLength(1) < LordControlGroupIconPolicy.VisibleSlotCount ||
                troopValues.GetLength(0) < ControlGroupNativeDefinition.ControlGroupCount ||
                troopValues.GetLength(1) < LordControlGroupIconPolicy.VisibleSlotCount ||
                troopExtraValues.Length < ControlGroupNativeDefinition.ControlGroupCount)
            {
                throw new InvalidOperationException("The control-group HUD references differ from Vanilla's ten-by-four layout.");
            }

            var types = new int[LordControlGroupIconPolicy.VisibleSlotCount];
            var counts = new int[LordControlGroupIconPolicy.VisibleSlotCount];
            for (int group = 0; group < ControlGroupNativeDefinition.ControlGroupCount; group++)
            {
                if (state.control_groups_total[group] <= 0 ||
                    !NativeGroupContainsLord(group, lordUnitId, lordGlobalId))
                {
                    continue;
                }

                int summaryOffset = checked(group * LordControlGroupIconPolicy.VisibleSlotCount);
                for (int slot = 0; slot < LordControlGroupIconPolicy.VisibleSlotCount; slot++)
                {
                    types[slot] = state.control_groups_type[summaryOffset + slot];
                    counts[slot] = state.control_groups_count[summaryOffset + slot];
                }
                LordControlGroupIconPolicy.InsertLord(types, counts);
                RenderLordSummary(
                    panel,
                    group,
                    state.control_groups_total[group],
                    types,
                    counts,
                    lordIcon,
                    troopImages,
                    troopValues,
                    troopExtraValues);
            }
        }

        private bool NativeGroupContainsLord(int group, int lordUnitId, int lordGlobalId)
        {
            int recordOffset = checked(
                group * ControlGroupNativeDefinition.ControlGroupCapacity *
                ControlGroupNativeDefinition.ControlGroupRecordIntCount);
            int* records = controlGroupRecords + recordOffset;
            for (int index = 0; index < ControlGroupNativeDefinition.ControlGroupCapacity; index++)
            {
                int* record = records + index * ControlGroupNativeDefinition.ControlGroupRecordIntCount;
                if (record[0] == lordUnitId && record[1] == lordGlobalId)
                    return true;
            }
            return false;
        }

        private void RenderLordSummary(
            HUD_ControlGroups panel,
            int group,
            int total,
            int[] types,
            int[] counts,
            ImageSource lordIcon,
            Image[,] troopImages,
            TextBlock[,] troopValues,
            TextBlock[] troopExtraValues)
        {
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

        private static MethodInfo RequireMethod(
            Type declaringType,
            string name,
            BindingFlags bindingFlags,
            Type[] parameterTypes,
            Type returnType = null)
        {
            MethodInfo method = declaringType.GetMethod(name, bindingFlags, null, parameterTypes, null);
            Type expectedReturnType = returnType ?? typeof(void);
            if (method == null || method.ReturnType != expectedReturnType)
                throw new MissingMethodException(declaringType.FullName, name);
            return method;
        }

        private static FieldInfo RequirePrivateField(string name, Type expectedType)
        {
            FieldInfo field = typeof(HUD_ControlGroups).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != expectedType)
                throw new MissingFieldException(typeof(HUD_ControlGroups).FullName, $"{name}: {expectedType.FullName}");
            return field;
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
