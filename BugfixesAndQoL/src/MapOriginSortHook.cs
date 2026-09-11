// Feature: Repair Vanilla's inactive map-origin headers in standalone and multiplayer map lists.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class MapOriginSortHook : IDisposable
    {
        private delegate void StandaloneHeaderDelegate(
            FRONT_StandaloneMission self,
            object sender,
            RoutedEventArgs args);

        private delegate void MultiplayerHeaderDelegate(
            FRONT_Multiplayer self,
            object sender,
            RoutedEventArgs args);

        private delegate void StandalonePopulateDelegate(FRONT_StandaloneMission self);

        private delegate void MultiplayerPopulateDelegate(
            FRONT_Multiplayer self,
            FileHeader selectedHeader,
            bool ignoreRefresh);

        private delegate void MultiplayerShowSetupDelegate(FRONT_Multiplayer self);

        private delegate FileHeader GetFileInfoDelegate(
            MapFileManager self,
            string filePath,
            string realFilePath,
            int folderType,
            bool loadRestartInfo);

        private static readonly FieldInfo StandaloneSortColumnField = FindRequiredField(
            typeof(FRONT_StandaloneMission),
            "sortByColumn");

        private static readonly FieldInfo StandaloneSortAscendingField = FindRequiredField(
            typeof(FRONT_StandaloneMission),
            "sortByAscending");

        private static readonly FieldInfo MultiplayerSortColumnField = FindRequiredField(
            typeof(FRONT_Multiplayer),
            "sortByColumn");

        private static readonly FieldInfo MultiplayerSortAscendingField = FindRequiredField(
            typeof(FRONT_Multiplayer),
            "sortByAscending");

        private static readonly FieldInfo MultiplayerSelectedHeaderField = FindRequiredField(
            typeof(FRONT_Multiplayer),
            "selectedMPHeader");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Hook standaloneHeaderHook;
        private readonly Hook standalonePopulateHook;
        private readonly Hook multiplayerHeaderHook;
        private readonly Hook multiplayerPopulateHook;
        private readonly Hook multiplayerShowSetupHook;
        private readonly Hook getFileInfoHook;
        private readonly StandaloneHeaderDelegate standaloneHeaderTrampoline;
        private readonly StandalonePopulateDelegate standalonePopulateTrampoline;
        private readonly MultiplayerHeaderDelegate multiplayerHeaderTrampoline;
        private readonly MultiplayerPopulateDelegate multiplayerPopulateTrampoline;
        private readonly MultiplayerShowSetupDelegate multiplayerShowSetupTrampoline;
        private readonly GetFileInfoDelegate getFileInfoTrampoline;
        private readonly LobbyMapSelectionStore lobbyMapSelectionStore;
        private FRONT_Multiplayer trackedMultiplayerView;
        private ListView trackedMultiplayerMapList;
        private bool setupRestorePending;
        private bool restoringSetup;
        private bool sortingFailureLogged;
        private bool disposed;

        internal MapOriginSortHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Hook newStandaloneHeaderHook = null;
            Hook newStandalonePopulateHook = null;
            Hook newMultiplayerHeaderHook = null;
            Hook newMultiplayerPopulateHook = null;
            Hook newMultiplayerShowSetupHook = null;
            Hook newGetFileInfoHook = null;
            try
            {
                MethodInfo standaloneHeader = FindRequiredMethod(
                    typeof(FRONT_StandaloneMission),
                    "FileListHeaderClickedHandler",
                    typeof(object),
                    typeof(RoutedEventArgs));
                MethodInfo standalonePopulate = FindRequiredMethod(
                    typeof(FRONT_StandaloneMission),
                    "populateList");
                MethodInfo multiplayerHeader = FindRequiredMethod(
                    typeof(FRONT_Multiplayer),
                    "FileListHeaderClickedHandler",
                    typeof(object),
                    typeof(RoutedEventArgs));
                MethodInfo multiplayerPopulate = FindRequiredMethod(
                    typeof(FRONT_Multiplayer),
                    "populateMapList",
                    typeof(FileHeader),
                    typeof(bool));
                MethodInfo multiplayerShowSetup = FindRequiredMethod(
                    typeof(FRONT_Multiplayer),
                    "ShowSetupScreen");

                newStandaloneHeaderHook = new Hook(
                    standaloneHeader,
                    (StandaloneHeaderDelegate)StandaloneHeaderHook);
                standaloneHeaderTrampoline =
                    newStandaloneHeaderHook.GenerateTrampoline<StandaloneHeaderDelegate>();

                newStandalonePopulateHook = new Hook(
                    standalonePopulate,
                    (StandalonePopulateDelegate)StandalonePopulateHook);
                standalonePopulateTrampoline =
                    newStandalonePopulateHook.GenerateTrampoline<StandalonePopulateDelegate>();

                newMultiplayerHeaderHook = new Hook(
                    multiplayerHeader,
                    (MultiplayerHeaderDelegate)MultiplayerHeaderHook);
                multiplayerHeaderTrampoline =
                    newMultiplayerHeaderHook.GenerateTrampoline<MultiplayerHeaderDelegate>();

                newMultiplayerPopulateHook = new Hook(
                    multiplayerPopulate,
                    (MultiplayerPopulateDelegate)MultiplayerPopulateHook);
                multiplayerPopulateTrampoline =
                    newMultiplayerPopulateHook.GenerateTrampoline<MultiplayerPopulateDelegate>();

                newMultiplayerShowSetupHook = new Hook(
                    multiplayerShowSetup,
                    (MultiplayerShowSetupDelegate)MultiplayerShowSetupHook);
                multiplayerShowSetupTrampoline =
                    newMultiplayerShowSetupHook.GenerateTrampoline<MultiplayerShowSetupDelegate>();

                MethodInfo getFileInfo = FindGetFileInfoMethod();
                newGetFileInfoHook = new Hook(
                    getFileInfo,
                    (GetFileInfoDelegate)GetFileInfoHook);
                getFileInfoTrampoline =
                    newGetFileInfoHook.GenerateTrampoline<GetFileInfoDelegate>();

                standaloneHeaderHook = newStandaloneHeaderHook;
                standalonePopulateHook = newStandalonePopulateHook;
                multiplayerHeaderHook = newMultiplayerHeaderHook;
                multiplayerPopulateHook = newMultiplayerPopulateHook;
                multiplayerShowSetupHook = newMultiplayerShowSetupHook;
                getFileInfoHook = newGetFileInfoHook;
                lobbyMapSelectionStore = new LobbyMapSelectionStore(log);
            }
            catch
            {
                DisposeHook(newGetFileInfoHook);
                DisposeHook(newMultiplayerShowSetupHook);
                DisposeHook(newMultiplayerPopulateHook);
                DisposeHook(newMultiplayerHeaderHook);
                DisposeHook(newStandalonePopulateHook);
                DisposeHook(newStandaloneHeaderHook);
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL map-origin sorting hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            DisposeHook(getFileInfoHook);
            DisposeHook(multiplayerShowSetupHook);
            DisposeHook(multiplayerPopulateHook);
            DisposeHook(multiplayerHeaderHook);
            DisposeHook(standalonePopulateHook);
            DisposeHook(standaloneHeaderHook);
            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL map-origin sorting hooks disposed.");
        }

        private bool IsActive => LobbyMapSelectionPolicy.IsFeatureEnabled(
            settings.EnableMod,
            settings.EnableCustomLordListEnhancements);

        private void StandaloneHeaderHook(
            FRONT_StandaloneMission self,
            object sender,
            RoutedEventArgs args)
        {
            if (IsActive && IsOriginHeader(args))
            {
                TrySelectOriginSort(
                    self,
                    StandaloneSortColumnField,
                    StandaloneSortAscendingField);
            }

            standaloneHeaderTrampoline(self, sender, args);
        }

        private void MultiplayerHeaderHook(
            FRONT_Multiplayer self,
            object sender,
            RoutedEventArgs args)
        {
            if (IsActive && IsOriginHeader(args))
            {
                TrySelectOriginSort(
                    self,
                    MultiplayerSortColumnField,
                    MultiplayerSortAscendingField);
            }

            multiplayerHeaderTrampoline(self, sender, args);
            TryRememberMultiplayerSort(self);
        }

        private void StandalonePopulateHook(FRONT_StandaloneMission self)
        {
            standalonePopulateTrampoline(self);
            TryApplyOriginSort(
                self,
                StandaloneSortColumnField,
                StandaloneSortAscendingField);
        }

        private void MultiplayerPopulateHook(
            FRONT_Multiplayer self,
            FileHeader selectedHeader,
            bool ignoreRefresh)
        {
            bool eligible = IsActive && IsEligibleMapMode(self);
            if (!eligible)
                setupRestorePending = false;

            bool restoreThisPopulation = eligible && setupRestorePending && !restoringSetup;
            if (restoreThisPopulation)
                TryApplyRememberedSort(self);

            multiplayerPopulateTrampoline(self, selectedHeader, ignoreRefresh);
            TryApplyOriginSort(
                self,
                MultiplayerSortColumnField,
                MultiplayerSortAscendingField);
            TryAttachMultiplayerSelectionHandler(self);

            if (restoreThisPopulation)
            {
                setupRestorePending = false;
                if (selectedHeader == null && CanRememberMap(self))
                    TrySelectRememberedMap(self);
            }

            TryRememberSelectedMap(self);
        }

        private void MultiplayerShowSetupHook(FRONT_Multiplayer self)
        {
            multiplayerShowSetupTrampoline(self);
            if (!IsActive || !IsEligibleMapMode(self))
            {
                setupRestorePending = false;
                return;
            }

            setupRestorePending = true;
            if (self.panelActive)
                TryRestoreEstablishedSetup(self);
        }

        private FileHeader GetFileInfoHook(
            MapFileManager self,
            string filePath,
            string realFilePath,
            int folderType,
            bool loadRestartInfo)
        {
            FileHeader result = getFileInfoTrampoline(
                self, filePath, realFilePath, folderType, loadRestartInfo);
            string path = string.IsNullOrWhiteSpace(realFilePath) ? filePath : realFilePath;
            if (!ClassicMapSizeReader.ShouldPopulate(
                    IsActive,
                    result?.classicSave == true,
                    result?.world_size ?? default,
                    path) ||
                !ClassicMapSizeReader.TryRead(path, out int worldSize))
            {
                return result;
            }

            result.world_size = worldSize;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Recovered classic map size {worldSize} from section 1050: {path}");
            return result;
        }

        private static bool IsOriginHeader(RoutedEventArgs args)
        {
            GridViewColumnHeader header = args?.Source as GridViewColumnHeader;
            return string.Equals(header?.Tag as string, "Type", StringComparison.Ordinal);
        }

        private void TrySelectOriginSort(
            object view,
            FieldInfo sortColumnField,
            FieldInfo sortAscendingField)
        {
            try
            {
                int currentColumn = (int)sortColumnField.GetValue(view);
                bool ascending = currentColumn == 4 &&
                    !(bool)sortAscendingField.GetValue(view);
                sortColumnField.SetValue(view, 4);
                sortAscendingField.SetValue(view, ascending);
            }
            catch (Exception exception)
            {
                LogSortingFailure(exception);
            }
        }

        private void TryApplyOriginSort(
            FrameworkElement view,
            FieldInfo sortColumnField,
            FieldInfo sortAscendingField)
        {
            try
            {
                ApplyOriginSort(view, sortColumnField, sortAscendingField);
            }
            catch (Exception exception)
            {
                // Vanilla already produced a usable list before this post-processing step.
                LogSortingFailure(exception);
            }
        }

        private void ApplyOriginSort(
            FrameworkElement view,
            FieldInfo sortColumnField,
            FieldInfo sortAscendingField)
        {
            if (!IsActive || (int)sortColumnField.GetValue(view) != 4)
                return;

            ListView mapList = view.FindName("MapList") as ListView;
            ObservableCollection<FileRow> rows =
                mapList?.ItemsSource as ObservableCollection<FileRow>;
            if (rows == null || rows.Count < 2)
                return;

            bool ascending = (bool)sortAscendingField.GetValue(view);
            List<IndexedRow> orderedRows = new List<IndexedRow>(rows.Count);
            for (int index = 0; index < rows.Count; index++)
                orderedRows.Add(new IndexedRow(rows[index], index));

            orderedRows.Sort((left, right) => CompareRows(left, right, ascending));
            for (int targetIndex = 0; targetIndex < orderedRows.Count; targetIndex++)
            {
                FileRow expected = orderedRows[targetIndex].Row;
                int currentIndex = rows.IndexOf(expected);
                if (currentIndex >= 0 && currentIndex != targetIndex)
                    rows.Move(currentIndex, targetIndex);
            }
        }

        private void LogSortingFailure(Exception exception)
        {
            if (sortingFailureLogged)
                return;

            sortingFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL map-origin sorting failed; the Vanilla map list remains usable: {exception}");
        }

        private void TryRestoreEstablishedSetup(FRONT_Multiplayer self)
        {
            if (restoringSetup)
                return;

            try
            {
                restoringSetup = true;
                TryApplyRememberedSort(self);
                ListView mapList = FindMapList(self);
                FileHeader selectedHeader =
                    MultiplayerSelectedHeaderField.GetValue(self) as FileHeader;
                if (CanRememberMap(self))
                    selectedHeader = FindRememberedHeader(mapList) ?? selectedHeader;

                // Keep Vanilla's selection event active so hosts synchronize the restored map.
                multiplayerPopulateTrampoline(self, selectedHeader, false);
                TryApplyOriginSort(
                    self,
                    MultiplayerSortColumnField,
                    MultiplayerSortAscendingField);
                TryAttachMultiplayerSelectionHandler(self);
                setupRestorePending = false;
                TryRememberSelectedMap(self);
            }
            catch (Exception exception)
            {
                setupRestorePending = false;
                LogMemoryFailure("restore the lobby map selection", exception);
            }
            finally
            {
                restoringSetup = false;
            }
        }

        private void TryApplyRememberedSort(FRONT_Multiplayer self)
        {
            try
            {
                LobbyMapSelectionSnapshot snapshot = lobbyMapSelectionStore.Current;
                if (!LobbyMapSelectionPolicy.IsValidSortColumn(snapshot.SortColumn))
                    return;

                MultiplayerSortColumnField.SetValue(self, snapshot.SortColumn);
                MultiplayerSortAscendingField.SetValue(self, snapshot.SortAscending);
            }
            catch (Exception exception)
            {
                LogMemoryFailure("restore the lobby map sort order", exception);
            }
        }

        private void TryRememberMultiplayerSort(FRONT_Multiplayer self)
        {
            if (!IsActive || !IsEligibleMapMode(self))
                return;

            try
            {
                lobbyMapSelectionStore.RememberSort(
                    (int)MultiplayerSortColumnField.GetValue(self),
                    (bool)MultiplayerSortAscendingField.GetValue(self));
            }
            catch (Exception exception)
            {
                LogMemoryFailure("remember the lobby map sort order", exception);
            }
        }

        private void TryAttachMultiplayerSelectionHandler(FRONT_Multiplayer self)
        {
            try
            {
                ListView mapList = FindMapList(self);
                if (mapList == null ||
                    ReferenceEquals(mapList, trackedMultiplayerMapList) &&
                    ReferenceEquals(self, trackedMultiplayerView))
                {
                    return;
                }

                trackedMultiplayerView = self;
                trackedMultiplayerMapList = mapList;
                mapList.SelectionChanged += MultiplayerMapSelectionChanged;
            }
            catch (Exception exception)
            {
                LogMemoryFailure("attach lobby map-selection memory", exception);
            }
        }

        private void MultiplayerMapSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            TryRememberSelectedMap(trackedMultiplayerView);
        }

        private void TryRememberSelectedMap(FRONT_Multiplayer self)
        {
            if (!IsActive || !IsEligibleMapMode(self) || !CanRememberMap(self))
                return;

            try
            {
                ListView mapList = FindMapList(self);
                FileHeader header = (mapList?.SelectedItem as FileRow)?.fileHeader;
                LobbyMapIdentity identity = CreateIdentity(header);
                if (identity != null)
                    lobbyMapSelectionStore.RememberMap(identity);
            }
            catch (Exception exception)
            {
                LogMemoryFailure("remember the selected lobby map", exception);
            }
        }

        private void TrySelectRememberedMap(FRONT_Multiplayer self)
        {
            try
            {
                ListView mapList = FindMapList(self);
                FileHeader remembered = FindRememberedHeader(mapList);
                if (remembered == null)
                    return;

                ObservableCollection<FileRow> rows =
                    mapList.ItemsSource as ObservableCollection<FileRow>;
                if (rows == null)
                    return;

                foreach (FileRow row in rows)
                {
                    if (ReferenceEquals(row?.fileHeader, remembered))
                    {
                        mapList.SelectedItem = row;
                        mapList.ScrollIntoView(row);
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                LogMemoryFailure("select the remembered lobby map", exception);
            }
        }

        private FileHeader FindRememberedHeader(ListView mapList)
        {
            LobbyMapIdentity remembered = lobbyMapSelectionStore.Current.Map;
            ObservableCollection<FileRow> rows =
                mapList?.ItemsSource as ObservableCollection<FileRow>;
            if (remembered == null || rows == null)
                return null;

            var candidates = new List<LobbyMapCandidate>(rows.Count);
            foreach (FileRow row in rows)
            {
                FileHeader header = row?.fileHeader;
                if (header == null)
                    continue;

                candidates.Add(new LobbyMapCandidate(
                    MapOriginSortHook.GetOrigin(header),
                    header.filePath,
                    header.fileName,
                    header));
            }

            return LobbyMapSelectionPolicy.FindMatch(remembered, candidates) as FileHeader;
        }

        private static LobbyMapIdentity CreateIdentity(FileHeader header) =>
            header == null
                ? null
                : LobbyMapSelectionPolicy.CreateIdentity(
                    header.builtinMap,
                    header.userMap,
                    header.workshopMap,
                    header.filePath,
                    header.fileName);

        private static string GetOrigin(FileHeader header) =>
            header == null
                ? string.Empty
                : LobbyMapSelectionPolicy.GetOrigin(
                    header.builtinMap,
                    header.userMap,
                    header.workshopMap);

        private static bool CanRememberMap(FRONT_Multiplayer self) =>
            self != null &&
            LobbyMapSelectionPolicy.HasMapAuthority(
                FRONT_Multiplayer.skirmishGame,
                self.currentLobby != null,
                self.currentLobby?.isHost == true);

        private static bool IsMapListVisible() =>
            MainViewModel.Instance?.Show_MPGameCreation == true;

        private static bool IsEligibleMapMode(FRONT_Multiplayer self) =>
            self != null &&
            !FRONT_Multiplayer.coopGame &&
            !FRONT_Multiplayer.customCoopGame &&
            IsMapListVisible();

        private static ListView FindMapList(FrameworkElement view) =>
            view?.FindName("MapList") as ListView;

        private void LogMemoryFailure(string action, Exception exception)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL could not {action}; Vanilla behavior remains active: {exception}");
        }

        private static int CompareRows(IndexedRow left, IndexedRow right, bool ascending)
        {
            int comparison = MapOriginSortPolicy.Compare(
                CreateKey(left.Row?.fileHeader),
                CreateKey(right.Row?.fileHeader),
                ascending);
            return comparison != 0
                ? comparison
                : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static MapOriginSortKey CreateKey(FileHeader header)
        {
            return header == null
                ? new MapOriginSortKey(false, false, false, string.Empty)
                : new MapOriginSortKey(
                    header.builtinMap,
                    header.userMap,
                    header.workshopMap,
                    header.display_filename);
        }

        private static FieldInfo FindRequiredField(Type type, string name)
        {
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(type.FullName, name);
            return field;
        }

        private static MethodInfo FindRequiredMethod(
            Type type,
            string name,
            params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static MethodInfo FindGetFileInfoMethod()
        {
            // Unlike the frontend population methods, this map-file API is public.
            MethodInfo method = typeof(MapFileManager).GetMethod(
                nameof(MapFileManager.GetFileInfoFromFileName),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(int), typeof(bool) },
                null);
            if (method == null)
            {
                throw new MissingMethodException(
                    typeof(MapFileManager).FullName,
                    nameof(MapFileManager.GetFileInfoFromFileName));
            }

            return method;
        }

        private static void DisposeHook(Hook hook)
        {
            if (hook == null)
                return;

            hook.Undo();
            hook.Dispose();
        }

        private readonly struct IndexedRow
        {
            internal IndexedRow(FileRow row, int originalIndex)
            {
                Row = row;
                OriginalIndex = originalIndex;
            }

            internal FileRow Row { get; }
            internal int OriginalIndex { get; }
        }
    }
}
