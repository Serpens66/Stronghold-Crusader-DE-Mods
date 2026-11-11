using System;
using System.Collections.Generic;

namespace AreaDemolitionTest
{
    // Only preview membership changes here; removal never borrows a stale parent closure.
    internal sealed class PreviewRootSelection
    {
        private ScreenHitGeometry geometry;
        private readonly List<PreviewRootResult> roots=new List<PreviewRootResult>();
        private readonly List<bool> selected=new List<bool>();
        private readonly Dictionary<string,int> indices=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Stack<int> free=new Stack<int>();
        private int processedPrefix,approvedPrefix;
        private bool dirty;
        internal DemolitionTarget[] Targets { get; private set; }=Array.Empty<DemolitionTarget>();
        internal void Replace(ScreenHitGeometry hitGeometry,IEnumerable<PreviewRootResult> approvedRoots)
        {
            Clear();geometry=hitGeometry;
            foreach(var root in approvedRoots)Put(root);
        }
        // Retain still-present roots from the preceding advisory quote while their fresh
        // checks are pending. Each processed rejection revokes its old closure immediately.
        // This avoids clearing/repainting the entire preview for every periodic quote.
        internal void UpdateProgress(ScreenHitGeometry hitGeometry,IReadOnlyList<DemolitionTarget> orderedRoots,int processedCount,IReadOnlyList<PreviewRootResult> approvedRoots)
        {
            if(!ReferenceEquals(geometry,hitGeometry))
            {
                geometry=hitGeometry;processedPrefix=approvedPrefix=0;
                for(int i=0;i<roots.Count;i++)if(roots[i]!=null && !geometry.ContainsRoot(roots[i].Root))Remove(i);
            }
            while(processedPrefix<processedCount)
            {
                var expected=orderedRoots[processedPrefix++];
                if(approvedPrefix<approvedRoots.Count && approvedRoots[approvedPrefix].Root.Identity==expected.Identity)
                    Put(approvedRoots[approvedPrefix++]);
                else if(indices.TryGetValue(expected.Identity,out int index))Remove(index);
            }
        }
        private void Put(PreviewRootResult root)
        {
            string id=root.Root.Identity;
            if(indices.TryGetValue(id,out int index)) {roots[index]=root;if(selected[index])dirty=true;return;}
            if(free.Count!=0){index=free.Pop();roots[index]=root;selected[index]=false;}
            else {index=roots.Count;roots.Add(root);selected.Add(false);}
            indices.Add(id,index);
        }
        private void Remove(int index)
        {
            if(selected[index])dirty=true;
            indices.Remove(roots[index].Root.Identity);roots[index]=null;selected[index]=false;free.Push(index);
        }
        internal bool Refresh(ScreenRect area)
        {
            if(geometry==null)return false;
            bool changed=dirty;dirty=false;
            for(int i=0;i<roots.Count;i++)
            {
                bool hit=roots[i]!=null && geometry.Intersects(roots[i].Root,area);
                if(hit!=selected[i]){selected[i]=hit;changed=true;}
            }
            if(!changed)return false;
            var union=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            for(int i=0;i<roots.Count;i++)if(selected[i])
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
        internal void Clear(){geometry=null;roots.Clear();selected.Clear();indices.Clear();free.Clear();processedPrefix=approvedPrefix=0;dirty=false;Targets=Array.Empty<DemolitionTarget>();}
    }
}
