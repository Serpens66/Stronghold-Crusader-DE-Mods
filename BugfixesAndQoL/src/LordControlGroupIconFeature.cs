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

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int* controlGroupRecords;
        private readonly FieldInfo troopImagesField;
        private readonly FieldInfo troopValuesField;
        private readonly FieldInfo troopExtraValuesField;
        private readonly MethodInfo getTroopSpriteMethod;
        private Hook populateHook;
        private PopulateDelegate populateOriginal;
        private bool firstIconLogged;
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

            troopImagesField = RequirePrivateField("RefTroopImages", typeof(Image[,]));
            troopValuesField = RequirePrivateField("RefTroopValues", typeof(TextBlock[,]));
            troopExtraValuesField = RequirePrivateField("RefTroopExtraValues", typeof(TextBlock[]));
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
                active = true;
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL Lord control-group icon hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            // Stop callbacks before touching the detour so a partial removal cannot
            // reactivate against an independently restored native patch.
            active = false;
            populateHook?.Undo();
            populateHook?.Dispose();
            populateHook = null;
            populateOriginal = null;
            disposed = true;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL Lord control-group icon hook removed.");
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
                if (!callbackErrorLogged)
                {
                    callbackErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL Lord control-group icon rendering failed closed; Vanilla summary remains visible: {ex}");
                }
            }
        }

        private void ApplyLordIcons(HUD_ControlGroups panel)
        {
            if (!TryGetControlledLord(out int lordUnitId, out int lordGlobalId))
                return;

            Image iconHolder = ((FrameworkElement)panel).FindName(IconSourceName) as Image;
            ImageSource lordIcon = iconHolder?.Source;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (lordIcon == null || state?.control_groups_total == null ||
                state.control_groups_count == null || state.control_groups_type == null)
            {
                throw new InvalidOperationException("The Lord icon resource or control-group summary arrays are unavailable.");
            }

            var troopImages = troopImagesField.GetValue(panel) as Image[,];
            var troopValues = troopValuesField.GetValue(panel) as TextBlock[,];
            var troopExtraValues = troopExtraValuesField.GetValue(panel) as TextBlock[];
            if (troopImages == null || troopValues == null || troopExtraValues == null ||
                troopImages.GetLength(0) < LordControlGroupNativeDefinition.ControlGroupCount ||
                troopImages.GetLength(1) < LordControlGroupIconPolicy.VisibleSlotCount ||
                troopValues.GetLength(0) < LordControlGroupNativeDefinition.ControlGroupCount ||
                troopValues.GetLength(1) < LordControlGroupIconPolicy.VisibleSlotCount ||
                troopExtraValues.Length < LordControlGroupNativeDefinition.ControlGroupCount)
            {
                throw new InvalidOperationException("The control-group HUD references differ from Vanilla's ten-by-four layout.");
            }

            int renderedGroups = 0;
            var types = new int[LordControlGroupIconPolicy.VisibleSlotCount];
            var counts = new int[LordControlGroupIconPolicy.VisibleSlotCount];
            for (int group = 0; group < LordControlGroupNativeDefinition.ControlGroupCount; group++)
            {
                if (group >= state.control_groups_total.Length ||
                    state.control_groups_total[group] <= 0 ||
                    !ContainsLord(group, lordUnitId, lordGlobalId))
                {
                    continue;
                }

                int summaryOffset = checked(group * LordControlGroupIconPolicy.VisibleSlotCount);
                if (summaryOffset + LordControlGroupIconPolicy.VisibleSlotCount > state.control_groups_count.Length ||
                    summaryOffset + LordControlGroupIconPolicy.VisibleSlotCount > state.control_groups_type.Length)
                {
                    throw new InvalidOperationException("The control-group summary arrays are shorter than Vanilla's ten-by-four layout.");
                }

                for (int slot = 0; slot < LordControlGroupIconPolicy.VisibleSlotCount; slot++)
                {
                    types[slot] = state.control_groups_type[summaryOffset + slot];
                    counts[slot] = state.control_groups_count[summaryOffset + slot];
                }

                LordControlGroupIconPolicy.InsertLord(types, counts);
                RenderGroup(
                    panel,
                    group,
                    state.control_groups_total[group],
                    types,
                    counts,
                    lordIcon,
                    troopImages,
                    troopValues,
                    troopExtraValues);
                renderedGroups++;
            }

            if (renderedGroups > 0 && !firstIconLogged)
            {
                firstIconLogged = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Dedicated Lord icon rendered in {renderedGroups} control-group row(s).");
            }
        }

        private bool ContainsLord(int group, int lordUnitId, int lordGlobalId)
        {
            int recordOffset = checked(
                group * LordControlGroupNativeDefinition.ControlGroupCapacity *
                LordControlGroupNativeDefinition.ControlGroupRecordIntCount);
            int* records = controlGroupRecords + recordOffset;
            for (int index = 0; index < LordControlGroupNativeDefinition.ControlGroupCapacity; index++)
            {
                int* record = records + index * LordControlGroupNativeDefinition.ControlGroupRecordIntCount;
                if (record[0] == lordUnitId && record[1] == lordGlobalId)
                    return true;
            }
            return false;
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
