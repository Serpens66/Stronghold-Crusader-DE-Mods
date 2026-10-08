from pathlib import Path
import re, shutil
root=Path.cwd()
def read(p):
    f=root/p
    backup=root/'_inspect/FormationRows/before'/p
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(f,backup)
    return f.read_text(encoding='utf-8-sig')
def write(p,s):
    (root/p).write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
def replace(s,a,b):
    assert a in s,a
    return s.replace(a,b)
p='APIShared/src/UnitCommands/FormationModel.cs'; s=read(p)
s=replace(s,'bool explicitDirection)','bool explicitDirection, int rows)')
s=replace(s,'Width = width;','Width = width;\n            Rows = rows;')
s=replace(s,'internal int Width { get; }','internal int Width { get; }\n        internal int Rows { get; }')
s=replace(s,'bool explicitDirection = false)','bool explicitDirection = false, int rows = 0)')
s=replace(s,'explicitDirection);','explicitDirection, fixedShape ? FormationModel.ResolveAutomaticRows(normalizedKind, unitCount) :\n                    FormationModel.NormalizeRows(rows == 0 ? FormationModel.ResolveAutomaticRows(normalizedKind, unitCount) : rows, unitCount));')
s=replace(s,'DirectionSector == other.DirectionSector && Width == other.Width &&','DirectionSector == other.DirectionSector && Width == other.Width && Rows == other.Rows &&')
s=replace(s,'hash = hash * 397 ^ Width;','hash = hash * 397 ^ Width;\n                hash = hash * 397 ^ Rows;')
s=re.sub(r'        internal static int ChangeDensity.*?\n\n','',s,count=1,flags=re.S)
start=s.index('        internal static int ResolveDraggedWidth('); end=s.index('        internal static List<FormationPoint> BuildRelativeSlots(',start)
s=s[:start]+'''        internal static int NormalizeRows(int rows, int count) =>
            Math.Max(1, Math.Min(Math.Max(1, count), rows));

        internal static int ResolveWidthForRows(FormationKind kind, int count, int rows)
        {
            if (kind == FormationKind.Circle || kind == FormationKind.Vanilla)
                return ResolveAutomaticWidth(kind, count);
            int[] counts = BuildRowCounts(kind, count, rows);
            int width = 1;
            foreach (int size in counts) width = Math.Max(width, size);
            return width;
        }

        private static int[] BuildRowCounts(FormationKind kind, int count, int rows)
        {
            rows = NormalizeRows(rows, count);
            var counts = new int[rows];
            if (kind != FormationKind.Wedge || rows == 1)
            {
                for (int rank = 0; rank < rows; rank++)
                    counts[rank] = count / rows + (rank < count % rows ? 1 : 0);
                return counts;
            }
            // Reserve a single apex and at least one unit in every remaining rank.
            for (int rank = 0; rank < rows; rank++) counts[rank] = 1;
            int remaining = count - rows;
            long weight = (long)(rows - 1) * (rows - 1);
            int allocated = 0;
            for (int rank = 1; rank < rows; rank++)
            {
                int extra = (int)((long)remaining * (2 * rank - 1) / weight);
                counts[rank] += extra;
                allocated += extra;
            }
            for (int rank = rows - 1; allocated < remaining; rank--, allocated++)
                counts[rank]++;
            Array.Sort(counts, 1, rows - 1);
            return counts;
        }

'''+s[end:]
start=s.index('        internal static List<FormationPoint> BuildRelativeSlots('); end=s.index('        internal static int[] AssignSlotsByRole(',start)
chunk=s[start:end].replace('int width,','int rows,').replace('int normalizedWidth = Math.Max(1, Math.Min(count, width));','int normalizedRows = NormalizeRows(rows, count);')
chunk=re.sub(r'            else if \(kind == FormationKind.Wedge\).*?            Center\(result\);','''            else
                BuildRanks(BuildRowCounts(kind, count, normalizedRows), normalizedDensity,
                    forwardX, forwardY, rightX, rightY, result);
            Center(result);''',chunk,flags=re.S)
s=s[:start]+chunk+s[end:]
start=s.index('        private static void BuildRanks(');end=s.index('        private static void Center(',start)
s=s[:start]+'''        private static void BuildRanks(
            int[] rowCounts, int spacing, int forwardX, int forwardY,
            int rightX, int rightY, List<FormationPoint> destination)
        {
            for (int rank = 0; rank < rowCounts.Length; rank++)
            {
                int inRank = rowCounts[rank];
                for (int file = 0; file < inRank; file++)
                {
                    // Quantize the local lattice before rotation, never individual world axes.
                    int lateral = file - (inRank - 1) / 2;
                    int x = (rightX * lateral - forwardX * rank) * spacing;
                    int y = (rightY * lateral - forwardY * rank) * spacing;
                    destination.Add(new FormationPoint(x, y, rank, lateral));
                }
            }
        }

'''+s[end:]
s=re.sub(r'        private static int RoundHalf.*?;\n','',s,count=1,flags=re.S)
idx=s.index('    internal static class FormationModel')
s=s[:idx]+'''    internal sealed class FormationGestureState
    {
        private readonly FormationKind kind;
        private readonly int count;
        private double wheelRemainder;
        private int lastWheelFrame = -1;
        internal FormationGestureState(FormationKind kind, int count, int direction)
        {
            this.kind = kind;
            this.count = count;
            Rows = FormationModel.ResolveAutomaticRows(kind, count);
            Direction = direction & 7;
        }
        internal int Rows { get; private set; }
        internal int Direction { get; private set; }
        internal bool ExplicitDirection { get; private set; }
        internal void ApplyWheel(float delta, int frame)
        {
            if (frame == lastWheelFrame || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            lastWheelFrame = frame;
            if (kind == FormationKind.Circle || kind == FormationKind.Vanilla) return;
            wheelRemainder += delta;
            double steps = Math.Truncate(wheelRemainder);
            wheelRemainder -= steps;
            Rows = (int)Math.Max(1, Math.Min(count, Rows - steps));
        }
        internal void UpdateDirection(int deltaX, int deltaY, int threshold)
        {
            if (Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) < threshold) return;
            Direction = FormationModel.QuantizeDirection(deltaX, deltaY, Direction);
            ExplicitDirection = true;
        }
    }

'''+s[idx:];write(p,s)
p='APIShared/src/UnitCommands/FormationOrderPacket.cs';s=read(p)
s=s.replace('[Key(13)] public ulong PlanHash;','[Key(13)] public ulong PlanHash;\n        [Key(14)] public ushort Rows;').replace('FieldCount = 14','FieldCount = 15').replace('writer.Write(value.PlanHash);','writer.Write(value.PlanHash);\n            writer.Write(value.Rows);').replace('default: reader.Skip();','case 14: packet.Rows = reader.ReadUInt16(); break;\n                    default: reader.Skip();');write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
s=s.replace('ProtocolVersion = 5','ProtocolVersion = 6').replace('FormationModel.ResolveActualRows((FormationKind)packet.Formation, packet.UnitCount, packet.Width)','packet.Rows').replace('FormationModel.ResolveActualRows(previewKey.Kind, previewKey.UnitCount, previewKey.Width)','previewKey.Rows')
s=s.replace('int changedDensity = 0;','int changedRows = 0;')
start=s.index('                        int newDensity =');end=s.index('\n                    }',start)
s=s[:start]+'''                        int oldRows = state.Rows;
                        state.Geometry.ApplyWheel(wheel, frame);
                        if (state.Rows != oldRows) changedRows = state.Rows;'''+s[end:]
s=s.replace('state.DragDeltaX = endpoint.NativeX - state.Target.NativeX;\n                        state.DragDeltaY = endpoint.NativeY - state.Target.NativeY;','state.Geometry.UpdateDirection(\n                            endpoint.NativeX - state.Target.NativeX,\n                            endpoint.NativeY - state.Target.NativeY, MinimumDragTileDistance);')
s=s.replace('changedDensity','changedRows').replace('FORMATION_DENSITY_CHANGED: density=','FORMATION_ROWS_CHANGED: rows=')
s=s.replace('Width = (ushort)width,','Width = (ushort)width,\n                Rows = checked((ushort)state.Rows),').replace('                        packet.Width,','                        packet.Rows,').replace('                    width,\n                    state.PlacementMode,','                    state.Rows,\n                    state.PlacementMode,')
s=s.replace('                HasExplicitDirection(state));','                HasExplicitDirection(state), state.Rows);')
start=s.index('        private NativeDestination[] BuildManagedDestinations(');end=s.index('        private static FormationDirectionIndicator BuildDirectionIndicator(',start)
chunk=s[start:end].replace('int width,','int rows,').replace('Math.Max(1, width)','rows').replace('Math.Max(256, units.Length * 16)), assassinOnly);','Math.Max(256, units.Length * 16)), assassinOnly, slots);');s=s[:start]+chunk+s[end:]
start=s.index('            int dragDistance =',s.index('        private static void ResolveDirectionAndWidth('));end=s.index('        private static bool TryCaptureSelection(',start)
s=s[:start]+'''            direction = state.Geometry.Direction;
            width = FormationModel.ResolveWidthForRows(state.Kind, state.Selection.Length, state.Rows);
        }

        private static bool HasExplicitDirection(ActiveDrag state) => state.Geometry.ExplicitDirection;

'''+s[end:]
s=s.replace('            else if (packet.PlanHash == 0UL)','''            else if (packet.Rows == 0 || packet.Rows > packet.UnitCount ||
                     (((FormationKind)packet.Formation == FormationKind.Circle ||
                       (FormationKind)packet.Formation == FormationKind.Vanilla) &&
                      packet.Rows != FormationModel.ResolveAutomaticRows((FormationKind)packet.Formation, packet.UnitCount)))
                rejection = "invalid rows";
            else if (packet.Width != FormationModel.ResolveWidthForRows(
                         (FormationKind)packet.Formation, packet.UnitCount, packet.Rows))
                rejection = "width does not match rows";
            else if (packet.PlanHash == 0UL)''')
s=s.replace('                    0);\n            }\n\n            internal int CommandButton','                    0);\n                Geometry = new FormationGestureState(kind, selection.Length, DefaultDirectionSector);\n            }\n\n            internal int CommandButton')
s=s.replace('            internal int DragDeltaX { get; set; }\n            internal int DragDeltaY { get; set; }','            internal FormationGestureState Geometry { get; }\n            internal int Rows => Geometry.Rows;')
# Reserve all reachable exact targets before nearest-place fallback.
start=s.index('            for (int slotIndex = 0;',s.index('        private static NativeDestination[] SnapSlots('))
s=s[:start]+'''            var exact = new Dictionary<long, int>();
            for (int index = 0; index < candidates.Count; index++)
                exact[((long)candidates[index].X << 32) | (uint)candidates[index].Y] = index;
            var assigned = new bool[slots.Count];
            for (int index = 0; index < slots.Count; index++)
            {
                long key = ((long)(anchorX + slots[index].X) << 32) | (uint)(anchorY + slots[index].Y);
                if (exact.TryGetValue(key, out int candidate) && !used[candidate])
                {
                    used[candidate] = true;
                    assigned[index] = true;
                    result[index] = candidates[candidate];
                }
            }
'''+s[start:]
s=s.replace('                FormationPoint slot = slots[slotIndex];\n                int desiredX','                if (assigned[slotIndex]) continue;\n                FormationPoint slot = slots[slotIndex];\n                int desiredX')
s=s.replace('            bool assassinOnly)\n        {\n            GameTileManagerView tileManager', '            bool assassinOnly, IReadOnlyList<FormationPoint> desiredSlots)\n        {\n            GameTileManagerView tileManager')
needle='            visited[anchorTile] = true;\n            queue.Enqueue'
s=replace(s,needle,'''            var pending = new HashSet<int>();
            foreach (FormationPoint slot in desiredSlots)
            {
                int x = anchorX + slot.X, y = anchorY + slot.Y;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth ||
                    movementTargetAvailability[y * MapWidth + x] == 0) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                if ((uint)tile < (uint)capacity && (uint)tile < (uint)edges.Length &&
                    components[tile] == component && (!assassinOnly ||
                    ((uint)tile < (uint)logic.Length && (logic[tile] & 0x10000100) == 0)))
                    pending.Add(tile);
            }
            visited[anchorTile] = true;
            queue.Enqueue''')
s=s.replace('while (queue.Count != 0 && result.Count < requestedCount)','while (queue.Count != 0 && (result.Count < requestedCount || pending.Count != 0))')
s=s.replace('                if (available && assassinAllowed)\n                    result.Add(current);','''                bool desired = pending.Remove(current.TileId);
                if (available && assassinAllowed && (result.Count < requestedCount || desired))
                    result.Add(current);''')
write(p,s)
p='BugfixesAndQoL/src/FormationMenuViewModel.cs';s=read(p)
names=['Vanilla','Block','Line','Column','Wedge','Circle']
needle='        public SolidColorBrush VanillaBackground'
idx=s.index(needle)
s=s[:idx]+''.join(f'        public bool Is{n} => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.{n};\n' for n in names)+s[idx:]
s=s.replace('            OnChanged(nameof(VanillaBackground));',''.join(f'            OnChanged(nameof(Is{n}));\n' for n in names)+'            OnChanged(nameof(VanillaBackground));');write(p,s)
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
templates=[]
for n in names:
    pattern=r'(<Button[^>]*CommandParameter="'+n+r'">\s*)(<Canvas.*?</Canvas>)'
    match=re.search(pattern,s,re.S);assert match,n
    templates.append(f'      <DataTemplate x:Key="BugfixesFormationIcon{n}">{match[2]}</DataTemplate>')
    s=s[:match.start(2)]+f'<ContentControl Width="32" Height="32" IsHitTestVisible="False" ContentTemplate="{{StaticResource BugfixesFormationIcon{n}}}"/>'+s[match.end(2):]
idx=s.index('  <Operation Type="Add" XPath="//n:Grid[@x:Name=\'LayoutRoot\']">')
resource='  <Operation Type="Add" XPath="//n:UserControl.Resources">\n    <Content><ResourceDictionary>\n'+'\n'.join(templates)+'\n    </ResourceDictionary></Content>\n  </Operation>\n'
# UserControl.Resources already holds a ResourceDictionary, target its dictionary directly.
resource=resource.replace('XPath="//n:UserControl.Resources"','XPath="//n:UserControl.Resources/n:ResourceDictionary"')
# Add each keyed DataTemplate separately: nested resource dictionary would not expose keys.
resource=''.join('  <Operation Type="Add" XPath="//n:UserControl.Resources/n:ResourceDictionary">\n    <Content>'+t.strip()+'</Content>\n  </Operation>\n' for t in templates)
s=s[:idx]+resource+s[idx:]
start=s.index('          <Grid Width="27"');end=s.index('</Grid>',start)+len('</Grid>')
s=s[:start]+'''          <Viewbox Width="27" Height="27"><Grid Width="32" Height="32" IsHitTestVisible="False">
'''+''.join(f'            <ContentControl ContentTemplate="{{StaticResource BugfixesFormationIcon{n}}}" Visibility="{{Binding Is{n}, Converter={{StaticResource booleanToVisibilityConverter}}}}"/>\n' for n in names)+'''          </Grid></Viewbox>'''+s[end:];write(p,s)
