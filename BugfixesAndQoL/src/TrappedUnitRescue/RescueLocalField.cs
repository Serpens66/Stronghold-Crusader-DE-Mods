using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Old geometry only. Lifetime/session/source authority belongs to the owner.
    // tileAt must use the same immutable native XY/packed-ID layout on every call;
    // it must not filter according to today's terrain or unit permissions.
    internal sealed class RescueLocalField
    {
        internal const int Radius=8, Side=17, CellCount=289, OrdinaryBytes=37, EncodedBytes=326;
        private static readonly int[] Dx={0,1,1,1,0,-1,-1,-1},Dy={-1,-1,0,1,1,1,0,-1};
        private readonly byte[] data;
        internal int OriginX { get; }
        internal int OriginY { get; }
        internal int OriginTile { get; }
        internal bool OriginWasOrdinary=>Ordinary(data,Radius*Side+Radius);

        private RescueLocalField(int x,int y,int tile,byte[] owned)
        {OriginX=x;OriginY=y;OriginTile=tile;data=owned;}
        internal byte[] Export()=>(byte[])data.Clone();
        private static bool Ordinary(byte[] bytes,int index)=>(bytes[index>>3]&(1<<(index&7)))!=0;

        internal static bool TryCapture(int originX,int originY,int originTile,Func<int,int,int> tileAt,
            Func<int,bool> ordinaryGround,Func<int,byte> connections,out RescueLocalField field,out string reason)
        {
            field=null;
            if(ordinaryGround==null || connections==null){reason="local-field-reader-missing";return false;}
            if(!TryMap(originX,originY,originTile,tileAt,out var map,out reason))return false;
            var bytes=new byte[EncodedBytes];
            try
            {
                for(int i=0;i<CellCount;i++)
                {
                    if(map[i]<0)continue;
                    if(ordinaryGround(map[i]))bytes[i>>3]|=(byte)(1<<(i&7));
                    bytes[OrdinaryBytes+i]=connections(map[i]);
                }
            }
            catch(Exception){reason="local-field-reader-failed";return false;}
            field=new RescueLocalField(originX,originY,originTile,bytes);reason=null;return true;
        }

        internal static bool TryRestore(int originX,int originY,int originTile,byte[] encoded,
            Func<int,int,int> tileAt,out RescueLocalField field,out string reason)
        {
            field=null;
            if(encoded==null || encoded.Length!=EncodedBytes){reason="local-field-length";return false;}
            // Only bit0 of byte36 belongs to the289 cells. Other bits are canonical0.
            if((encoded[OrdinaryBytes-1]&0xFE)!=0){reason="local-field-unused-bits";return false;}
            if(!TryMap(originX,originY,originTile,tileAt,out var map,out reason) || !CanonicalCells(encoded,map,out reason))return false;
            field=new RescueLocalField(originX,originY,originTile,(byte[])encoded.Clone());reason=null;return true;
        }

        // Save decoding has no current world. Only global machine bounds can be
        // checked here; TryFindCandidates must bind the real diamond/layout later.
        internal static bool TryReadUnbound(int originX,int originY,int originTile,byte[] encoded,
            out RescueLocalField field,out string reason)
        {
            field=null;
            if((uint)originX>=800 || (uint)originY>=800 || (uint)originTile>=NativeRelocation.TileCapacity)
            {reason="local-field-origin";return false;}
            if(encoded==null || encoded.Length!=EncodedBytes){reason="local-field-length";return false;}
            if((encoded[OrdinaryBytes-1]&0xFE)!=0){reason="local-field-unused-bits";return false;}
            for(int iy=0;iy<Side;iy++)for(int ix=0;ix<Side;ix++)
            {
                int i=iy*Side+ix,x=originX+ix-Radius,y=originY+iy-Radius;
                if(((uint)x>=800 || (uint)y>=800) && (Ordinary(encoded,i) || encoded[OrdinaryBytes+i]!=0))
                {reason="local-field-invalid-cell";return false;}
            }
            field=new RescueLocalField(originX,originY,originTile,(byte[])encoded.Clone());reason=null;return true;
        }

        internal bool TryFindCandidates(Func<int,int,int> tileAt,out List<RescueCandidate> candidates,out string reason)
        {
            candidates=new List<RescueCandidate>();
            if(!TryMap(OriginX,OriginY,OriginTile,tileAt,out var map,out reason) || !CanonicalCells(data,map,out reason))return false;
            int origin=Radius*Side+Radius;if(!Ordinary(data,origin)){reason=null;return true;}
            var seen=new bool[CellCount];seen[origin]=true;
            // The result list is also the FIFO: origin is virtual entry0 and each
            // accepted neighbour is appended exactly once in the existing Dx/Dy order.
            int cursor=0;
            while(cursor<=candidates.Count)
            {
                RescueCandidate current=cursor==0?new RescueCandidate{X=OriginX,Y=OriginY,Tile=OriginTile}:candidates[cursor-1];
                int cx=current.X-OriginX+Radius,cy=current.Y-OriginY+Radius,source=cy*Side+cx;
                for(int d=0;d<8;d++)
                {
                    int nx=cx+Dx[d],ny=cy+Dy[d];if((uint)nx>=Side || (uint)ny>=Side)continue;
                    int next=ny*Side+nx;
                    if(map[next]<0 || seen[next] || !Ordinary(data,next) ||
                        ((data[OrdinaryBytes+source]&(1<<d))==0 && (data[OrdinaryBytes+next]&(1<<(d^4)))==0))continue;
                    seen[next]=true;
                    candidates.Add(new RescueCandidate{X=OriginX+nx-Radius,Y=OriginY+ny-Radius,Tile=map[next],Distance=current.Distance+1});
                }
                cursor++;
            }
            reason=null;return true;
        }

        private static bool CanonicalCells(byte[] bytes,int[] map,out string reason)
        {
            for(int i=0;i<CellCount;i++)if(map[i]<0 && (Ordinary(bytes,i) || bytes[OrdinaryBytes+i]!=0))
            {reason="local-field-invalid-cell";return false;}
            reason=null;return true;
        }
        private static bool TryMap(int x,int y,int originTile,Func<int,int,int> tileAt,out int[] map,out string reason)
        {
            map=null;
            //800 axes and320800 packed cells are the audited machine layout,
            // not a rectangle describing the current playable diamond.
            if(tileAt==null || (uint)x>=800 || (uint)y>=800 || (uint)originTile>=NativeRelocation.TileCapacity)
            {reason="local-field-origin";return false;}
            var values=new int[CellCount];var seen=new HashSet<int>();
            try
            {
                if(tileAt(x,y)!=originTile){reason="local-field-origin-identity";return false;}
                for(int iy=0;iy<Side;iy++)for(int ix=0;ix<Side;ix++)
                {
                    int tx=x+ix-Radius,ty=y+iy-Radius;
                    int tile=(uint)tx>=800 || (uint)ty>=800?-1:tileAt(tx,ty);
                    if(tile < -1 || tile>=NativeRelocation.TileCapacity || tile>=0 && !seen.Add(tile))
                    {reason="local-field-layout";return false;}
                    values[iy*Side+ix]=tile;
                }
                if(values[Radius*Side+Radius]!=originTile){reason="local-field-origin-identity";return false;}
            }
            catch(Exception){reason="local-field-mapping-failed";return false;}
            map=values;reason=null;return true;
        }
    }
}
