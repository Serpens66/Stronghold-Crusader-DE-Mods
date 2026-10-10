// Reuse existing property validators and pure option parsers, never native setters.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.Toml;
using CrusaderDETweaker.Config.Toml.Units;
using CrusaderDETweaker.Config.Toml.Structures;
using Tomlyn.Model;

namespace CrusaderDETweaker.Configuration
{
    internal static class ConfigurationPropertyValidation
    {
        internal static void Bind(ConfigurationDocuments documents)
        {
            foreach (var handler in UnitPropertyRegistry.Instance.GetAll())
                documents.BindValidator(ConfigPaths.UnitsFileName, null, handler.Name, handler.ValueType, handler.ValidateWithoutApplying);
            foreach (var handler in StructurePropertyRegistry.Instance.GetAll())
                documents.BindValidator(ConfigPaths.StructuresFileName, null, handler.Name, handler.ValueType, handler.ValidateWithoutApplying);

            foreach (var slot in TeamColors.Slots)
                documents.BindTextValue(ConfigPaths.GlobalsFileName, TeamColors.Section, slot.Key, ReadColor, WriteColor);

            foreach (var option in documents.Options.Where(x => x.File == ConfigPaths.GlobalsFileName))
            {
                if (option.Group == ApothecaryHealing.Section)
                {
                    string name = option.Name;
                    Type type = name == "HealPercent" ? typeof(double) : option.DefaultValue.GetType();
                    documents.BindValidator(option.File, option.Group, name, type, value =>
                    {
                        var errors = new List<string>();
                        ApothecaryHealing.Parse(new TomlTable { [name] = value }, errors.Add);
                        return errors.Count == 0;
                    });
                }
                else if (option.Group == UnitLimit.Section && option.Name == UnitLimit.Key)
                {
                    documents.BindValidator(option.File, option.Group, option.Name, typeof(long), value =>
                    {
                        long limit = (long)value;
                        return limit < 0 || (limit >= UnitLimit.Minimum && limit <= UnitLimit.Maximum);
                    });
                }
                else if (option.Group.StartsWith(SkirmishStartingTroops.Section + " / ", StringComparison.Ordinal))
                {
                    string level = option.Group.Substring(SkirmishStartingTroops.Section.Length + 3);
                    string name = option.Name;
                    documents.BindExactValidator(option.Key, value =>
                    {
                        var errors = new List<string>();
                        var model = new TomlTable
                        {
                            [SkirmishStartingTroops.Section] = new TomlTable
                            {
                                [level] = new TomlTable { [name] = value }
                            }
                        };
                        SkirmishStartingTroops.Parse(model, errors);
                        return errors.Count == 0;
                    });
                }
            }
        }

        // Scalar API representation: "-1" (game colour) or canonical "#RRGGBB".
        // Both arrays and strings from existing files pass through the original parser.
        private static string ReadColor(object raw)
        {
            if (!TeamColors.TryParse(raw, out var color, out string problem) || problem != null)
                throw new InvalidDataException(problem ?? "Invalid team colour.");
            return color.HasValue ? "#" + color.Value.R.ToString("X2", CultureInfo.InvariantCulture)
                + color.Value.G.ToString("X2", CultureInfo.InvariantCulture)
                + color.Value.B.ToString("X2", CultureInfo.InvariantCulture) : "-1";
        }

        private static object WriteColor(string text)
        {
            object raw = text == "-1" ? (object)(-1L) : text;
            if (ReadColor(raw) != text) throw new InvalidDataException("Use -1 or a canonical #RRGGBB colour.");
            return raw;
        }
    }
}
