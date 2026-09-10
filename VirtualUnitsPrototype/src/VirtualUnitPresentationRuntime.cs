using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using UnityEngine;
using VirtualUnitsPrototype.API;

namespace VirtualUnitsPrototype
{
    internal sealed unsafe class VirtualUnitPresentationRuntime : IDisposable
    {
        private const int ReplaceControlGroup = -1;
        private const int AddToControlGroup = 10;
        private const int DeleteControlGroup = 20;
        private const int ControlGroupCount = (int)Enums.KeyFunctions.SelectClan9 - (int)Enums.KeyFunctions.SelectClan0 + 1;
        private readonly VirtualEntityRuntime runtime;
        private readonly ManualLogSource log;
        private readonly object groupSync = new object();
        private readonly Dictionary<int, Dictionary<int, uint>> shadowGroups = new Dictionary<int, Dictionary<int, uint>>();
        private Hook selectedTypesHook;
        private Hook leftClickHook;
        private Hook rightClickHook;
        private Hook gameActionHook;
        private SelectedTypesDelegate selectedTypesTrampoline;
        private TroopClickDelegate leftClickTrampoline;
        private TroopClickDelegate rightClickTrampoline;
        private GameActionDelegate gameActionTrampoline;
        private int lastFrame = -1;
        private int pendingSelectedGroup = -1;
        private int pendingSelectedGroupFrame = -1;
        private bool controlGroupsAwaitingRestore;
        private Noesis.TextBlock normalArcherArmyCount;
        private bool frameErrorLogged;

        private delegate int[] SelectedTypesDelegate(EditorDirector self);
        private delegate void TroopClickDelegate(MainViewModel self, object parameter);
        private delegate void GameActionDelegate(Enums.KeyFunctions command, int value1, int value2, int value3);

        internal VirtualUnitPresentationRuntime(VirtualEntityRuntime runtime, ManualLogSource log)
        {
            this.runtime = runtime; this.log = log;
            ViewModel = new VirtualUnitPresentationViewModel(SelectOnlyDesertArchers, RemoveDesertArchers);
        }

        internal VirtualUnitPresentationViewModel ViewModel { get; }

        internal void Install()
        {
            selectedTypesHook = new Hook(RequireMethod(typeof(EditorDirector), "getSelectedChimpTypes", Type.EmptyTypes), (SelectedTypesDelegate)SelectedTypesHook);
            selectedTypesTrampoline = selectedTypesHook.GenerateTrampoline<SelectedTypesDelegate>();
            leftClickHook = new Hook(RequireMethod(typeof(MainViewModel), "TroopsLeftClickCommand", new[] { typeof(object) }), (TroopClickDelegate)LeftClickHook);
            leftClickTrampoline = leftClickHook.GenerateTrampoline<TroopClickDelegate>();
            rightClickHook = new Hook(RequireMethod(typeof(MainViewModel), "TroopsRightClickCommand", new[] { typeof(object) }), (TroopClickDelegate)RightClickHook);
            rightClickTrampoline = rightClickHook.GenerateTrampoline<TroopClickDelegate>();
            gameActionHook = new Hook(RequireMethod(typeof(EngineInterface), "GameAction", new[] { typeof(Enums.KeyFunctions), typeof(int), typeof(int), typeof(int) }), (GameActionDelegate)GameActionHook);
            gameActionTrampoline = gameActionHook.GenerateTrampoline<GameActionDelegate>();
            Application.onBeforeRender += OnBeforeRender;
            Shared.DebugLogHelper.LogInfo(log, "Distinct-unit presentation hooks installed for selected types, troop filters, and control-group actions.");
        }

        internal void ResetForMapLifecycle()
        {
            lock (groupSync) shadowGroups.Clear();
            pendingSelectedGroup = -1; pendingSelectedGroupFrame = -1; controlGroupsAwaitingRestore = false;
            normalArcherArmyCount = null;
            ViewModel.Reset();
        }

        internal ControlGroupSaveRecord[] ExportControlGroups()
        {
            lock (groupSync) return shadowGroups.SelectMany(group => group.Value.Select(item => new ControlGroupSaveRecord
            { Group = group.Key, GameId = item.Key, GlobalId = item.Value, TypeId = VirtualUnitsPlugin.DesertArcherId })).ToArray();
        }

        internal void ImportControlGroups(IEnumerable<ControlGroupSaveRecord> records)
        {
            lock (groupSync)
            {
                shadowGroups.Clear(); controlGroupsAwaitingRestore = true;
                foreach (ControlGroupSaveRecord record in records ?? Array.Empty<ControlGroupSaveRecord>())
                {
                    if (record.Group < 0 || record.Group >= ControlGroupCount || record.GameId <= 0 || record.GlobalId == 0 || record.TypeId != VirtualUnitsPlugin.DesertArcherId) continue;
                    if (!shadowGroups.TryGetValue(record.Group, out Dictionary<int, uint> group)) shadowGroups.Add(record.Group, group = new Dictionary<int, uint>());
                    group[record.GameId] = record.GlobalId;
                }
            }
        }
        internal void FinishRestore() => controlGroupsAwaitingRestore = false;

        private int[] SelectedTypesHook(EditorDirector self)
        {
            int[] result = selectedTypesTrampoline(self);
            if (result == null) return null;
            foreach (VirtualUnitSelectionSnapshot category in GetSelectedCategories())
            {
                if (!TryGetDefinition(category.TypeId, out VirtualUnitDefinition definition)) continue;
                int index = (int)definition.BaseType;
                if (index >= 0 && index < result.Length) result[index] = Math.Max(0, result[index] - category.Count);
            }
            return result;
        }

        private void LeftClickHook(MainViewModel self, object parameter)
        {
            bool archer = IsArcherParameter(self, parameter);
            leftClickTrampoline(self, parameter);
            if (archer) ApplyExactSelection(item => item.UnitType == (int)eChimps.CHIMP_TYPE_ARCHER && !IsVirtual(item.UnitId));
        }

        private void RightClickHook(MainViewModel self, object parameter)
        {
            bool archer = IsArcherParameter(self, parameter);
            rightClickTrampoline(self, parameter);
            if (archer) ApplyExactSelection(item => item.UnitType != (int)eChimps.CHIMP_TYPE_ARCHER || IsVirtual(item.UnitId));
        }

        private void GameActionHook(Enums.KeyFunctions command, int value1, int value2, int value3)
        {
            SelectedUnitInfo[] before = SafeSelection();
            gameActionTrampoline(command, value1, value2, value3);
            if (TryGetGroup(command, false, out int group))
            {
                Dictionary<int, uint> selectedVirtual = CaptureVirtualIdentities(before);
                lock (groupSync)
                {
                if (value1 == DeleteControlGroup) shadowGroups.Remove(group);
                else if (value1 == AddToControlGroup)
                {
                    if (!shadowGroups.TryGetValue(group, out Dictionary<int, uint> existing)) shadowGroups.Add(group, existing = new Dictionary<int, uint>());
                    foreach (KeyValuePair<int, uint> item in selectedVirtual) existing[item.Key] = item.Value;
                }
                else if (value1 == ReplaceControlGroup) shadowGroups[group] = selectedVirtual;
                }
            }
            else if (TryGetGroup(command, true, out group))
            {
                pendingSelectedGroup = group;
                pendingSelectedGroupFrame = Time.frameCount;
            }
        }

        private void OnBeforeRender()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (pendingSelectedGroup >= 0 && Time.frameCount > pendingSelectedGroupFrame)
                {
                    lock (groupSync) shadowGroups[pendingSelectedGroup] = CaptureVirtualIdentities(SafeSelection());
                    pendingSelectedGroup = -1;
                }
                RefreshViewModel();
            }
            catch (Exception ex)
            {
                if (frameErrorLogged) return;
                frameErrorLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Distinct-unit presentation frame failed closed: {ex}");
            }
        }

        private void RefreshViewModel()
        {
            VirtualUnitSelectionSnapshot desert = GetSelectedCategories().FirstOrDefault(x => x.TypeId == VirtualUnitsPlugin.DesertArcherId);
            int selectedVirtual = desert?.Count ?? 0;
            int localPlayer = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            int virtualArmy = localPlayer > 0 ? runtime.GetValidatedUnitsForPlayer(localPlayer).Count(x => x.TypeId == VirtualUnitsPlugin.DesertArcherId) : 0;
            int allArchers = 0;
            if (localPlayer > 0)
            {
                foreach (int unitId in GameUnitManagerAPI.Instance.GetAllAliveUnits())
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && unit != null && unit->r_ControllableForPlayerId == localPlayer && unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARCHER) allArchers++;
            }
            int[] groupCounts = new int[ControlGroupCount];
            lock (groupSync) foreach (int group in shadowGroups.Keys.ToArray())
            {
                Dictionary<int, uint> entries = shadowGroups[group];
                if (!controlGroupsAwaitingRestore) foreach (int unitId in entries.Keys.ToArray())
                {
                    if (!runtime.TryGetValidatedUnit(unitId, out VirtualEntityInstance instance, out VirtualUnitDefinition definition) || instance.Key.GlobalId != entries[unitId] || definition.TypeId != VirtualUnitsPlugin.DesertArcherId)
                        entries.Remove(unitId);
                }
                groupCounts[group] = entries.Count;
            }
            ViewModel.Update(selectedVirtual, Math.Max(0, allArchers - virtualArmy), virtualArmy, groupCounts);
            Noesis.TextBlock armyCount = GameXAMLManagerAPI.Instance?.FindGlobalElement("VirtualUnitsPrototypeArmyNormalArcherCount") as Noesis.TextBlock;
            if (armyCount != null)
            {
                normalArcherArmyCount = armyCount;
                normalArcherArmyCount.Text = Math.Max(0, allArchers - virtualArmy).ToString();
            }
            UpdateHoverName();
        }

        private void UpdateHoverName()
        {
            if (GameData.Instance?.lastGameState == null || MainViewModel.Instance == null) return;
            int unitId = GameData.Instance.lastGameState.in_chimp;
            if (runtime.TryGetValidatedUnit(unitId, out VirtualEntityInstance ignored, out VirtualUnitDefinition definition))
                MainViewModel.Instance.ChimpTypeText = definition.DisplayName;
        }

        private void SelectOnlyDesertArchers() => ApplyExactSelection(item => IsVirtualType(item.UnitId, VirtualUnitsPlugin.DesertArcherId));
        private void RemoveDesertArchers() => ApplyExactSelection(item => !IsVirtualType(item.UnitId, VirtualUnitsPlugin.DesertArcherId));

        private void ApplyExactSelection(Func<SelectedUnitInfo, bool> predicate)
        {
            SelectedUnitInfo[] selected = SafeSelection();
            if (selected.Length == 0) return;
            int[] ids = selected.Where(item => item.UnitId > 0 && predicate(item)).Select(item => item.UnitId).Distinct().ToArray();
            EngineInterface.TroopSelectionChanged(ids);
        }

        private bool IsVirtual(int unitId) => runtime.TryGetValidatedUnit(unitId, out VirtualEntityInstance ignored, out VirtualUnitDefinition definition) && definition.PresentationProfile.ShowAsDistinctCategory;
        private bool IsVirtualType(int unitId, string typeId) => runtime.TryGetValidatedUnit(unitId, out VirtualEntityInstance instance, out VirtualUnitDefinition ignored) && instance.TypeId == typeId;
        private bool TryGetDefinition(string typeId, out VirtualUnitDefinition definition) => runtime.TryGetDefinition(typeId, out definition).Succeeded;
        private VirtualUnitSelectionSnapshot[] GetSelectedCategories() => runtime.GetSelectedVirtualUnits(out IReadOnlyList<VirtualUnitSelectionSnapshot> result).Succeeded ? result.ToArray() : Array.Empty<VirtualUnitSelectionSnapshot>();
        private SelectedUnitInfo[] SafeSelection() { try { return GamePlayerManagerAPI.Instance?.GetSelectedChimps() ?? Array.Empty<SelectedUnitInfo>(); } catch { return Array.Empty<SelectedUnitInfo>(); } }
        private Dictionary<int, uint> CaptureVirtualIdentities(IEnumerable<SelectedUnitInfo> selected)
        {
            var result = new Dictionary<int, uint>();
            foreach (SelectedUnitInfo item in selected)
                if (runtime.TryGetValidatedUnit(item.UnitId, out VirtualEntityInstance instance, out VirtualUnitDefinition definition) && definition.PresentationProfile.ShowAsDistinctCategory) result[item.UnitId] = instance.Key.GlobalId;
            return result;
        }

        private static bool IsArcherParameter(MainViewModel self, object parameter)
        {
            try { return self.getChimpEnum(parameter as string) == Enums.eChimps.CHIMP_TYPE_ARCHER; }
            catch { return false; }
        }

        private static bool TryGetGroup(Enums.KeyFunctions command, bool select, out int group)
        {
            Enums.KeyFunctions first = select ? Enums.KeyFunctions.SelectClan0 : Enums.KeyFunctions.GroupTroops0;
            Enums.KeyFunctions last = select ? Enums.KeyFunctions.SelectClan9 : Enums.KeyFunctions.GroupTroops9;
            if ((int)command < (int)first || (int)command > (int)last) { group = -1; return false; }
            group = (int)command - (int)first; return true;
        }

        private static MethodInfo RequireMethod(Type type, string name, Type[] parameters) => type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);

        public void Dispose()
        {
            Application.onBeforeRender -= OnBeforeRender;
            gameActionHook?.Dispose(); rightClickHook?.Dispose(); leftClickHook?.Dispose(); selectedTypesHook?.Dispose();
        }
    }

    internal sealed class VirtualUnitPresentationViewModel : INotifyPropertyChanged
    {
        private int selectedCount;
        private int normalArmyCount;
        private int virtualArmyCount;
        private readonly int[] groupCounts = new int[(int)Enums.KeyFunctions.SelectClan9 - (int)Enums.KeyFunctions.SelectClan0 + 1];
        public VirtualUnitPresentationViewModel(Action select, Action remove)
        {
            SelectDistinctCommand = new SHCDESE.NoesisUtil.RelayCommand(select);
            RemoveDistinctCommand = new SHCDESE.NoesisUtil.RelayCommand(remove);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand SelectDistinctCommand { get; }
        public ICommand RemoveDistinctCommand { get; }
        public string DisplayName => "Desert Archer";
        public int SelectedCount => selectedCount;
        public bool SelectedVisible => selectedCount > 0;
        public int NormalArcherArmyCount => normalArmyCount;
        public int VirtualArcherArmyCount => virtualArmyCount;
        public bool VirtualArmyVisible => virtualArmyCount > 0;
        public Noesis.ImageSource SelectedIcon => MainViewModel.Instance?.UIButtonsK023;
        public Noesis.ImageSource ArmyIcon => MainViewModel.Instance?.UIBuildingsO001;
        public string Group0Text => GroupText(0); public string Group1Text => GroupText(1);
        public string Group2Text => GroupText(2); public string Group3Text => GroupText(3);
        public string Group4Text => GroupText(4); public string Group5Text => GroupText(5);
        public string Group6Text => GroupText(6); public string Group7Text => GroupText(7);
        public string Group8Text => GroupText(8); public string Group9Text => GroupText(9);
        public void Update(int selected, int normalArmy, int virtualArmy, int[] groups)
        {
            if (selectedCount != selected) { selectedCount = selected; Changed(nameof(SelectedCount)); Changed(nameof(SelectedVisible)); }
            if (normalArmyCount != normalArmy) { normalArmyCount = normalArmy; Changed(nameof(NormalArcherArmyCount)); }
            if (virtualArmyCount != virtualArmy) { virtualArmyCount = virtualArmy; Changed(nameof(VirtualArcherArmyCount)); Changed(nameof(VirtualArmyVisible)); }
            for (int index = 0; index < groupCounts.Length; index++) if (groupCounts[index] != groups[index]) { groupCounts[index] = groups[index]; Changed("Group" + index + "Text"); }
        }
        public void Reset() => Update(0, 0, 0, new int[groupCounts.Length]);
        private string GroupText(int group) => groupCounts[group] > 0 ? "DA:" + groupCounts[group] : string.Empty;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
