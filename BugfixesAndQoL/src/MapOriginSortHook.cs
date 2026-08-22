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

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Hook standaloneHeaderHook;
        private readonly Hook standalonePopulateHook;
        private readonly Hook multiplayerHeaderHook;
        private readonly Hook multiplayerPopulateHook;
        private readonly StandaloneHeaderDelegate standaloneHeaderTrampoline;
        private readonly StandalonePopulateDelegate standalonePopulateTrampoline;
        private readonly MultiplayerHeaderDelegate multiplayerHeaderTrampoline;
        private readonly MultiplayerPopulateDelegate multiplayerPopulateTrampoline;
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

                standaloneHeaderHook = newStandaloneHeaderHook;
                standalonePopulateHook = newStandalonePopulateHook;
                multiplayerHeaderHook = newMultiplayerHeaderHook;
                multiplayerPopulateHook = newMultiplayerPopulateHook;
            }
            catch
            {
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
            DisposeHook(multiplayerPopulateHook);
            DisposeHook(multiplayerHeaderHook);
            DisposeHook(standalonePopulateHook);
            DisposeHook(standaloneHeaderHook);
            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL map-origin sorting hooks disposed.");
        }

        private bool IsActive => settings.EnableMod && settings.EnableCustomLordListEnhancements;

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
            multiplayerPopulateTrampoline(self, selectedHeader, ignoreRefresh);
            TryApplyOriginSort(
                self,
                MultiplayerSortColumnField,
                MultiplayerSortAscendingField);
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
