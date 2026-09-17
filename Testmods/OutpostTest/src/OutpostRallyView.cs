using CrusaderDE;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using UnityEngine;

namespace OutpostTest
{
    // Input is event-driven. Rendering only maintains one non-interactive world marker.
    internal sealed class OutpostRallyView
    {
        private readonly OutpostRuntime runtime;
        private readonly OutpostPresentationState state=new OutpostPresentationState();
        private GameObject flag;
        private Transform flagTransform;
        private Mesh mesh;
        private Material material;
        private GameMap map;
        private MainControls controls;
        private int diagnosticClicks;
        internal OutpostRallyView(OutpostRuntime runtime) { this.runtime=runtime; }
        private static bool HudClear()
        {
            if(!MainViewModel.viewModelLoaded) return false;
            var vm=MainViewModel.Instance;
            return vm!=null && vm.Show_HUD_Building && !vm.Show_BlackOut && !vm.Show_HUD_Briefing &&
                !vm.Show_HUD_IngameMenu && !vm.Show_HUD_FrontEndBlackout && !vm.Show_HUD_MissionOver;
        }
        private bool ResolveReferences()
        {
            if(map==null) map=GameMap.instance;
            if(controls==null) controls=MainControls.instance;
            return map!=null && controls!=null;
        }
        internal void Input(UnityInputEventArgs args)
        {
            if(args.Phase!=EventHookPhase.Pre || args.Key!=KeyCode.Mouse2 || !args.Result ||
                !state.Click(Time.frameCount,UnityEngine.Input.GetMouseButtonDown(2))) return;
            string reason="hud-or-offworld";
            bool accepted=false;
            if(HudClear() && ResolveReferences() && !controls.overGUI && !controls.isOffWorld()) {
                reason="selection";
                if(runtime.TrySelected(out int id,out uint global,out int owner,out int type)) {
                    // Resolve the click now, on the Unity thread. Never access Unity from OnTick.
                    var screen=UnityEngine.Input.mousePosition;
                    float previousHeight=map.lastMouseLandscapeHeight;bool previousHalf=map.overTopHalf;
                    Vector3 world=Vector3.zero;Vector3Int cell=new Vector3Int(-1,-1,0);int depth=0;
                    try { map.CalcMapTileFromMousePos(screen,ref world,ref cell,ref depth,false,true); }
                    finally { map.lastMouseLandscapeHeight=previousHeight;map.overTopHalf=previousHalf; }
                    var tile=map.getMapTile(cell.x,cell.y);
                    reason="tile-or-identity";
                    if(tile!=null) accepted=runtime.SetRally(id,global,owner,type,tile.gameMapX,tile.gameMapY);
                }
            }
            if(accepted) args.Result=false;
            // First clicks only: explain rejection without periodic logging.
            if(diagnosticClicks++<8) runtime.InputDiagnostic(accepted?"accepted":reason);
        }
        internal void Present(bool hasPoint,int targetX,int targetY)
        {
            if(!hasPoint || !HudClear() || !ResolveReferences()) { Hide();return; }
            map.mapGameTileToTilemapCoord(targetX,targetY,out int x,out int y);
            var tile=map.getMapTile(x,y);
            if(tile==null) { Hide();return; }
            EnsureFlag();
            Vector3 position=controls.getCellCentre(x,y);
            position.y+=tile.testHeight;position.z=200f+position.y;
            if(state.Position(position.x,position.y,position.z)) flagTransform.position=position;
            if(state.Visibility(true)) flag.SetActive(true);
        }
        private void EnsureFlag()
        {
            if(flag!=null) return;
            flag=new GameObject("OutpostTest Rallypoint");flag.SetActive(false);
            flagTransform=flag.transform;state.Visibility(false);state.ForgetPosition();
            Object.DontDestroyOnLoad(flag);
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
        internal void Hide() { if(state.Visibility(false) && flag!=null) flag.SetActive(false); }
        // Main-thread only, called after the simulation publishes a revision change.
        internal void Reset() { Hide();map=null;controls=null;state.ForgetPosition();diagnosticClicks=0; }
    }
}