// Feature: Add editable Vanilla maps to editor load dialogs and protect their source files.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class VanillaMapEditorHook : INotifyPropertyChanged, IDisposable
    {
        private delegate void PopulateListDelegate(HUD_LoadSaveRequester self);
        private delegate List<FileHeader> GetMapEditableMapsDelegate(
            MapFileManager self,
            int sortMode,
            bool sortAscend);
        private delegate void SaveSaveGameOrMapDelegate(
            EditorDirector self,
            string path,
            string mapName,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave);

        private static readonly FieldInfo RequesterTypeField = FindRequiredField(
            typeof(HUD_LoadSaveRequester),
            "requesterType");
        private static readonly FieldInfo FileListField = FindRequiredField(
            typeof(HUD_LoadSaveRequester),
            "RefFileLists");
        private static readonly FieldInfo ActionButtonField = FindRequiredField(
            typeof(HUD_LoadSaveRequester),
            "RefActionButton");
        private static readonly FieldInfo SelectedHeaderField = FindRequiredField(
            typeof(HUD_LoadSaveRequester),
            "selectedHeader");
        private static readonly MethodInfo PopulateListMethod = FindRequiredMethod(
            typeof(HUD_LoadSaveRequester),
            "populateList");
        private static readonly MethodInfo SortListMethod = FindRequiredMethod(
            typeof(MapFileManager),
            "SortList",
            typeof(List<FileHeader>),
            typeof(int),
            typeof(bool));

        [ThreadStatic]
        private static int loadEditorMapPopulateDepth;

        [ThreadStatic]
        private static int editorMapPopulateDepth;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<ListView, HUD_LoadSaveRequester> requesterByList =
            new Dictionary<ListView, HUD_LoadSaveRequester>();
        private Hook populateListHook;
        private Hook editableMapsHook;
        private Hook saveMapHook;
        private PopulateListDelegate populateListOriginal;
        private GetMapEditableMapsDelegate editableMapsOriginal;
        private SaveSaveGameOrMapDelegate saveMapOriginal;
        private bool listFailureLogged;
        private bool saveFailureLogged;
        private bool uiFailureLogged;
        private HUD_LoadSaveRequester activeRequester;
        private bool disposed;

        internal VanillaMapEditorHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            DeleteMapCommand = new RelayCommand(DeleteSelectedMap, CanDeleteSelectedMap);

            try
            {
                populateListHook = new Hook(
                    FindRequiredMethod(typeof(HUD_LoadSaveRequester), "populateList"),
                    (PopulateListDelegate)PopulateListHook);
                populateListOriginal = populateListHook.GenerateTrampoline<PopulateListDelegate>();

                editableMapsHook = new Hook(
                    FindRequiredMethod(
                        typeof(MapFileManager),
                        nameof(MapFileManager.GetMapEditableMaps),
                        typeof(int),
                        typeof(bool)),
                    (GetMapEditableMapsDelegate)GetMapEditableMapsHook);
                editableMapsOriginal = editableMapsHook.GenerateTrampoline<GetMapEditableMapsDelegate>();

                saveMapHook = new Hook(
                    FindRequiredMethod(
                        typeof(EditorDirector),
                        nameof(EditorDirector.SaveSaveGameOrMap),
                        typeof(string),
                        typeof(string),
                        typeof(bool),
                        typeof(bool),
                        typeof(bool)),
                    (SaveSaveGameOrMapDelegate)SaveSaveGameOrMapHook);
                saveMapOriginal = saveMapHook.GenerateTrampoline<SaveSaveGameOrMapDelegate>();

                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLShowVanillaMapsHost",
                    this);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLDeleteMapHost",
                    this);
                settings.PropertyChanged += SettingsPropertyChanged;
            }
            catch
            {
                DisposeHook(ref saveMapHook);
                DisposeHook(ref editableMapsHook);
                DisposeHook(ref populateListHook);
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Vanilla map-editor hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            settings.PropertyChanged -= SettingsPropertyChanged;
            foreach (KeyValuePair<ListView, HUD_LoadSaveRequester> pair in requesterByList)
                pair.Key.SelectionChanged -= RequesterSelectionChanged;
            requesterByList.Clear();
            DisposeHook(ref saveMapHook);
            DisposeHook(ref editableMapsHook);
            DisposeHook(ref populateListHook);
            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Vanilla map-editor hooks disposed.");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand DeleteMapCommand { get; }

        public bool ShowVanillaMaps
        {
            get => settings.ShowVanillaMapsInEditor;
            set
            {
                if (settings.ShowVanillaMapsInEditor != value)
                    settings.ShowVanillaMapsInEditor = value;
            }
        }

        public string ShowVanillaMapsText => settings.ShowVanillaMapsInEditorText;
        public string ShowVanillaMapsHelpText => settings.ShowVanillaMapsInEditorHelpText;
        public string DeleteMapText => SerpLocalization.Get("BugfixesAndQoL.DeleteMap");
        public string DeleteMapHelpText => SerpLocalization.Get("BugfixesAndQoL.DeleteMapHelp");

        public Visibility ShowVanillaMapsVisibility =>
            FeatureEnabled && GetActiveRequesterType() == Enums.RequesterTypes.LoadEditorMap
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility DeleteMapVisibility =>
            FeatureEnabled && IsEditorMapRequester(GetActiveRequesterType())
                ? Visibility.Visible
                : Visibility.Collapsed;

        public bool DeleteMapEnabled => CanDeleteSelectedMap();

        private bool FeatureEnabled => settings.EnableMod;

        private bool ShouldShowBuiltIns => FeatureEnabled && settings.ShowVanillaMapsInEditor;

        private void PopulateListHook(HUD_LoadSaveRequester self)
        {
            Enums.RequesterTypes requesterType = GetRequesterType(self);
            bool editorMapRequester = IsEditorMapRequester(requesterType);
            bool exposeBuiltIns = VanillaMapEditorPolicy.ShouldExposeBuiltIns(
                ShouldShowBuiltIns,
                requesterType == Enums.RequesterTypes.LoadEditorMap);
            if (editorMapRequester)
                editorMapPopulateDepth++;
            if (exposeBuiltIns)
                loadEditorMapPopulateDepth++;

            try
            {
                if (editorMapRequester)
                {
                    activeRequester = self;
                    AttachRequester(self);
                    RefreshUiState();
                }
                populateListOriginal(self);
            }
            finally
            {
                if (exposeBuiltIns)
                    loadEditorMapPopulateDepth--;
                if (editorMapRequester)
                    editorMapPopulateDepth--;
            }

            if (editorMapRequester)
                RefreshUiState();
        }

        private List<FileHeader> GetMapEditableMapsHook(
            MapFileManager self,
            int sortMode,
            bool sortAscend)
        {
            List<FileHeader> vanilla = editableMapsOriginal(self, sortMode, sortAscend);
            if (!FeatureEnabled || editorMapPopulateDepth <= 0)
                return vanilla;

            try
            {
                vanilla = VanillaMapEditorPolicy.RemoveMissingUserMaps(
                    vanilla,
                    header => header.builtinMap,
                    header => header.filePath,
                    File.Exists);
                if (loadEditorMapPopulateDepth <= 0)
                    return vanilla;

                IEnumerable<IEnumerable<FileHeader>> builtInGroups = new[]
                {
                    self.GetFreebuildMaps(sortMode, sortAscend, true, false, false),
                    self.GetInvasionMaps(sortMode, sortAscend, true, false, false),
                    self.GetMultiplayerMaps(sortMode, sortAscend, 0, true, false, false),
                };
                return VanillaMapEditorPolicy.MergeEditableBuiltIns(
                    vanilla,
                    builtInGroups,
                    header => header.builtinMap,
                    header => header.isMapEditable(),
                    header => header.filePath,
                    merged => SortWithVanilla(self, merged, sortMode, sortAscend));
            }
            catch (Exception ex)
            {
                if (!listFailureLogged)
                {
                    listFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL could not add Vanilla maps to the editor; Vanilla's user-map list remains active: {ex}");
                }
                return vanilla;
            }
        }

        private void SaveSaveGameOrMapHook(
            EditorDirector self,
            string path,
            string mapName,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave)
        {
            string safePath = path;
            try
            {
                safePath = VanillaMapEditorPolicy.ResolveProtectedSavePath(
                    path,
                    System.IO.Path.Combine(Application.streamingAssetsPath, "Maps"),
                    ConfigSettings.GetUserMapsPath(),
                    FeatureEnabled,
                    mapSave);
                if (!string.Equals(safePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL redirected a protected Vanilla map save from [{path}] to [{safePath}].");
                }
            }
            catch (Exception ex)
            {
                if (!saveFailureLogged)
                {
                    saveFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL could not validate map save path [{path}]; Vanilla behavior remains active: {ex}");
                }
            }

            saveMapOriginal(self, safePath, mapName, lockMap, tempLockOnly, mapSave);
        }

        private void AttachRequester(HUD_LoadSaveRequester requester)
        {
            try
            {
                ListView list = (ListView)FileListField.GetValue(requester);
                if (list == null || requesterByList.ContainsKey(list))
                    return;

                requesterByList.Add(list, requester);
                list.SelectionChanged += RequesterSelectionChanged;
            }
            catch (Exception ex)
            {
                LogUiFailure("attach the map-menu selection handler", ex);
            }
        }

        private void RequesterSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ListView list && requesterByList.TryGetValue(list, out HUD_LoadSaveRequester requester))
                activeRequester = requester;
            RefreshUiState();
        }

        private void SettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                args.PropertyName != nameof(BugfixesAndQoLViewModel.ShowVanillaMapsInEditor))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowVanillaMaps));
            RefreshUiState();
            if (activeRequester != null && IsEditorMapRequester(GetActiveRequesterType()))
                RefreshRequesterList(activeRequester);
        }

        private bool CanDeleteSelectedMap()
        {
            return TryGetSelectedDeletableMap(out _, out _);
        }

        private void DeleteSelectedMap()
        {
            if (!TryGetSelectedDeletableMap(out FileHeader header, out string safePath))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Bugfixes and QoL rejected a map deletion because the selection was not a direct user Maps file.");
                ShowDeleteError(string.Empty);
                RefreshUiState();
                return;
            }

            string mapName = string.IsNullOrWhiteSpace(header.display_filename)
                ? System.IO.Path.GetFileNameWithoutExtension(safePath)
                : header.display_filename;
            HUD_LoadSaveRequester requester = activeRequester;
            try
            {
                HUD_ConfirmationPopup.ShowConfirmationMessage(
                    SerpLocalization.Get("BugfixesAndQoL.DeleteMapConfirmTitle"),
                    () => DeleteMapConfirmed(requester, safePath, mapName),
                    RefreshUiState,
                    SerpLocalization.Get(
                        "BugfixesAndQoL.DeleteMapConfirmMessage",
                        "MapName",
                        mapName));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not open the delete confirmation for [{safePath}]: {ex}");
                ShowDeleteError(mapName);
            }
        }

        private void DeleteMapConfirmed(
            HUD_LoadSaveRequester requester,
            string requestedPath,
            string mapName)
        {
            try
            {
                if (!VanillaMapEditorPolicy.TryResolveDeletableUserMapPath(
                        requestedPath,
                        ConfigSettings.GetUserMapsPath(),
                        File.Exists,
                        out string safePath))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL rejected a map deletion after confirmation because the path was no longer safe: [{requestedPath}].");
                    ShowDeleteError(mapName);
                    return;
                }

                File.Delete(safePath);
                if (File.Exists(safePath))
                    throw new IOException("The map file still exists after File.Delete returned.");

                ClearDeletedSelection(requester, mapName);
                RefreshRequesterList(requester);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL permanently deleted user map [{safePath}].");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not delete user map [{requestedPath}]: {ex}");
                ShowDeleteError(mapName);
            }
            finally
            {
                RefreshUiState();
            }
        }

        private bool TryGetSelectedDeletableMap(out FileHeader header, out string safePath)
        {
            header = null;
            safePath = null;
            try
            {
                Enums.RequesterTypes requesterType = GetActiveRequesterType();
                if (!FeatureEnabled || !IsEditorMapRequester(requesterType) || activeRequester == null)
                    return false;

                ListView list = (ListView)FileListField.GetValue(activeRequester);
                header = (list?.SelectedItem as FileRow)?.fileHeader;
                return header != null &&
                    !header.builtinMap &&
                    VanillaMapEditorPolicy.TryResolveDeletableUserMapPath(
                        header.filePath,
                        ConfigSettings.GetUserMapsPath(),
                        File.Exists,
                        out safePath);
            }
            catch (Exception ex)
            {
                LogUiFailure("validate the selected map for deletion", ex);
                header = null;
                safePath = null;
                return false;
            }
        }

        private void ClearDeletedSelection(HUD_LoadSaveRequester requester, string mapName)
        {
            if (requester == null)
                return;

            ListView list = (ListView)FileListField.GetValue(requester);
            if (list != null)
                list.SelectedItem = null;
            SelectedHeaderField.SetValue(requester, null);

            Button actionButton = (Button)ActionButtonField.GetValue(requester);
            if (actionButton != null)
                actionButton.IsEnabled = false;

            MainViewModel viewModel = MainViewModel.Instance;
            if (string.Equals(viewModel.LoadSaveFileName, mapName, StringComparison.OrdinalIgnoreCase))
                viewModel.LoadSaveFileName = string.Empty;
            viewModel.RadarRequesterImage = null;
            viewModel.Show_Radar160Border = false;
            viewModel.Show_Radar300Border = false;
            viewModel.Show_Radar500Border = false;
            viewModel.Show_Radar700Border = false;
        }

        private void RefreshRequesterList(HUD_LoadSaveRequester requester)
        {
            if (requester == null)
                return;

            try
            {
                PopulateListMethod.Invoke(requester, null);
            }
            catch (Exception ex)
            {
                LogUiFailure("refresh the editor map list", ex);
            }
        }

        private void RefreshUiState()
        {
            OnPropertyChanged(nameof(ShowVanillaMapsVisibility));
            OnPropertyChanged(nameof(DeleteMapVisibility));
            OnPropertyChanged(nameof(DeleteMapEnabled));
            DeleteMapCommand.RaiseCanExecuteChanged();
        }

        private void ShowDeleteError(string mapName)
        {
            try
            {
                string displayName = string.IsNullOrWhiteSpace(mapName)
                    ? SerpLocalization.Get("BugfixesAndQoL.DeleteMapUnknownName")
                    : mapName;
                HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                    SerpLocalization.Get("BugfixesAndQoL.DeleteMapErrorTitle"),
                    RefreshUiState,
                    SerpLocalization.Get(
                        "BugfixesAndQoL.DeleteMapErrorMessage",
                        "MapName",
                        displayName));
            }
            catch (Exception ex)
            {
                LogUiFailure("show the map-deletion error", ex);
            }
        }

        private void LogUiFailure(string action, Exception ex)
        {
            if (uiFailureLogged)
                return;

            uiFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL could not {action}; Vanilla map-menu behavior remains available: {ex}");
        }

        private Enums.RequesterTypes GetActiveRequesterType() =>
            activeRequester == null ? (Enums.RequesterTypes)(-1) : GetRequesterType(activeRequester);

        private static bool IsEditorMapRequester(Enums.RequesterTypes requesterType) =>
            requesterType == Enums.RequesterTypes.LoadEditorMap ||
            requesterType == Enums.RequesterTypes.SaveEditorMap;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static Enums.RequesterTypes GetRequesterType(HUD_LoadSaveRequester requester) =>
            (Enums.RequesterTypes)RequesterTypeField.GetValue(requester);

        private static List<FileHeader> SortWithVanilla(
            MapFileManager manager,
            List<FileHeader> headers,
            int sortMode,
            bool sortAscend) =>
            (List<FileHeader>)SortListMethod.Invoke(
                manager,
                new object[] { headers, sortMode, sortAscend });

        private static FieldInfo FindRequiredField(Type type, string name) =>
            type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new MissingFieldException(type.FullName, name);

        private static MethodInfo FindRequiredMethod(
            Type type,
            string name,
            params Type[] parameterTypes) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null) ??
            throw new MissingMethodException(type.FullName, name);

        private static void DisposeHook(ref Hook hook)
        {
            Hook current = hook;
            hook = null;
            if (current == null)
                return;

            current.Undo();
            current.Dispose();
        }
    }
}
