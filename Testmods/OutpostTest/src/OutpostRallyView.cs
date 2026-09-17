using CrusaderDE;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using UnityEngine;

namespace OutpostTest
{
    // All input is event-driven; LateUpdate only resolves queued input and presents the marker.
    internal sealed class OutpostRallyView
    {
        private readonly OutpostRuntime runtime;
        private GameObject flag;
        private Mesh mesh;
        private Material material;
        private Click pending;
        private int lastInputFrame=-1;
        private sealed class Click
        {
            internal int Id,Owner,Type,Frame;
            internal uint Global;
            internal Vector3 Screen;
        }
        internal OutpostRallyView(OutpostRuntime runtime) { this.runtime=runtime; }
        private static bool HudClear()
        {
            var vm=MainViewModel.Instance;
            return vm!=null && vm.Show_HUD_Main && !vm.Show_BlackOut && !vm.Show_HUD_Briefing &&
                !vm.Show_HUD_IngameMenu && !vm.Show_HUD_FrontEndBlackout && !vm.Show_HUD_MissionOver;
        }
        internal void Input(UnityInputEventArgs args)
        {
            if(args.Phase!=EventHookPhase.Pre || args.Key!=KeyCode.Mouse2 || !args.Result || !UnityEngine.Input.GetMouseButtonDown(2) || Time.frameCount==lastInputFrame) return;
            lastInputFrame=Time.frameCount;
            if(!HudClear() || MainControls.instance==null || MainControls.instance.overGUI || MainControls.instance.isOffWorld()) return;
            if(!runtime.TrySelected(out int id,out uint global,out int owner,out int type)) return;
            pending=new Click { Id=id,Global=global,Owner=owner,Type=type,Screen=UnityEngine.Input.mousePosition,Frame=Time.frameCount };
            args.Result=false;
        }
        internal void AcceptInput()
        {
            var c=pending;pending=null;
            if(c==null || !HudClear() || MainControls.instance==null || MainControls.instance.overGUI ||
                MainControls.instance.isOffWorld() || GameMap.instance==null || Camera.main==null) return;
            // Use the captured screen coordinate, not a later mouse position. Avoid leaking the
            // projection helper's incidental hover state into Vanilla's input handling.
            var map=GameMap.instance;
            float previousHeight=map.lastMouseLandscapeHeight;bool previousHalf=map.overTopHalf;
            Vector3 world=Vector3.zero;Vector3Int cell=new Vector3Int(-1,-1,0);int depth=0;
            try { map.CalcMapTileFromMousePos(c.Screen,ref world,ref cell,ref depth,false,true); }
            finally { map.lastMouseLandscapeHeight=previousHeight;map.overTopHalf=previousHalf; }
            var tile=map.getMapTile(cell.x,cell.y);
            if(tile!=null) runtime.SetRally(c.Id,c.Global,c.Owner,c.Type,tile.gameMapX,tile.gameMapY);
        }
        internal void Present(OutpostRallyState.Record selected)
        {
            if(selected==null || !HudClear() || GameMap.instance==null || MainControls.instance==null)
            { Hide();return; }
            var map=GameMap.instance;
            map.mapGameTileToTilemapCoord(selected.X,selected.Y,out int x,out int y);
            var tile=map.getMapTile(x,y);
            if(tile==null) { Hide();return; }
            EnsureFlag();
            Vector3 position=MainControls.instance.getCellCentre(x,y);
            position.y+=tile.testHeight;
            position.z=200f+position.y;
            flag.transform.position=position;
            flag.SetActive(true);
        }
        private void EnsureFlag()
        {
            if(flag!=null) return;
            flag=new GameObject("OutpostTest Rallypoint");
            Object.DontDestroyOnLoad(flag);
            // Vector pennant and mast: no colliders, HUD hit targets or native sprite slots.
            mesh=new Mesh { name="OutpostTest rally flag mesh" };
            mesh.vertices=new[] {
                new Vector3(-.022f,0,0),new Vector3(.022f,0,0),new Vector3(.022f,.8f,0),new Vector3(-.022f,.8f,0),
                new Vector3(.022f,.77f,0),new Vector3(.40f,.60f,0),new Vector3(.022f,.43f,0) };
            mesh.triangles=new[] {0,1,2,0,2,3,4,5,6};
            mesh.colors=new[] {Color.white,Color.white,Color.white,Color.white,Color.yellow,Color.yellow,Color.yellow};
            mesh.uv=new[] {Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero};
            mesh.RecalculateBounds();
            material=new Material(Shader.Find("Sprites/Default")) { name="OutpostTest rally flag material" };
            material.mainTexture=Texture2D.whiteTexture;
            flag.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=flag.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=32760;
        }
        private void Hide() { if(flag!=null) flag.SetActive(false); }
        internal void Reset() { pending=null;Hide(); }
    }
}
