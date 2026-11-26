using System;
using System.Collections.Generic;
using System.Reflection;
using SHCDESE.API;
using SHCDESE.Interop.Enums;
using APIShared.WorldMutations;

namespace TrappedUnitRescueTest
{
    internal enum EditorAcceptanceWriter { Type,Clear,Preset,Raise,Number,Even,Delete }
    // Existing SE wrappers own Original+mirror/broker calls. No new native entry,
    // publicized assembly, synthetic surface mutation or unsafe direct delegate.
    internal static class EditorAcceptanceBrushes
    {
        private delegate long Four(IntPtr manager,int center,int y,int width);
        private delegate long Five(IntPtr manager,int center,int y,int width,int value);
        private delegate long Number(IntPtr manager,int center,int y,int width,short mode);
        private static readonly Type Owner=typeof(GameTileManagerAPI).Assembly.GetType("SHCDESE.Detours.BulkMapEditorDetours",true);
        private static readonly Four clear=Resolve<Four>("c_game_editor_brush_set_tiletype_to_none_hook_impl",typeof(int));
        private static readonly Five preset=Resolve<Five>("c_game_editor_brush_set_height_to_preset_hook_impl",typeof(int),typeof(int));
        private static readonly Five raise=Resolve<Five>("c_game_editor_brush_raise_tileheight_hook_impl",typeof(int),typeof(int));
        private static readonly Number number=Resolve<Number>("c_game_editor_brush_set_height_to_number_hook_impl",typeof(int),typeof(short));
        private static readonly Four even=Resolve<Four>("c_game_editor_brush_even_height_hook_impl",typeof(int));
        private static readonly Four delete=Resolve<Four>("c_game_editor_brush_delete_hook_impl",typeof(int));
        private static T Resolve<T>(string name,params Type[] tail) where T:class
        {
            var args=new List<Type>{typeof(IntPtr),typeof(int),typeof(int)};args.AddRange(tail);
            var method=Owner.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,args.ToArray(),null);
            if(method==null || method.ReturnType!=typeof(long))throw new MissingMethodException(Owner.FullName,name);
            return Delegate.CreateDelegate(typeof(T),method) as T ?? throw new InvalidOperationException("Editor wrapper delegate differs: "+name);
        }
        internal static WorldMutationSource Source(EditorAcceptanceWriter kind)
        {
            switch(kind)
            {
                case EditorAcceptanceWriter.Type:return WorldMutationSource.EditorTileType;
                case EditorAcceptanceWriter.Clear:return WorldMutationSource.EditorTileTypeClear;
                case EditorAcceptanceWriter.Preset:return WorldMutationSource.EditorHeightPreset;
                case EditorAcceptanceWriter.Raise:return WorldMutationSource.EditorHeightRaise;
                case EditorAcceptanceWriter.Number:return WorldMutationSource.EditorHeightNumber;
                case EditorAcceptanceWriter.Even:return WorldMutationSource.EditorHeightEven;
                case EditorAcceptanceWriter.Delete:return WorldMutationSource.EditorDelete;
                default:throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        internal static long Call(EditorAcceptanceWriter kind,int issuer,int x,int y,int width,int value=0)
        {
            var tiles=GameTileManagerAPI.Instance;var manager=tiles.GetTileManager();int tile=tiles.GetTileId(x,y);
            switch(kind)
            {
                case EditorAcceptanceWriter.Type:return EditorBrush.Call(manager,issuer,tile,y,width,unchecked((int)(uint)TilePropertyFlag.Sea),0,0);
                case EditorAcceptanceWriter.Clear:return clear(manager,tile,y,width);
                case EditorAcceptanceWriter.Preset:return preset(manager,tile,y,width,value);
                case EditorAcceptanceWriter.Raise:return raise(manager,tile,y,width,value);
                case EditorAcceptanceWriter.Number:return number(manager,tile,y,width,checked((short)value));
                case EditorAcceptanceWriter.Even:return even(manager,tile,y,width);
                case EditorAcceptanceWriter.Delete:return delete(manager,x,y,width);
                default:throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
