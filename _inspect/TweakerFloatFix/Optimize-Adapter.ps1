$ErrorActionPreference='Stop'
function Edit($path, [scriptblock]$change) {
 $full=Join-Path (Get-Location) $path
 $s=[IO.File]::ReadAllText($full).Replace("`r`n","`n")
 $s=& $change $s
 $s=$s.Replace("`r`n","`n").Replace("`n","`r`n")
 [IO.File]::WriteAllText($full,$s)
 if (![string]::Equals([IO.File]::ReadAllText($full),$s,[StringComparison]::Ordinal)){throw 'Write mismatch'}
}
Edit 'SerpsModsHost/src/StatsTweakerPresetAdapter.cs' {
 param($s)
 $s=$s.Replace('using System.Reflection;', "using System.Reflection;`nusing System.Diagnostics;`nusing System.Collections.Concurrent;")
 $s=$s.Replace('var provider = new StatsTweakerConfigurationProvider(api);', 'var phase = Stopwatch.StartNew();' + "`n                    " + 'var provider = new StatsTweakerConfigurationProvider(api, message => log.LogInfo(message));')
 $s=$s.Replace('var candidate = new StatsTweakerPresetViewModel(provider);', 'log.LogInfo("[PresetPerf] provider ready: options=" + provider.GetSettings().Count + ", ms=" + phase.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));' + "`n                    phase.Restart();`n                    var candidate = new StatsTweakerPresetViewModel(provider);")
 $s=$s.Replace('                    candidate.ImportOwnFiles();', '                    log.LogInfo("[PresetPerf] preset activation: ms=" + phase.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", " + provider.PerformanceCounts);')
 $s=$s.Replace('FrameworkElement panel;', "var uiWatch = Stopwatch.StartNew();`n                FrameworkElement panel;")
 $s=$s.Replace('attached = true; //', 'log.LogInfo("[PresetPerf] preset panel: ms=" + uiWatch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));' + "`n                attached = true; //")
 $s=$s.Replace('private readonly Type api;', @'
private readonly Type api;
        private readonly Action<string> diagnostics;
        private int ownReads, validations;
        internal string PerformanceCounts => "ownReads=" + ownReads + ", validations=" + validations;
        private static readonly ConcurrentDictionary<Tuple<Type, string>, PropertyInfo> properties = new ConcurrentDictionary<Tuple<Type, string>, PropertyInfo>();
'@)
 $s=$s.Replace('internal StatsTweakerConfigurationProvider(Type api)', 'internal StatsTweakerConfigurationProvider(Type api, Action<string> diagnostics = null)')
 $s=$s.Replace('this.api = api;', 'this.api = api; this.diagnostics = diagnostics;')
 $s=$s.Replace("            var own = ReadOwn();`n            ValidateValues(own);`n            working = own;", '            working = ReadOwn();')
 $s=$s.Replace('PropertyInfo info = value?.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);', @'
if (value == null) throw new InvalidDataException("Missing Tweaker API value: " + property);
            PropertyInfo info = properties.GetOrAdd(Tuple.Create(value.GetType(), property), key =>
                key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidDataException("Missing Tweaker API property: " + key.Item2));
'@)
 $s=$s.Replace('object snapshot = Call("ReadOwnConfiguration");', "var watch = Stopwatch.StartNew();`n            ownReads++;`n            object snapshot = Call(`"ReadOwnConfiguration`");")
 $s=$s.Replace('            return values;', '            diagnostics?.Invoke("[PresetPerf] read personal configuration: ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", " + PerformanceCounts);' + "`n            return values;")
 $s=$s.Replace('            string[] errors = ((IEnumerable)Call("ValidateConfiguration", values))', "            var watch = Stopwatch.StartNew();`n            validations++;`n            string[] errors = ((IEnumerable)Call(`"ValidateConfiguration`", values))")
 $s=$s.Replace('            if (errors.Length != 0)', '            diagnostics?.Invoke("[PresetPerf] validate configuration: options=" + values.Count + ", ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));' + "`n            if (errors.Length != 0)")
 $s=$s.Replace("        public void ReplaceValues(Dictionary<string, object> values)`n        {`n            ValidateValues(values);", @'
        public void ReplaceValues(Dictionary<string, object> values)
        {
            // An equal copy of our already validated snapshot needs no second validation.
            if (working != null && values != null && values.Count == working.Count &&
                values.All(x => working.TryGetValue(x.Key, out var current) && Equals(current, x.Value))) return;
            ValidateValues(values);
'@)
 $s
}
Edit 'SerpsModsHost/src/StatsTweakerPresetViewModel.cs' {
 param($s)
 $start=$s.IndexOf('        internal void ImportOwnFiles()'); $end=$s.IndexOf('        private void Run(', $start)
 if($start -lt 0 -or $end -lt 0){throw 'missing import helper'}
 $s.Substring(0,$start)+$s.Substring($end)
}
Edit 'APIShared/src/ModSettings/PresetLobbyModSettingsViewModel.cs' {
 param($s)
 $s=$s.Replace('            public IReadOnlyList<PresetSettingDescriptor> GetSettingDescriptors() =>', @'
            private PresetPropertyAccessor[] descriptorOrder;
            public IReadOnlyList<PresetSettingDescriptor> GetSettingDescriptors()
            {
                if (descriptorOrder == null)
                    descriptorOrder = persistedProperties.OrderBy(p => IsHostProperty(p) ? PresetSettingScope.Host :
                        p.GetCustomAttribute<SyncPerPlayerAttribute>() != null ? PresetSettingScope.Player : PresetSettingScope.Local)
                        .ThenBy(p => p.Name, StringComparer.Ordinal).ToArray();
                // Return fresh descriptors; capabilities remain live and callers cannot mutate the cache.
                return
'@)
 $s=$s.Replace('                persistedProperties.Select(property => new PresetSettingDescriptor', '                descriptorOrder.Select(property => new PresetSettingDescriptor')
 $s=$s.Replace('                }).OrderBy(item => item.Scope).ThenBy(item => item.PropertyName, StringComparer.Ordinal).ToArray();', "                }).ToArray();`n            }")
 $s
}
Edit 'ExtendedData/Override/ScriptExtenderUI/ExtendedDataSettings.xaml' {
 param($s)
 $anchor='              <ItemsControl ItemsSource="{Binding Settings}"'
 $extra=@'
              <WrapPanel Visibility="{Binding PagingVisibility}" Margin="26,4,0,4" shared:ModSettingsSearch.Exclude="True">
                <TextBlock Text="{Binding FilterHelp}" Foreground="White" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBox ui:KeyboardCaptureBinding.Enabled="True" Text="{Binding Filter, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="240" ToolTip="{Binding FilterHelp}" ToolTipService.ShowDuration="60000"/>
                <Button Content="◀" Command="{Binding PreviousPageCommand}" ToolTip="{Binding PreviousHelp}" ToolTipService.ShowDuration="60000" Margin="8,0,0,0"/>
                <TextBlock Text="{Binding PageText}" Foreground="White" VerticalAlignment="Center" Margin="8,0"/>
                <Button Content="▶" Command="{Binding NextPageCommand}" ToolTip="{Binding NextHelp}" ToolTipService.ShowDuration="60000"/>
              </WrapPanel>
'@
 $s.Replace($anchor,$extra+"`n"+$anchor)
}
foreach($file in Get-ChildItem ExtendedData/Locales -Filter '*.txt') {
 Edit ('ExtendedData/Locales/'+$file.Name) {
  param($s)
  if($file.Name -eq 'de-DE.txt') {$s+="`nExtendedData.Options.Filter=Einstellungen filtern`nExtendedData.Options.Previous=Vorherige Seite`nExtendedData.Options.Next=Nächste Seite`n"}
  else {$s+="`nExtendedData.Options.Filter=Filter settings`nExtendedData.Options.Previous=Previous page`nExtendedData.Options.Next=Next page`n"}
  $s
 }
}
