using BepInEx.Bootstrap;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CastlePlanner
{
    internal sealed class FixesKeepRotationCompatibility
    {
        private const string FixesPluginGuid = "fixes";
        private const BindingFlags PublicInstance =
            BindingFlags.Instance | BindingFlags.Public;

        private readonly ManualLogSource log;
        private Array overriddenData;
        private readonly Dictionary<int, object> originalValues =
            new Dictionary<int, object>();

        public FixesKeepRotationCompatibility(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Validate(IReadOnlyCollection<FreeCastleSelection> selections)
        {
            if (selections == null ||
                !HasActiveSelection(selections))
                return string.Empty;
            try
            {
                ResolveActiveRotationData(out _);
                return string.Empty;
            }
            catch (Exception ex)
            {
                return "Fixes keep-rotation compatibility is unavailable: " +
                    ex.GetBaseException().Message;
            }
        }

        public void Apply(IReadOnlyCollection<FreeCastleSelection> selections)
        {
            Restore("replace");
            if (selections == null || !HasActiveSelection(selections) ||
                !ResolveActiveRotationData(out Array data))
            {
                return;
            }

            Type elementType = data.GetType().GetElementType();
            try
            {
                foreach (FreeCastleSelection selection in selections)
                {
                    FreeCastleProtocol.ValidateSelection(selection);
                    if (selection.Mode == FreeCastleSelectionMode.Nothing)
                        continue;
                    if (selection.PlayerId >= data.Length)
                    {
                        throw new InvalidOperationException(
                            $"Fixes PlayerKeepRotationData has no slot {selection.PlayerId}.");
                    }

                    originalValues.Add(
                        selection.PlayerId,
                        data.GetValue(selection.PlayerId));
                    data.SetValue(
                        Enum.ToObject(elementType, selection.Rotation),
                        selection.PlayerId);
                }
                overriddenData = data;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Temporarily aligned Fixes keep rotations for CastlePlanner players " +
                    $"[{string.Join(",", originalValues.Keys)}].");
            }
            catch
            {
                overriddenData = data;
                Restore("failed-apply");
                throw;
            }
        }

        private static bool HasActiveSelection(
            IEnumerable<FreeCastleSelection> selections)
        {
            foreach (FreeCastleSelection selection in selections)
            {
                if (selection != null &&
                    selection.Mode != FreeCastleSelectionMode.Nothing)
                {
                    return true;
                }
            }
            return false;
        }

        public void Restore(string reason)
        {
            if (overriddenData == null)
                return;
            try
            {
                foreach (KeyValuePair<int, object> pair in originalValues)
                    overriddenData.SetValue(pair.Value, pair.Key);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Restored Fixes keep rotations after CastlePlanner spawn ({reason}).");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Fixes keep rotations could not be restored ({reason}): {ex}");
            }
            finally
            {
                originalValues.Clear();
                overriddenData = null;
            }
        }

        private static bool ResolveActiveRotationData(out Array data)
        {
            data = null;
            if (!Chainloader.PluginInfos.TryGetValue(
                    FixesPluginGuid,
                    out BepInEx.PluginInfo pluginInfo) ||
                pluginInfo == null ||
                ReferenceEquals(pluginInfo.Instance, null))
            {
                return false;
            }

            object plugin = pluginInfo.Instance;
            Type pluginType = plugin.GetType();
            FieldInfo optionField = pluginType.GetField(
                "AllowCustomPlayerKeepRotations",
                PublicInstance);
            object viewModel = pluginType
                .GetProperty("LobbySettingsViewModel", PublicInstance)?
                .GetValue(plugin, null);
            PropertyInfo dataProperty = viewModel?.GetType().GetProperty(
                "PlayerKeepRotationData",
                PublicInstance);
            if (optionField == null)
            {
                if (dataProperty == null)
                    return false;
                throw new InvalidOperationException(
                    "Fixes exposes an incomplete keep-rotation interface.");
            }

            object option = optionField.GetValue(plugin) ??
                throw new InvalidOperationException(
                    "Fixes keep-rotation option is unavailable.");
            object enabledValue = option.GetType()
                .GetProperty("Value", PublicInstance)?
                .GetValue(option, null);
            if (!(enabledValue is bool enabled))
            {
                throw new InvalidOperationException(
                    "Fixes keep-rotation option has no Boolean Value.");
            }
            if (!enabled)
                return false;

            if (dataProperty == null || viewModel == null)
            {
                throw new InvalidOperationException(
                    "Fixes exposes an incomplete keep-rotation interface.");
            }

            data = dataProperty.GetValue(viewModel, null) as Array;
            Type elementType = data?.GetType().GetElementType();
            if (data == null || data.Length < 9 ||
                elementType == null || !elementType.IsEnum)
            {
                throw new InvalidOperationException(
                    "Fixes PlayerKeepRotationData is incompatible.");
            }
            return true;
        }
    }
}
