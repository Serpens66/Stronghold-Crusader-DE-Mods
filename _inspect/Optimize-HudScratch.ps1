$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'APIShared/src/UnitHudPresentationCapability.cs'
$source=[IO.File]::ReadAllText($path)
function Replace-Exact([string]$old,[string]$new) {
 if(-not $script:source.Contains($old)) {throw "Missing anchor $old"}
 $script:source=$script:source.Replace($old,$new)
}
function Wrap-Method([string]$first,[string]$next) {
 $a=$script:source.IndexOf($first); $b=$script:source.IndexOf($next,$a)
 $part=$script:source.Substring($a,$b-$a); $open=$part.IndexOf("        {`r`n")+11; $close=$part.LastIndexOf("        }`r`n")
 $body=$part.Substring($open,$close-$open).TrimEnd([char]13,[char]10)
 $body=($body -split "`r`n" | ForEach-Object {if($_){'    '+$_}else{''}}) -join "`r`n"
 $part=$part.Substring(0,$open)+"            HudWorkBuffers buffers = RentHudBuffers();`r`n            try`r`n            {`r`n$body`r`n            }`r`n            finally`r`n            {`r`n                buffers.Clear();`r`n                lock (sync) hudBufferPool.Push(buffers);`r`n            }`r`n        }`r`n`r`n"
 $script:source=$script:source.Substring(0,$a)+$part+$script:source.Substring($b)
}
Replace-Exact '        private readonly object sync = new object();' "        private readonly object sync = new object();`r`n        private readonly Stack<HudWorkBuffers> hudBufferPool = new Stack<HudWorkBuffers>();`r`n        private static readonly UnitHudImageSlot[] ImageSlots = (UnitHudImageSlot[])Enum.GetValues(typeof(UnitHudImageSlot));"
Replace-Exact 'foreach (UnitHudImageSlot slot in Enum.GetValues(typeof(UnitHudImageSlot)))' 'foreach (UnitHudImageSlot slot in ImageSlots)'
Replace-Exact "                foreach (ImageRegistration registration in registrations.Where(x => x.Definition.Slot == slot))`r`n                {" "                foreach (ImageRegistration registration in registrations)`r`n                {`r`n                    if (registration.Definition.Slot != slot) continue;"
Replace-Exact '            bool selectionComplete = TryCaptureSelectedUnits(out List<UnitHudUnitSnapshot> selected);' "            List<UnitHudUnitSnapshot> selected = buffers.Selected;`r`n            bool selectionComplete = TryCaptureSelectedUnits(selected, buffers.Seen);"
Replace-Exact 'BuildEntries(selected, vanillaCounts, selectionComplete, UnitHudSurface.TroopSelection);' 'BuildEntries(selected, vanillaCounts, selectionComplete, UnitHudSurface.TroopSelection, buffers);'
Replace-Exact '            var snapshots = new List<UnitHudSlotSnapshot>();' '            List<UnitHudSlotSnapshot> snapshots = buffers.Slots;'
Wrap-Method '        private void RenderTroopCategories(' '        private static void ApplyTint('
Replace-Exact "            bool selectionComplete,`r`n            UnitHudSurface surface)" "            bool selectionComplete,`r`n            UnitHudSurface surface,`r`n            HudWorkBuffers buffers)"
Replace-Exact "            var claimed = new Dictionary<string, List<UnitHudUnitSnapshot>>(StringComparer.Ordinal);`r`n            var claimedIds = new HashSet<int>();`r`n            var reduction = new int[vanillaCounts.Length];" "            Dictionary<string, List<UnitHudUnitSnapshot>> claimed = buffers.Grouped;`r`n            HashSet<int> claimedIds = buffers.ClaimedIds;`r`n            if (buffers.Reduction.Length != vanillaCounts.Length) buffers.Reduction = new int[vanillaCounts.Length];`r`n            int[] reduction = buffers.Reduction;"
Replace-Exact 'claimed[category.Key] = list = new List<UnitHudUnitSnapshot>();' 'claimed[category.Key] = list = buffers.RentGroup();'
Replace-Exact "                claimedIds,`r`n                selectionComplete);`r`n            var result = new List<DisplayEntry>();" "                claimedIds,`r`n                selectionComplete,`r`n                buffers.EffectiveCounts);`r`n            buffers.EffectiveCounts = effectiveCounts;`r`n            List<DisplayEntry> result = buffers.Entries;"
Replace-Exact "                foreach (CategoryRegistration category in registrations.Where(x => x.Definition.BaseUnitType == type && HasSurface(x, surface)))`r`n                    if (claimed.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> units) && units.Count > 0) result.Add(new DisplayEntry(category, units));" "                foreach (CategoryRegistration category in registrations)`r`n                    if (category.Definition.BaseUnitType == type && HasSurface(category, surface) &&`r`n                        claimed.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> units) && units.Count > 0) result.Add(new DisplayEntry(category, units));"
Replace-Exact "            result = new List<UnitHudUnitSnapshot>();`r`n            EngineInterface.PlayState state" "            result = new List<UnitHudUnitSnapshot>();`r`n            return TryCaptureSelectedUnits(result, new HashSet<int>());`r`n        }`r`n`r`n        private static bool TryCaptureSelectedUnits(List<UnitHudUnitSnapshot> result, HashSet<int> seen)`r`n        {`r`n            result.Clear();`r`n            seen.Clear();`r`n            EngineInterface.PlayState state"
Replace-Exact "            int count = state.numSelectedChimps;`r`n            var seen = new HashSet<int>();" '            int count = state.numSelectedChimps;'
Replace-Exact "            var grouped = new Dictionary<string, List<UnitHudUnitSnapshot>>(StringComparer.Ordinal);`r`n            foreach (UnitHudUnitSnapshot unit in CaptureSelectedUnits())" "            Dictionary<string, List<UnitHudUnitSnapshot>> grouped = buffers.Grouped;`r`n            TryCaptureSelectedUnits(buffers.Selected, buffers.Seen);`r`n            foreach (UnitHudUnitSnapshot unit in buffers.Selected)"
Replace-Exact 'grouped[category.Key] = items = new List<UnitHudUnitSnapshot>();' 'grouped[category.Key] = items = buffers.RentGroup();'
Replace-Exact '            return CategoryCopy().Where(x => grouped.ContainsKey(x.Key)).Select(x => Snapshot(x, grouped[x.Key])).ToArray();' "            foreach (CategoryRegistration category in CategoryCopy())`r`n                if (grouped.TryGetValue(category.Key, out List<UnitHudUnitSnapshot> units))`r`n                    buffers.Categories.Add(Snapshot(category, units));`r`n            return buffers.Categories.ToArray();"
Wrap-Method '        private IReadOnlyList<UnitHudCategorySnapshot> CaptureSelectedCategories()' '        private IReadOnlyList<UnitHudControlGroupSnapshot> CaptureControlGroups()'
Replace-Exact "            bool selectionComplete)`r`n        {`r`n            if (vanillaCounts == null)" "            bool selectionComplete,`r`n            int[] reusableCounts = null)`r`n        {`r`n            if (vanillaCounts == null)"
Replace-Exact '            var result = (int[])vanillaCounts.Clone();' "            int[] result = reusableCounts != null && reusableCounts.Length == vanillaCounts.Length`r`n                ? reusableCounts : new int[vanillaCounts.Length];`r`n            Array.Copy(vanillaCounts, result, vanillaCounts.Length);"
$helper=@'
        private HudWorkBuffers RentHudBuffers()
        {
            lock (sync) return hudBufferPool.Count == 0 ? new HudWorkBuffers() : hudBufferPool.Pop();
        }

        // A callback may reenter the service. Never share a live workspace between calls,
        // and copy public snapshots before clearing these internal lists.
        private sealed class HudWorkBuffers
        {
            internal readonly List<UnitHudUnitSnapshot> Selected = new List<UnitHudUnitSnapshot>();
            internal readonly HashSet<int> Seen = new HashSet<int>();
            internal readonly HashSet<int> ClaimedIds = new HashSet<int>();
            internal readonly Dictionary<string, List<UnitHudUnitSnapshot>> Grouped =
                new Dictionary<string, List<UnitHudUnitSnapshot>>(StringComparer.Ordinal);
            internal readonly List<DisplayEntry> Entries = new List<DisplayEntry>();
            internal readonly List<UnitHudSlotSnapshot> Slots = new List<UnitHudSlotSnapshot>();
            internal readonly List<UnitHudCategorySnapshot> Categories = new List<UnitHudCategorySnapshot>();
            internal int[] Reduction = Array.Empty<int>();
            internal int[] EffectiveCounts = Array.Empty<int>();
            private readonly Stack<List<UnitHudUnitSnapshot>> groupPool = new Stack<List<UnitHudUnitSnapshot>>();

            internal List<UnitHudUnitSnapshot> RentGroup() =>
                groupPool.Count == 0 ? new List<UnitHudUnitSnapshot>() : groupPool.Pop();

            internal void Clear()
            {
                foreach (List<UnitHudUnitSnapshot> group in Grouped.Values)
                {
                    group.Clear();
                    groupPool.Push(group);
                }
                Grouped.Clear();
                Selected.Clear();
                Seen.Clear();
                ClaimedIds.Clear();
                Entries.Clear();
                Slots.Clear();
                Categories.Clear();
                Array.Clear(Reduction, 0, Reduction.Length);
                Array.Clear(EffectiveCounts, 0, EffectiveCounts.Length);
            }
        }

'@
$helper=[regex]::Replace($helper,'\r?\n',"`r`n")+"`r`n"
Replace-Exact '        private sealed class CategoryRegistration' ($helper+'        private sealed class CategoryRegistration')
[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
if([IO.File]::ReadAllText($path) -cne $source -or [regex]::Matches($source,'(?<!\r)\n').Count) {throw 'Write verification'}
"HUD CRLF=$([regex]::Matches($source,"`r`n").Count) bareLF=0"
