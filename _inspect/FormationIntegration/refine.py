exec((__import__('pathlib').Path(__file__).parent/'integrate.py').read_text().split("for name in ['FormationModel'")[0])
p=main/'FormationPreviewOverlay.cs';s=read(p)
start=s.index('    internal readonly struct FormationPreviewPoint')
models=s[start:s.rindex('}')]
s=s[:start]+'}\n'
s=s.replace('private static volatile bool showRoleMarkers = true;', '''private static volatile bool showRoleMarkers = true;
        private static volatile bool nativeAllowed;
        internal static void SetNativeAllowed(bool allowed)
        {
            if (nativeAllowed == allowed) return;
            nativeAllowed = allowed;
            Refresh();
        }''')
s=s.replace('        internal static void Clear()\n        {','        internal static void Clear()\n        {\n            nativeAllowed = false;')
s=s.replace('if (current.Points.Length == 0)','if (!nativeAllowed || current.Points.Length == 0)')
write(p,s)
p=shared/'FormationPresentation.cs';s=read(p).replace('        void ClearPreview();','        void ClearPreview();\n        void SetPreviewAuthorization(bool allowed);')
s=s[:s.rindex('}')]+models+'}\n';write(p,s)
p=main/'FormationMenuViewModel.cs';s=read(p)
s=s.replace('        public void RefreshPreview()', '        public void SetPreviewAuthorization(bool allowed) => FormationPreviewOverlay.SetNativeAllowed(allowed);\n        public bool FeatureAvailable => Enabled();\n        private bool lastAvailability;\n        public void RefreshPreview()')
s=s.replace('        public void RefreshHostState()\n        {', '''        public void RefreshHostState()
        {
            if (lastAvailability != FeatureAvailable)
            {
                lastAvailability = FeatureAvailable;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeatureAvailable)));
            }''')
write(p,s)
p=root/'BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
s=s.replace('x:Name="BugfixesAndQoLFormationButtonHost"', 'x:Name="BugfixesAndQoLFormationButtonHost" Visibility="{Binding FeatureAvailable, Converter={StaticResource booleanToVisibilityConverter}}"')
write(p,s)
p=main/'FormationFeature.cs';s=read(p)
s=s.replace('        private static ConfigEntry<FormationKind> kind;', '        internal static bool RuntimeAvailable => runtime != null && runtime.Enabled;\n        private static ConfigEntry<FormationKind> kind;')
write(p,s)
p=main/'LargeMoveTargetMarkerRuntime.cs';s=read(p).replace('settings.EnableMod && settings.EnableMoveFormationEnhancements;', 'settings.EnableMod && settings.EnableMoveFormationEnhancements && FormationFeature.RuntimeAvailable;');write(p,s)
p=shared/'FormationRuntime.cs';s=read(p)
s=s.replace('''            lock (stateSync)
                return Enabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state) &&
                    state.Authorization.Observe(groundFeedbackReader.Read());''', '''            bool allowed;
            lock (stateSync)
                allowed = Enabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state) &&
                    state.Authorization.Observe(groundFeedbackReader.Read());
            menuViewModel.SetPreviewAuthorization(allowed);
            return allowed;''')
write(p,s)
p=root/'BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p).replace('consume.Contains("originalState.ConsumeRelease")','consume.Contains("FormationReleaseStateModel.Consume()")');write(p,s)
# Remove source-only legacy defaults migrations; they existed solely to migrate the prototype's own revisions.
# The ported model tests still cover these pure helpers until final prototype removal; selections use explicit main migration.
write(root/'_inspect/FormationIntegration/changed-refine.txt','\n'.join(changed)+'\n')
