using System.Collections.Generic;

namespace AreaDemolitionTest
{
    internal enum PointerVisualRole { Ordinary, Overlay, Decoration }
    internal enum PointerHitDisposition { Continue, SkipSelf, SkipSubtree }

    // Vanilla's visual-level hit test differs from routed-input IsHitTestVisible.
    // Only our decorative visuals are removed; remaining hits keep Vanilla's tag contract.
    internal static class PointerUiPolicy
    {
        internal static PointerHitDisposition Filter(PointerVisualRole role) =>
            role==PointerVisualRole.Decoration ? PointerHitDisposition.SkipSubtree :
            role==PointerVisualRole.Overlay ? PointerHitDisposition.SkipSelf : PointerHitDisposition.Continue;

        internal static bool IsBlocking(IReadOnlyList<string> tags,bool editorMenuMode)
        {
            if(tags==null)return true;
            for(int i=0;i<tags.Count;i++)
            {
                string tag=tags[i];
                if(tag=="Ignore" || tag=="IgnoreSelf" && i==0 ||
                    tag=="IgnoreNotME" && !editorMenuMode || tag=="IgnoreME" && editorMenuMode)return false;
            }
            return true;
        }
    }
}
