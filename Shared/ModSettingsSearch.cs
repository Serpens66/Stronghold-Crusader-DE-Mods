#if !SHARED_PRESET_TESTS
using Noesis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Shared
{
    /// <summary>
    /// Explicit, localized metadata for one logical mod-setting entry. The properties are
    /// attached to the element that should be scrolled into view for a search result.
    /// </summary>
    public static class ModSettingsSearch
    {
        private static readonly Dictionary<object, SearchSource> Sources =
            new Dictionary<object, SearchSource>(ReferenceEqualityComparer.Instance);

        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.RegisterAttached(
                "Key",
                typeof(string),
                typeof(ModSettingsSearch),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.RegisterAttached(
                "Title",
                typeof(string),
                typeof(ModSettingsSearch),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ExcludeProperty =
            DependencyProperty.RegisterAttached(
                "Exclude",
                typeof(bool),
                typeof(ModSettingsSearch),
                new PropertyMetadata(false));

        public static string GetKey(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(KeyProperty) as string ?? string.Empty;

        public static void SetKey(DependencyObject value, string key) =>
            value?.SetValue(KeyProperty, key ?? string.Empty);

        public static string GetTitle(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(TitleProperty) as string ?? string.Empty;

        public static void SetTitle(DependencyObject value, string title) =>
            value?.SetValue(TitleProperty, title ?? string.Empty);

        public static bool GetExclude(DependencyObject value) =>
            value != null && value.GetValue(ExcludeProperty) is bool excluded && excluded;

        public static void SetExclude(DependencyObject value, bool excluded) =>
            value?.SetValue(ExcludeProperty, excluded);

        /// <summary>
        /// Called reflectively by SerpsModsHost. Returning framework-owned objects keeps every
        /// individual mod standalone and avoids a hard reference to the optional pack host.
        /// </summary>
        public static void RegisterSource(
            object viewModel,
            string absoluteXamlPath,
            BepInEx.Logging.ManualLogSource log,
            string modName)
        {
            if (viewModel == null || string.IsNullOrWhiteSpace(absoluteXamlPath))
                return;
            Sources[viewModel] = new SearchSource(absoluteXamlPath, log, modName);
        }

        public static IReadOnlyList<ModSettingsSearchEntry> Export(
            object viewModel,
            FrameworkElement view)
        {
            // Registered Serps mods use the immutable XAML catalog exclusively. Traversing a
            // realized Noesis tree during or after a tab transition can crash in native code.
            if (viewModel != null && Sources.TryGetValue(viewModel, out SearchSource source))
                return source.GetEntries(viewModel);

            var realizedEntries = new List<ModSettingsSearchEntry>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var visited = new HashSet<DependencyObject>();
            var structuralParents = new Dictionary<DependencyObject, DependencyObject>();
            Visit(view, realizedEntries, keys, identities, visited, structuralParents, null, 0);
            return realizedEntries;
        }

        private static void Visit(
            DependencyObject current,
            List<ModSettingsSearchEntry> entries,
            HashSet<string> keys,
            HashSet<string> identities,
            HashSet<DependencyObject> visited,
            Dictionary<DependencyObject, DependencyObject> structuralParents,
            DependencyObject structuralParent,
            int depth)
        {
            if (current == null || depth > 80 || !visited.Add(current))
                return;

            if (structuralParent != null && !structuralParents.ContainsKey(current))
                structuralParents.Add(current, structuralParent);

            if (current is FrameworkElement excludedElement && GetExclude(excludedElement))
                return;

            if (current is FrameworkElement element)
            {
                string key = GetKey(element).Trim();
                string title = GetTitle(element).Trim();
                if (key.Length > 0 || title.Length > 0)
                {
                    // Explicit metadata is authoritative. Repeated keys deliberately allow a
                    // logical setting with several controls to select its first navigation target.
                    if (key.Length > 0 && title.Length > 0 && keys.Add(key))
                    {
                        string toolTip = ReadToolTip(element);
                        entries.Add(new ModSettingsSearchEntry(
                            key,
                            title,
                            toolTip,
                            element));
                        identities.Add(title + "\u001f" + toolTip);
                    }
                }
                else if (IsInteractive(element))
                {
                    string toolTip = ReadToolTip(element);
                    title = FindAutomaticTitle(element, structuralParents);
                    if (title.Length == 0 && toolTip.Length > 0)
                        title = FirstSentence(toolTip);
                    string identity = title + "\u001f" + toolTip;
                    if (title.Length > 0 && toolTip.Length > 0 && identities.Add(identity))
                    {
                        key = BuildAutomaticKey(element, title, toolTip);
                        int suffix = 2;
                        string uniqueKey = key;
                        while (!keys.Add(uniqueKey))
                            uniqueKey = key + ":" + suffix++;

                        // Store the generated metadata on the real navigation target. This turns
                        // the shared convention into a durable runtime anchor for later re-indexes.
                        SetKey(element, uniqueKey);
                        SetTitle(element, title);
                        entries.Add(new ModSettingsSearchEntry(uniqueKey, title, toolTip, element));
                    }
                }
            }

            foreach (DependencyObject child in EnumerateChildren(current))
                Visit(child, entries, keys, identities, visited, structuralParents, current, depth + 1);
        }

        private static IEnumerable<DependencyObject> EnumerateChildren(DependencyObject current)
        {
            // Noesis does not create the visual tree of an unselected mod tab. Its declarative
            // content tree is nevertheless already available after LoadXaml, so inspect both.
            var result = new List<DependencyObject>();
            var seen = new HashSet<DependencyObject>();

            if (current is Panel panel)
            {
                for (int index = 0; index < panel.Children.Count; index++)
                    AddChild(result, seen, panel.Children[index]);
            }
            if (current is Decorator decorator)
                AddChild(result, seen, decorator.Child);
            if (current is ContentControl contentControl)
                AddChild(result, seen, contentControl.Content as DependencyObject);
            int visualCount = VisualTreeHelper.GetChildrenCount(current);
            for (int index = 0; index < visualCount; index++)
                AddChild(result, seen, VisualTreeHelper.GetChild(current, index));
            return result;
        }

        private static void AddChild(
            ICollection<DependencyObject> result,
            ISet<DependencyObject> seen,
            DependencyObject child)
        {
            if (child != null && seen.Add(child))
                result.Add(child);
        }

        private static bool IsInteractive(FrameworkElement element) =>
            element is Button || element is CheckBox || element is ComboBox ||
            element is Slider || element is TextBox;

        private static string BuildAutomaticKey(FrameworkElement element, string title, string toolTip)
        {
            Binding binding = BindingOperations.GetBinding(element, ToolTipService.ToolTipProperty);
            string bindingPath = binding?.Path?.Path ?? string.Empty;
            string seed = bindingPath.Length > 0 ? bindingPath + ":" + title : title + ":" + toolTip;
            return "auto:" + StableHash(seed).ToString("x8", CultureInfo.InvariantCulture);
        }

        internal static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return hash;
            }
        }

        private static string FindAutomaticTitle(
            FrameworkElement element,
            IReadOnlyDictionary<DependencyObject, DependencyObject> structuralParents)
        {
            if (element is ContentControl contentControl)
            {
                string contentText = ReadContentText(contentControl.Content);
                if (contentText.Length > 0)
                    return contentText;
            }

            DependencyObject current = element;
            for (int level = 0; level < 4; level++)
            {
                DependencyObject parent = VisualTreeHelper.GetParent(current);
                if (parent == null)
                    structuralParents.TryGetValue(current, out parent);
                if (parent == null)
                    break;
                int row = Grid.GetRow(current);
                int count = VisualTreeHelper.GetChildrenCount(parent);
                for (int index = 0; index < count; index++)
                {
                    DependencyObject sibling = VisualTreeHelper.GetChild(parent, index);
                    if (ReferenceEquals(sibling, current))
                        continue;
                    if (sibling is TextBlock textBlock &&
                        (!(parent is Grid) || Grid.GetRow(textBlock) == row) &&
                        !string.IsNullOrWhiteSpace(textBlock.Text))
                    {
                        return textBlock.Text.Trim();
                    }
                    if (sibling is FrameworkElement siblingElement &&
                        (!(parent is Grid) || Grid.GetRow(siblingElement) == row))
                    {
                        string nested = ReadContentText(siblingElement);
                        if (nested.Length > 0)
                            return nested;
                    }
                }
                current = parent;
            }
            return string.Empty;
        }

        private static string ReadContentText(object content)
        {
            if (content is string text)
                return text.Trim();
            if (content is TextBlock textBlock)
                return textBlock.Text?.Trim() ?? string.Empty;
            if (content is DependencyObject dependencyObject)
            {
                int count = VisualTreeHelper.GetChildrenCount(dependencyObject);
                for (int index = 0; index < count; index++)
                {
                    string nested = ReadContentText(VisualTreeHelper.GetChild(dependencyObject, index));
                    if (nested.Length > 0)
                        return nested;
                }
            }
            return string.Empty;
        }

        internal static string FirstSentence(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            int end = normalized.IndexOfAny(new[] { '.', '!', '?', '\r', '\n' });
            if (end >= 0)
                normalized = normalized.Substring(0, end + 1);
            return normalized.Length <= 120 ? normalized : normalized.Substring(0, 117) + "...";
        }

        private static string ReadToolTip(DependencyObject element)
        {
            object toolTip = ToolTipService.GetToolTip(element);
            if (toolTip is ToolTip explicitToolTip)
                toolTip = explicitToolTip.Content;
            return ReadContentText(toolTip);
        }
    }

    /// <summary>
    /// Reflection-friendly export shape. Consumers must inspect the public properties instead
    /// of casting because Shared is compiled into each standalone mod assembly.
    /// </summary>
    public sealed class ModSettingsSearchEntry
    {
        public ModSettingsSearchEntry(string key, string title, string toolTip, FrameworkElement target)
        {
            Key = key;
            Title = title;
            ToolTip = toolTip;
            Target = target;
        }

        public string Key { get; }
        public string Title { get; }
        public string ToolTip { get; }
        public FrameworkElement Target { get; }
    }

    internal sealed class SearchSource
    {
        private readonly string path;
        private readonly BepInEx.Logging.ManualLogSource log;
        private readonly string modName;
        private object cachedViewModel;
        private IReadOnlyList<ModSettingsSearchEntry> cachedEntries;

        public SearchSource(
            string path,
            BepInEx.Logging.ManualLogSource log,
            string modName)
        {
            this.path = path;
            this.log = log;
            this.modName = modName ?? string.Empty;
        }

        public IReadOnlyList<ModSettingsSearchEntry> GetEntries(object viewModel)
        {
            if (ReferenceEquals(cachedViewModel, viewModel) && cachedEntries != null)
                return cachedEntries;
            try
            {
                cachedEntries = BuildCatalog(path, viewModel);
                cachedViewModel = viewModel;
                return cachedEntries;
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"[{modName}] Could not build the preloaded mod-settings search catalog from [{System.IO.Path.GetFileName(path)}]: {ex.Message}");
                cachedEntries = Array.Empty<ModSettingsSearchEntry>();
                cachedViewModel = viewModel;
                return cachedEntries;
            }
        }

        private static IReadOnlyList<ModSettingsSearchEntry> BuildCatalog(
            string path,
            object rootViewModel)
        {
            XDocument document = XDocument.Load(path, LoadOptions.None);
            var result = new List<ModSettingsSearchEntry>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (XElement element in document.Descendants())
            {
                if (IsExcluded(element))
                    continue;
                string localName = element.Name.LocalName;
                bool interactive = localName == "Button" || localName == "CheckBox" ||
                    localName == "ComboBox" || localName == "Slider" || localName == "TextBox";
                XAttribute keyAttribute = FindAttribute(element, "Key");
                XAttribute titleAttribute = FindAttribute(element, "Title");
                if (!interactive && keyAttribute == null && titleAttribute == null)
                    continue;

                foreach (object dataContext in ResolveDataContexts(element, rootViewModel))
                {
                    string title = ResolveValue(titleAttribute?.Value, dataContext);
                    if (title.Length == 0)
                        title = ResolveElementContent(element, dataContext);
                    string toolTip = ResolveToolTip(element, dataContext);
                    if (title.Length == 0)
                        title = FindNearbyTitle(element, dataContext);
                    if (title.Length == 0 && toolTip.Length > 0)
                        title = ModSettingsSearch.FirstSentence(toolTip);
                    if (title.Length == 0 || toolTip.Length == 0)
                        continue;

                    string key = ResolveValue(keyAttribute?.Value, dataContext);
                    if (key.Length == 0)
                    {
                        string toolTipPath = ReadBindingPath(FindAttribute(element, "ToolTip")?.Value);
                        string seed = toolTipPath.Length > 0
                            ? toolTipPath + ":" + title
                            : title + ":" + toolTip;
                        key = "auto:" + ModSettingsSearch.StableHash(seed).ToString("x8", CultureInfo.InvariantCulture);
                    }
                    string identity = title + "\u001f" + toolTip;
                    if (!identities.Add(identity))
                        continue;
                    string uniqueKey = key;
                    int suffix = 2;
                    while (!keys.Add(uniqueKey))
                        uniqueKey = key + ":" + suffix++;
                    result.Add(new ModSettingsSearchEntry(uniqueKey, title, toolTip, null));
                }
            }
            return result;
        }

        private static IEnumerable<object> ResolveDataContexts(XElement element, object root)
        {
            XElement template = element.Ancestors().FirstOrDefault(value =>
                value.Name.LocalName == "DataTemplate");
            if (template == null)
                return new[] { root };
            XElement itemsControl = template.Ancestors().FirstOrDefault(value =>
                value.Name.LocalName == "ItemsControl");
            string path = ReadBindingPath(FindAttribute(itemsControl, "ItemsSource")?.Value);
            object items = ReadPropertyPath(root, path);
            if (items is System.Collections.IEnumerable enumerable && !(items is string))
                return enumerable.Cast<object>().Where(value => value != null).ToArray();
            return Array.Empty<object>();
        }

        private static bool IsExcluded(XElement element) =>
            element.AncestorsAndSelf().Any(value =>
                string.Equals(FindAttribute(value, "Exclude")?.Value, "True", StringComparison.OrdinalIgnoreCase));

        private static string ResolveToolTip(XElement element, object dataContext)
        {
            string result = ResolveValue(FindAttribute(element, "ToolTip")?.Value, dataContext);
            if (result.Length > 0)
                return result;
            XElement property = element.Elements().FirstOrDefault(value =>
                value.Name.LocalName.EndsWith(".ToolTip", StringComparison.Ordinal));
            if (property == null)
                return string.Empty;
            foreach (XElement value in property.DescendantsAndSelf())
            {
                result = ResolveValue(FindAttribute(value, "Text")?.Value, dataContext);
                if (result.Length > 0)
                    return result;
                result = ResolveValue(FindAttribute(value, "Content")?.Value, dataContext);
                if (result.Length > 0)
                    return result;
            }
            return string.Empty;
        }

        private static string ResolveElementContent(XElement element, object dataContext)
        {
            string result = ResolveValue(FindAttribute(element, "Content")?.Value, dataContext);
            if (result.Length > 0)
                return result;

            IEnumerable<XElement> candidates = element.Elements().Where(value =>
                value.Name.LocalName == "TextBlock" ||
                value.Name.LocalName.EndsWith(".Content", StringComparison.Ordinal));
            foreach (XElement container in candidates)
            {
                foreach (XElement candidate in container.DescendantsAndSelf())
                {
                    if (candidate.Name.LocalName != "TextBlock")
                        continue;
                    result = ResolveValue(FindAttribute(candidate, "Text")?.Value, dataContext);
                    if (result.Length > 0)
                        return result;
                }
            }
            return string.Empty;
        }

        private static string FindNearbyTitle(XElement element, object dataContext)
        {
            string row = FindAttribute(element, "Row")?.Value ?? "0";
            XElement current = element;
            for (int level = 0; level < 4 && current.Parent != null; level++)
            {
                XElement parent = current.Parent;
                foreach (XElement sibling in parent.Elements())
                {
                    if (ReferenceEquals(sibling, current))
                        continue;
                    string siblingRow = FindAttribute(sibling, "Row")?.Value ?? "0";
                    if (parent.Name.LocalName == "Grid" && siblingRow != row)
                        continue;
                    foreach (XElement candidate in sibling.DescendantsAndSelf())
                    {
                        if (candidate.Name.LocalName != "TextBlock")
                            continue;
                        string text = ResolveValue(FindAttribute(candidate, "Text")?.Value, dataContext);
                        if (text.Length > 0)
                            return text;
                    }
                }
                current = parent;
                row = FindAttribute(current, "Row")?.Value ?? "0";
            }
            return string.Empty;
        }

        private static XAttribute FindAttribute(XElement element, string localName) =>
            element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName);

        private static string ResolveValue(string xamlValue, object dataContext)
        {
            if (string.IsNullOrWhiteSpace(xamlValue))
                return string.Empty;
            string path = ReadBindingPath(xamlValue);
            if (xamlValue.TrimStart().StartsWith("{Binding", StringComparison.Ordinal) &&
                path.Length == 0)
            {
                return string.Empty;
            }
            if (path.Length == 0)
                return xamlValue.Trim();
            return ReadPropertyPath(dataContext, path)?.ToString()?.Trim() ?? string.Empty;
        }

        private static string ReadBindingPath(string value)
        {
            string text = value?.Trim() ?? string.Empty;
            if (!text.StartsWith("{Binding", StringComparison.Ordinal) ||
                !text.EndsWith("}", StringComparison.Ordinal))
                return string.Empty;
            text = text.Substring(8, text.Length - 9).Trim();
            foreach (string part in text.Split(','))
            {
                string token = part.Trim();
                if (token.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                    return token.Substring(5).Trim();
                if (token.Length > 0 && token.IndexOf('=') < 0)
                    return token;
            }
            return string.Empty;
        }

        private static object ReadPropertyPath(object instance, string path)
        {
            object current = instance;
            foreach (string segment in (path ?? string.Empty).Split('.'))
            {
                if (current == null || segment.Length == 0)
                    return null;
                PropertyInfo property = current.GetType().GetProperty(
                    segment,
                    BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead)
                    return null;
                current = property.GetValue(current, null);
            }
            return current;
        }
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
#endif
