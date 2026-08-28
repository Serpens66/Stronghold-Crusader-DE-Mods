using System;
using System.Globalization;
using System.Linq;
using System.Text;
#if !SHARED_PRESET_TESTS
using Noesis;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
#endif

namespace Shared
{
    /// <summary>Pure matching policy shared by runtime filtering and isolated tests.</summary>
    public static class ModSettingsSearchMatcher
    {
        public static bool IsMatch(
            string filterText,
            bool includeToolTips,
            string exactKey,
            string key,
            string title,
            string toolTip)
        {
            return IsMatch(
                filterText,
                includeToolTips,
                exactKey,
                key,
                title,
                toolTip,
                string.Empty,
                string.Empty);
        }

        public static bool IsMatch(
            string filterText,
            bool includeToolTips,
            string exactKey,
            string key,
            string title,
            string toolTip,
            string sectionKey,
            string sectionTitle)
        {
            string exact = Normalize(exactKey);
            if (exact.Length > 0)
            {
                return string.Equals(Normalize(key), exact, StringComparison.Ordinal) ||
                    string.Equals(Normalize(sectionKey), exact, StringComparison.Ordinal);
            }

            string filter = Normalize(filterText);
            if (filter.Length == 0)
                return true;

            string visibleText = JoinSearchText(title, sectionTitle);
            if (ContainsAllTerms(visibleText, filter))
                return true;
            return includeToolTips && ContainsAllTerms(JoinSearchText(visibleText, toolTip), filter);
        }

        public static bool IsSectionTitleMatch(string filterText, string sectionTitle) =>
            ContainsAllTerms(sectionTitle, Normalize(filterText));

        public static string Normalize(string value) => (value ?? string.Empty).Trim();

        private static bool ContainsAllTerms(string value, string filter)
        {
            if (filter.Length == 0)
                return true;
            string haystack = Fold(value);
            string needle = Fold(filter);
            if (haystack.Contains(needle))
                return true;
            return needle.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .All(term => haystack.Contains(term));
        }

        private static string JoinSearchText(params string[] values) =>
            string.Join(" ", (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)));

        private static string Fold(string value)
        {
            string decomposed = Normalize(value).Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(decomposed.Length);
            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    result.Append(char.ToLower(character, CultureInfo.CurrentCulture));
            }
            return result.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}

#if !SHARED_PRESET_TESTS
namespace Shared
{
    /// <summary>
    /// Explicit, localized metadata for one logical mod-setting entry. The properties are
    /// attached to the smallest container that represents the logical setting.
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

        public static readonly DependencyProperty ToolTipTextProperty =
            DependencyProperty.RegisterAttached(
                "ToolTipText",
                typeof(string),
                typeof(ModSettingsSearch),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SectionKeyProperty =
            DependencyProperty.RegisterAttached(
                "SectionKey",
                typeof(string),
                typeof(ModSettingsSearch),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty SectionTitleProperty =
            DependencyProperty.RegisterAttached(
                "SectionTitle",
                typeof(string),
                typeof(ModSettingsSearch),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty IsSectionProperty =
            DependencyProperty.RegisterAttached(
                "IsSection",
                typeof(bool),
                typeof(ModSettingsSearch),
                new PropertyMetadata(false));

        public static readonly DependencyProperty FilterTextProperty =
            DependencyProperty.RegisterAttached(
                "FilterText",
                typeof(string),
                typeof(ModSettingsSearch),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty ExactKeyProperty =
            DependencyProperty.RegisterAttached(
                "ExactKey",
                typeof(string),
                typeof(ModSettingsSearch),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty IncludeToolTipsProperty =
            DependencyProperty.RegisterAttached(
                "IncludeToolTips",
                typeof(bool),
                typeof(ModSettingsSearch),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty FocusRequestProperty =
            DependencyProperty.RegisterAttached(
                "FocusRequest",
                typeof(int),
                typeof(ModSettingsSearch),
                new PropertyMetadata(0, OnFocusRequestChanged));

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

        public static string GetToolTipText(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(ToolTipTextProperty) as string ?? string.Empty;

        public static void SetToolTipText(DependencyObject value, string text) =>
            value?.SetValue(ToolTipTextProperty, text ?? string.Empty);

        public static string GetSectionKey(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(SectionKeyProperty) as string ?? string.Empty;

        public static void SetSectionKey(DependencyObject value, string key) =>
            value?.SetValue(SectionKeyProperty, key ?? string.Empty);

        public static string GetSectionTitle(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(SectionTitleProperty) as string ?? string.Empty;

        public static void SetSectionTitle(DependencyObject value, string title) =>
            value?.SetValue(SectionTitleProperty, title ?? string.Empty);

        public static bool GetIsSection(DependencyObject value) =>
            value != null && value.GetValue(IsSectionProperty) is bool isSection && isSection;

        public static void SetIsSection(DependencyObject value, bool isSection) =>
            value?.SetValue(IsSectionProperty, isSection);

        public static string GetFilterText(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(FilterTextProperty) as string ?? string.Empty;

        public static void SetFilterText(DependencyObject value, string text) =>
            value?.SetValue(FilterTextProperty, text ?? string.Empty);

        public static string GetExactKey(DependencyObject value) =>
            value == null ? string.Empty : value.GetValue(ExactKeyProperty) as string ?? string.Empty;

        public static void SetExactKey(DependencyObject value, string key) =>
            value?.SetValue(ExactKeyProperty, key ?? string.Empty);

        public static bool GetIncludeToolTips(DependencyObject value) =>
            value != null && value.GetValue(IncludeToolTipsProperty) is bool enabled && enabled;

        public static void SetIncludeToolTips(DependencyObject value, bool enabled) =>
            value?.SetValue(IncludeToolTipsProperty, enabled);

        public static int GetFocusRequest(DependencyObject value) =>
            value != null && value.GetValue(FocusRequestProperty) is int request ? request : 0;

        public static void SetFocusRequest(DependencyObject value, int request) =>
            value?.SetValue(FocusRequestProperty, request);

        private static void OnFocusRequestChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is TextBox textBox) ||
                !(args.NewValue is int request) ||
                request <= 0 ||
                Equals(args.OldValue, args.NewValue) ||
                !textBox.IsVisible ||
                !textBox.IsEnabled)
            {
                return;
            }

            // The request is raised only after the local search row became visible. Keeping the
            // concrete TextBox as the target avoids tree traversal and cross-tab focus attempts.
            textBox.Focus();
            textBox.SelectAll();
        }

        /// <summary>
        /// Registers the immutable XAML catalog used by the optional SerpsModsHost. Each mod
        /// remains standalone because the host consumes only the reflection-friendly data shape.
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

        public static bool HasMatches(
            object viewModel,
            string filterText,
            bool includeToolTips,
            string exactKey)
        {
            if (viewModel == null || !Sources.TryGetValue(viewModel, out SearchSource source))
                return true;
            return source.GetEntries(viewModel).Any(entry => ModSettingsSearchMatcher.IsMatch(
                filterText,
                includeToolTips,
                exactKey,
                entry.Key,
                entry.Title,
                entry.ToolTip,
                entry.SectionKey,
                entry.SectionTitle));
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
                string sectionKey = GetSectionKey(element).Trim();
                string sectionTitle = GetSectionTitle(element).Trim();
                bool isSection = GetIsSection(element);
                if (isSection)
                {
                    if (sectionKey.Length > 0 && sectionTitle.Length > 0 && keys.Add(sectionKey))
                    {
                        entries.Add(new ModSettingsSearchEntry(
                            sectionKey,
                            sectionTitle,
                            string.Empty,
                            sectionKey,
                            sectionTitle,
                            true));
                    }
                }
                else if (key.Length > 0 || title.Length > 0)
                {
                    // Explicit metadata is authoritative. Repeated keys deliberately allow a
                    // logical setting with several controls to select its first navigation target.
                    if (key.Length > 0 && title.Length > 0 && keys.Add(key))
                    {
                        string toolTip = GetToolTipText(element).Trim();
                        if (toolTip.Length == 0)
                            toolTip = ReadToolTip(element);
                        entries.Add(new ModSettingsSearchEntry(
                            key,
                            title,
                            toolTip,
                            sectionKey,
                            sectionTitle,
                            false));
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

                        entries.Add(new ModSettingsSearchEntry(uniqueKey, title, toolTip));
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

    public sealed class ModSettingsSearchVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string filterText = ReadString(values, 0);
            bool includeToolTips = values != null && values.Length > 1 && values[1] is bool value && value;
            string exactKey = ReadString(values, 2);
            if (values == null || values.Length < 6)
                return Visibility.Collapsed;
            for (int index = 3; index + 2 < values.Length; index += 5)
            {
                if (ModSettingsSearchMatcher.IsMatch(
                    filterText,
                    includeToolTips,
                    exactKey,
                    ReadString(values, index),
                    ReadString(values, index + 1),
                    ReadString(values, index + 2),
                    ReadString(values, index + 3),
                    ReadString(values, index + 4)))
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static string ReadString(object[] values, int index) =>
            values != null && index < values.Length && values[index] != null
                ? values[index].ToString()
                : string.Empty;
    }

    /// <summary>
    /// Reflection-friendly export shape. Consumers must inspect the public properties instead
    /// of casting because Shared is compiled into each standalone mod assembly.
    /// </summary>
    public sealed class ModSettingsSearchEntry
    {
        public ModSettingsSearchEntry(string key, string title, string toolTip)
            : this(key, title, toolTip, string.Empty, string.Empty, false)
        {
        }

        public ModSettingsSearchEntry(
            string key,
            string title,
            string toolTip,
            string sectionKey,
            string sectionTitle,
            bool isSection)
        {
            Key = key;
            Title = title;
            ToolTip = toolTip;
            SectionKey = sectionKey ?? string.Empty;
            SectionTitle = sectionTitle ?? string.Empty;
            IsSection = isSection;
        }

        public string Key { get; }
        public string Title { get; }
        public string ToolTip { get; }
        public string SectionKey { get; }
        public string SectionTitle { get; }
        public bool IsSection { get; }
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
                bool isSection = string.Equals(
                    FindAttribute(element, "IsSection")?.Value,
                    "True",
                    StringComparison.OrdinalIgnoreCase);
                bool hasExplicitAncestor = element.Ancestors().Any(value =>
                    FindAttribute(value, "Key") != null || FindAttribute(value, "Title") != null);
                if (interactive && keyAttribute == null && titleAttribute == null && hasExplicitAncestor)
                    continue;
                if (!isSection && !interactive && keyAttribute == null && titleAttribute == null)
                    continue;

                foreach (object dataContext in ResolveDataContexts(element, rootViewModel))
                {
                    XElement sectionOwner = FindSectionOwner(element);
                    object sectionDataContext = IsInsideDataTemplate(sectionOwner)
                        ? dataContext
                        : rootViewModel;
                    string sectionKey = ResolveValue(
                        FindAttribute(sectionOwner, "SectionKey")?.Value,
                        sectionDataContext);
                    string sectionTitle = ResolveValue(
                        FindAttribute(sectionOwner, "SectionTitle")?.Value,
                        sectionDataContext);
                    if (isSection)
                    {
                        if (sectionKey.Length == 0 || sectionTitle.Length == 0 || !keys.Add(sectionKey))
                            continue;
                        result.Add(new ModSettingsSearchEntry(
                            sectionKey,
                            sectionTitle,
                            string.Empty,
                            sectionKey,
                            sectionTitle,
                            true));
                        continue;
                    }

                    string title = ResolveValue(titleAttribute?.Value, dataContext);
                    if (title.Length == 0)
                        title = ResolveElementContent(element, dataContext);
                    string toolTip = ResolveValue(FindAttribute(element, "ToolTipText")?.Value, dataContext);
                    if (toolTip.Length == 0)
                        toolTip = ResolveToolTip(element, dataContext);
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
                    result.Add(new ModSettingsSearchEntry(
                        uniqueKey,
                        title,
                        toolTip,
                        sectionKey,
                        sectionTitle,
                        false));
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

        private static XElement FindSectionOwner(XElement element) =>
            element?.AncestorsAndSelf().FirstOrDefault(value =>
                FindAttribute(value, "SectionKey") != null ||
                FindAttribute(value, "SectionTitle") != null);

        private static bool IsInsideDataTemplate(XElement element) =>
            element != null && element.AncestorsAndSelf().Any(value =>
                value.Name.LocalName == "DataTemplate");

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
            element?.Attributes().FirstOrDefault(attribute =>
                attribute.Name.LocalName == localName ||
                attribute.Name.LocalName.EndsWith("." + localName, StringComparison.Ordinal));

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
