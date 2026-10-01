from pathlib import Path
def write(p,s): Path(p).write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))

p='BugfixesAndQoL/src/LargeMoveTargetMarkerRenderer.cs'
s=Path(p).read_text(encoding='utf-8-sig')
s=s.replace('        private bool overlayPassActive;', '''        private bool overlayPassActive;
        private Func<bool> previewAuthorization;
        private bool previewCheckedThisPass, previewAllowedThisPass;''')
# Keep the old public internal call shape while requiring an authorization for previews.
s=s.replace('        public void SetPreviewMarkerTiles(IEnumerable<int> tileIds)\n        {', '''        public void SetPreviewMarkerTiles(IEnumerable<int> tileIds) =>
            SetPreviewMarkerTiles(tileIds, null);

        public void SetPreviewMarkerTiles(IEnumerable<int> tileIds, Func<bool> authorization)
        {''')
s=s.replace('                previewRequestBuffer.Clear();\n                if (ReplacementAvailable', '''                if (!ReferenceEquals(previewAuthorization, authorization))
                {
                    previewCheckedThisPass = false;
                    previewAllowedThisPass = false;
                }
                previewAuthorization = authorization;
                previewRequestBuffer.Clear();
                if (ReplacementAvailable''')
s=s.replace('                publishedPreview = EmptyPreview;', '''                publishedPreview = EmptyPreview;
                previewAuthorization = null;
                previewAllowedThisPass = false;''')
s=s.replace('                bool renderedOverflow =', '''                previewCheckedThisPass = false;
                previewAllowedThisPass = false;
                bool renderedOverflow =''')
s=s.replace('                if (preview.Count != 0)\n                {', '''                if (preview.Count != 0)
                {
                    if (!previewCheckedThisPass)
                    {
                        // ResetDrawList runs at the END of the preceding render pass.
                        // No renderer lock is held across the gesture callback.
                        Func<bool> authorization = previewAuthorization;
                        previewAllowedThisPass = authorization != null && authorization();
                        previewCheckedThisPass = true;
                    }
                    if (!previewAllowedThisPass) return;''')
write(p,s)
p='BugfixesAndQoL/src/LargeMoveTargetMarkerRuntime.cs'
s=Path(p).read_text(encoding='utf-8-sig')
s=s.replace('        public void ClearPreview()', '''        public void SetPreview(IEnumerable<int> tileIds, Func<bool> authorization) =>
            renderer.SetPreviewMarkerTiles(tileIds, authorization);

        public void ClearPreview()''')
write(p,s)
p='BugfixesAndQoL/src/MoveFormationDragRuntime.cs'
s=Path(p).read_text(encoding='utf-8-sig')
s=s.replace('        private NativeTroopCommandModeReader commandModeReader;', '''        private NativeTroopCommandModeReader commandModeReader;
        private NativeGroundMoveFeedbackReader groundFeedbackReader;''')
s=s.replace('                mouseTileXField = RequireEditorField', '''                groundFeedbackReader = new NativeGroundMoveFeedbackReader(
                    libraryContext.ModuleHandle, libraryContext.Memory);
                mouseTileXField = RequireEditorField''')
s=s.replace('                Screen.width);\n            lock (dragSync)', '''                Screen.width,
                GamePlayerManagerAPI.Instance.GetLocalPlayerId());
            state.PreviewAuthorization = () => AuthorizePreview(state);
            lock (dragSync)''')
s=s.replace('                EditorDirector director = EditorDirector.instance;\n                lock (dragSync)\n                {\n                    if (!failed && drag != null', '''                EditorDirector director = EditorDirector.instance;
                lock (dragSync)
                {
                    // Selection/map/control changes invalidate the anchored proof before
                    // native cursor dispatch. No extra route search is performed here.
                    if (drag != null && !ValidateActiveDrag(drag))
                    {
                        drag = null;
                        markers.ClearPreview();
                    }
                    if (!failed && drag != null''')
s=s.replace('                markers.SetPreview(previewTiles);', '                markers.SetPreview(previewTiles, state.PreviewAuthorization);')
a=s.index('        private void PublishPreview(DragState state)')
s=s[:a]+'''        private bool AuthorizePreview(DragState state)
        {
            lock (dragSync)
            {
                if (!Enabled || !ReferenceEquals(drag, state) ||
                    state.ReleaseGate.Released || state.ReleaseGate.Aborted ||
                    groundFeedbackReader == null)
                    return false;
                return state.Authorization.Observe(groundFeedbackReader.Read());
            }
        }

'''+s[a:]
s=s.replace('                int screenWidth)\n            {\n                TribeId = tribeId;', '''                int screenWidth,
                int playerId)
            {
                Authorization = new GroundMovePreviewAuthorization(playerId, tribeId,
                    selection.Length, target.NativeX, target.NativeY);
                TribeId = tribeId;''')
s=s.replace('            internal int TribeId { get; }\n            internal GroundTarget', '''            internal GroundMovePreviewAuthorization Authorization { get; }
            internal Func<bool> PreviewAuthorization { get; set; }
            internal int TribeId { get; }
            internal GroundTarget''')
write(p,s)
for p in ['BugfixesAndQoL/BugfixesAndQoL.csproj',
          'BugfixesAndQoL/tests/ExtendedShiftCommandQueue.Tests/ExtendedShiftCommandQueue.Tests.csproj']:
    s=Path(p).read_text(encoding='utf-8-sig')
    item='    <Compile Include="src\\GroundMovePreviewAuthorization.cs" />' if '/tests/' not in p else '    <Compile Include="..\\..\\src\\GroundMovePreviewAuthorization.cs" Link="GroundMovePreviewAuthorization.cs" />'
    s=s.replace('  <ItemGroup>','  <ItemGroup>\n'+item,1)
    write(p,s)
p='BugfixesAndQoL/src/GroundMovePreviewAuthorization.cs'
s=Path(p).read_text().replace('Read(0x3A11DE4), Read(0x3A11DF0)', 'Read(0x3A11DF0), Read(0x3A11DE4)')
write(p,s)
