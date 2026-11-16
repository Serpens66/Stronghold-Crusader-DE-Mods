using System;
using System.Reflection;
using SHCDESE.API;

namespace TrappedUnitRescueTest
{
    // SE's existing managed brush wrapper belongs to an internal type. Resolve the exact
    // installed signature rather than using a publicized assembly or bypassing its native hooks.
    internal static class EditorBrush
    {
        private delegate long Brush(IntPtr manager, int issuer, int packedTile, int y,
            int width, int operation, int variant, byte auxiliary);
        private static readonly Brush invoke = Resolve();
        private static Brush Resolve()
        {
            Type owner = typeof(GameTileManagerAPI).Assembly.GetType("SHCDESE.Detours.BulkMapEditorDetours", true);
            MethodInfo method = owner.GetMethod("c_game_editor_brush_set_tiletype_hook_impl",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(IntPtr), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(byte) }, null);
            if (method == null || method.ReturnType != typeof(long))
                throw new MissingMethodException(owner.FullName, "c_game_editor_brush_set_tiletype_hook_impl");
            return (Brush)Delegate.CreateDelegate(typeof(Brush), method);
        }
        internal static long Call(IntPtr manager, int issuer, int tile, int y, int width, int operation, int variant, byte auxiliary)
            => invoke(manager, issuer, tile, y, width, operation, variant, auxiliary);
    }
}
