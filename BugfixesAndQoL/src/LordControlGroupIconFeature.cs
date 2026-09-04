// Feature: Keep Vanilla's control-group summary current and give the Lord a dedicated icon.
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
        private const int ManagedSummaryArrayLength = 40;
        private delegate void PopulateDelegate(HUD_ControlGroups self);
        private delegate void KeyGameActionDelegate(
            Enums.KeyFunctions command,
            int value1,
            int value2,
            int value3);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int* controlGroupRecords;
        private readonly bool[] groupContainsControlledLord =
            new bool[ControlGroupNativeDefinition.ControlGroupCount];
        private readonly FieldInfo troopImagesField;
        private readonly FieldInfo troopValuesField;
        private readonly FieldInfo troopExtraValuesField;
        private readonly MethodInfo getTroopSpriteMethod;
        private Hook populateHook;
        private PopulateDelegate populateOriginal;
        private Hook keyGameActionHook;
        private KeyGameActionDelegate keyGameActionOriginal;
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
            MethodInfo keyGameActionMethod = RequireMethod(
                typeof(EngineInterface),
                "GameAction",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                new[]
                {
                    typeof(Enums.KeyFunctions),
                    typeof(int),
                    typeof(int),
                    typeof(int)
                });

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
                keyGameActionHook = new Hook(keyGameActionMethod, (KeyGameActionDelegate)KeyGameActionHook);
                keyGameActionOriginal = keyGameActionHook.GenerateTrampoline<KeyGameActionDelegate>();
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

            active = false;
            Array.Clear(groupContainsControlledLord, 0, groupContainsControlledLord.Length);
            Exception firstFailure = null;
            TryDisposeHook(ref keyGameActionHook, ref firstFailure);
            TryDisposeHook(ref populateHook, ref firstFailure);
            keyGameActionOriginal = null;
            populateOriginal = null;
            disposed = true;
            if (firstFailure != null)
                throw new InvalidOperationException("The Lord control-group UI hooks could not be removed completely.", firstFailure);
        }

        private void KeyGameActionHook(Enums.KeyFunctions command, int value1, int value2, int value3)
        {
            keyGameActionOriginal(command, value1, value2, value3);
            if (!active || !settings.EnableMod || !settings.EnableLordUnitControls ||
                !IsControlGroupMutation(command))
            {
                return;
            }

            try
            {
                MainViewModel main = MainViewModel.Instance;
                HUD_ControlGroups panel = main?.HUDControlGroups;
                if (panel != null && main.Show_HUD_ControlGroups)
                    panel.Update();
            }
            catch (Exception ex)
            {
                ReportCallbackError(ex);
            }
        }

        private static bool IsControlGroupMutation(Enums.KeyFunctions command) =>
            command >= Enums.KeyFunctions.GroupTroops0 &&
            command <= Enums.KeyFunctions.GroupTroops9;

        private void PopulateHook(HUD_ControlGroups self)
        {
            bool rebuilt = false;
            if (active && settings.EnableMod && settings.EnableLordUnitControls)
            {
                try
                {
                    rebuilt = RebuildVanillaSummary();
                }
                catch (Exception ex)
                {
                    ReportCallbackError(ex);
                }
            }

            // Vanilla owns the complete window. Only its three summary arrays are
            // refreshed beforehand from the same native records Vanilla maintains.
            populateOriginal(self);
            if (!rebuilt)
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

        private bool RebuildVanillaSummary()
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            GameUnitManagerAPI unitManager = GameUnitManagerAPI.Instance;
            int groupCount = ControlGroupNativeDefinition.ControlGroupCount;
            int visibleSlotCount = LordControlGroupIconPolicy.VisibleSlotCount;
            if (state == null)
                return false;

            bool hasControlledLord = TryGetControlledLord(out int lordUnitId, out int lordGlobalId);
            // Vanilla's converter uses a one-element total array as a sentinel outside
            // its native troop mode. Recreate the same 40-entry managed shape it emits
            // in troop mode so the unmodified HUD can populate for a sole Lord too.
            var totals = new short[ManagedSummaryArrayLength];
            var types = new byte[ManagedSummaryArrayLength];
            var counts = new short[ManagedSummaryArrayLength];
            var containsControlledLord = new bool[groupCount];

            for (int group = 0; group < groupCount; group++)
            {
                var categoryCounts = new int[LordControlGroupIconPolicy.SummaryTypeCount];
                int total = 0;
                int recordOffset = checked(
                    group * ControlGroupNativeDefinition.ControlGroupCapacity *
                    ControlGroupNativeDefinition.ControlGroupRecordIntCount);
                int* records = controlGroupRecords + recordOffset;

                for (int index = 0; index < ControlGroupNativeDefinition.ControlGroupCapacity; index++)
                {
                    int* record = records + index * ControlGroupNativeDefinition.ControlGroupRecordIntCount;
                    int unitId = record[0];
                    int globalId = record[1];
                    if (unitId <= 0 || !unitManager.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null || (int)unit->r_GlobalId != globalId)
                    {
                        continue;
                    }

                    total++;
                    if (hasControlledLord && unitId == lordUnitId && globalId == lordGlobalId)
                        containsControlledLord[group] = true;
                    if (LordControlGroupIconPolicy.TryGetSummaryType((int)unit->r_UnitChimp, out int summaryType))
                        categoryCounts[summaryType]++;
                }

                totals[group] = checked((short)total);
                var visibleTypes = new int[visibleSlotCount];
                var visibleCounts = new int[visibleSlotCount];
                LordControlGroupIconPolicy.SelectVisibleSummary(
                    categoryCounts,
                    visibleTypes,
                    visibleCounts);
                int summaryOffset = checked(group * visibleSlotCount);
                for (int slot = 0; slot < visibleSlotCount; slot++)
                {
                    types[summaryOffset + slot] = checked((byte)visibleTypes[slot]);
                    counts[summaryOffset + slot] = checked((short)visibleCounts[slot]);
                }
            }

            // These assignments cannot fail and happen only after every group was built,
            // so Vanilla never observes partially reconstructed rows.
            state.control_groups_total = totals;
            state.control_groups_type = types;
            state.control_groups_count = counts;
            Array.Copy(containsControlledLord, groupContainsControlledLord, groupCount);
            return true;
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
                if (state.control_groups_total[group] <= 0 || !groupContainsControlledLord[group])
                    continue;

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
            GamePlayerManagerAPI playerManager = GamePlayerManagerAPI.Instance;
            GameUnitManagerAPI unitManager = GameUnitManagerAPI.Instance;
            int playerId = Shared.GameModeHelper.IsMapEditor()
                ? (EditorDirector.instance?.ActivePlayerID ?? -1)
                : playerManager.GetLocalPlayerId();
            if (playerId < 1 || playerId > 8)
                return false;

            unitId = playerManager.GetLordUnitId(playerId);
            if (unitId <= 0 ||
                !unitManager.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD)
            {
                return false;
            }

            globalId = (int)unit->r_GlobalId;
            return globalId != 0;
        }
    }
}
