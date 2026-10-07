using System;
namespace EnemyBridgePathTest
{
    internal static class VirtualPlanningBuildings
    {
        // Raw native manager records start at game-ID 1, stride 32C (not the
        // public interop header). C4BF0 selects live types45..47, D8CE0 closes
        // the two cardinal apertures. Forced state2 overrides height/seed tests.
        internal static bool TryPrepare(VirtualBridgeMap map,byte[] records,ushort[] buildingIds,out byte[] closedEdges,out ushort[] types,out byte[] blocks,out string reason)
        {
            closedEdges=(byte[])map.Edges.Clone();types=new ushort[map.Components.Length];blocks=new byte[types.Length];reason="complete";
            if(records.Length%0x32c!=0||buildingIds.Length!=types.Length){reason="building-capacity";return false;}
            for(int tile=0;tile<types.Length;tile++)
            {
                int id=buildingIds[tile];if(id==0)continue;int at=(id-1)*0x32c;
                if(at<0||at+0x32c>records.Length){reason="unresolved-building-id";return false;}
                types[tile]=BitConverter.ToUInt16(records,at+0x12e);blocks[tile]=records[at+0x300];
            }
            for(int at=0;at<records.Length;at+=0x32c)
            {
                int kind=BitConverter.ToUInt16(records,at+0x12e);
                if(BitConverter.ToInt16(records,at+0x12c)==0||kind<45||kind>47)continue;
                int x=BitConverter.ToInt16(records,at+0x14a),y=BitConverter.ToInt16(records,at+0x14c),size=BitConverter.ToInt32(records,at+0x154);
                if(size<1||size>800){reason="unresolved-gate-size";return false;}
                int a,b,c,d;
                if(BitConverter.ToInt16(records,at+0x15e)==80)
                {a=map.Tile(x-1,y+size/2);b=map.Tile(x,y+size/2);c=map.Tile(x+size,y+size/2);d=map.Tile(x+size-1,y+size/2);}
                else
                {a=map.Tile(x+size/2,y-1);b=map.Tile(x+size/2,y);c=map.Tile(x+size/2,y+size);d=map.Tile(x+size/2,y+size-1);}
                if(a<0||b<0||c<0||d<0){reason="unresolved-gate-aperture";return false;}
                bool east=BitConverter.ToInt16(records,at+0x15e)==80;
                closedEdges[a]&=(byte)(east?0xfb:0xef);closedEdges[b]&=(byte)(east?0xbf:0xfe);
                closedEdges[c]&=(byte)(east?0xbf:0xfe);closedEdges[d]&=(byte)(east?0xfb:0xef);
            }
            return true;
        }
    }
}
