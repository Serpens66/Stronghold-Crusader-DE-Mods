using System;
using System.Collections.Generic;

namespace AreaDemolitionTest
{
    // Only preview membership changes here; removal never borrows a stale parent closure.
    internal sealed class PreviewRootSelection
    {
        private ScreenHitGeometry geometry;
        private PreviewRootResult[] roots=Array.Empty<PreviewRootResult>();
        private bool[] selected=Array.Empty<bool>();
        internal DemolitionTarget[] Targets { get; private set; }=Array.Empty<DemolitionTarget>();
        internal void Replace(ScreenHitGeometry hitGeometry,IEnumerable<PreviewRootResult> approvedRoots)
        {
            geometry=hitGeometry;roots=new List<PreviewRootResult>(approvedRoots).ToArray();
            selected=new bool[roots.Length];Targets=Array.Empty<DemolitionTarget>();
        }
        internal bool Refresh(ScreenRect area)
        {
            if(geometry==null)return false;
            bool changed=false;
            for(int i=0;i<roots.Length;i++)
            {
                bool hit=geometry.Intersects(roots[i].Root,area);
                if(hit!=selected[i]){selected[i]=hit;changed=true;}
            }
            if(!changed)return false;
            var union=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            for(int i=0;i<roots.Length;i++)if(selected[i])
                foreach(var target in roots[i].Closure)union[target.Identity]=target;
            var values=new DemolitionTarget[union.Count];union.Values.CopyTo(values,0);
            Array.Sort(values,Compare);Targets=values;return true;
        }
        private static int Compare(DemolitionTarget a,DemolitionTarget b)
        {
            int value=((int)a.Kind).CompareTo((int)b.Kind);if(value!=0)return value;
            value=a.GlobalId.CompareTo(b.GlobalId);if(value!=0)return value;
            value=a.TileId.CompareTo(b.TileId);if(value!=0)return value;
            value=a.Slot.CompareTo(b.Slot);return value!=0?value:a.TaskIndex.CompareTo(b.TaskIndex);
        }
        internal void Clear(){geometry=null;roots=Array.Empty<PreviewRootResult>();selected=Array.Empty<bool>();Targets=Array.Empty<DemolitionTarget>();}
    }
}
