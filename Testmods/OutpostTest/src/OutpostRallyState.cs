using System;
using System.Collections.Generic;
using System.IO;

namespace OutpostTest
{
    internal sealed class OutpostRallyState
    {
        internal sealed class Record
        {
            internal int Building, Owner, Type, Target, Produced;
            internal uint Global;
            internal bool HasPoint;
            internal int X, Y;
            internal bool Matches(uint global,int owner,int type) => OutpostSchedule.SameIdentity(Global,Owner,Type,global,owner,type);
        }
        internal sealed class Order
        {
            internal int Building, Owner, Unit, Tribe, X, Y, Wait;
            internal uint BuildingGlobal, UnitGlobal, TribeGlobal;
        }
        internal readonly Dictionary<int,Record> Records = new Dictionary<int,Record>();
        internal readonly List<Order> Pending = new List<Order>();
        internal Record Get(int id,uint global,int owner,int type)
        {
            if (!Records.TryGetValue(id,out var r) || !r.Matches(global,owner,type))
            {
                Remove(id);
                Records[id]=r=new Record { Building=id, Global=global, Owner=owner, Type=type };
            }
            return r;
        }
        internal void Remove(int id)
        { Records.Remove(id); Pending.RemoveAll(p=>p.Building==id); }
        internal void CancelTribe(int tribe) => Pending.RemoveAll(p=>p.Tribe==tribe);
        internal void CancelUnit(int unit) => Pending.RemoveAll(p=>p.Unit==unit);
        internal void Queue(Record r,int unit,uint global,int tribe,uint tribeGlobal)
        {
            if (r.HasPoint) Pending.Add(new Order { Building=r.Building, BuildingGlobal=r.Global, Owner=r.Owner,
                Unit=unit, UnitGlobal=global, Tribe=tribe, TribeGlobal=tribeGlobal, X=r.X, Y=r.Y });
        }
        // Slot identity and ownership are checked by the native adapter before Ready is evaluated.
        internal static bool Ready(bool needsInit,bool alive,int members,int currentTribe,int expectedTribe) =>
            !needsInit && alive && members==1 && currentTribe==expectedTribe;
        internal static bool PointValid(int x,int y) => x>=0 && x<800 && y>=0 && y<800;
        internal byte[] Encode()
        {
            using (var stream=new MemoryStream()) using(var w=new BinaryWriter(stream))
            {
                w.Write(0x4F505254); w.Write(1); w.Write(Records.Count);
                foreach(var r in Records.Values)
                { w.Write(r.Building);w.Write(r.Global);w.Write(r.Owner);w.Write(r.Type);w.Write(r.HasPoint);w.Write(r.X);w.Write(r.Y);w.Write(r.Target);w.Write(r.Produced); }
                w.Write(Pending.Count);
                foreach(var p in Pending)
                { w.Write(p.Building);w.Write(p.BuildingGlobal);w.Write(p.Owner);w.Write(p.Unit);w.Write(p.UnitGlobal);w.Write(p.Tribe);w.Write(p.TribeGlobal);w.Write(p.X);w.Write(p.Y);w.Write(p.Wait); }
                return stream.ToArray();
            }
        }
        internal static OutpostRallyState Decode(byte[] bytes)
        {
            if(bytes==null || bytes.Length>1024*1024) throw new InvalidDataException("Invalid rally save size.");
            var result=new OutpostRallyState();
            using(var stream=new MemoryStream(bytes,false)) using(var r=new BinaryReader(stream))
            {
                if(r.ReadInt32()!=0x4F505254 || r.ReadInt32()!=1) throw new InvalidDataException("Unknown rally save version.");
                int count=Count(r,3999);
                for(int i=0;i<count;i++)
                {
                    var e=new Record { Building=r.ReadInt32(),Global=r.ReadUInt32(),Owner=r.ReadInt32(),Type=r.ReadInt32(),
                        HasPoint=r.ReadBoolean(),X=r.ReadInt32(),Y=r.ReadInt32(),Target=r.ReadInt32(),Produced=r.ReadInt32() };
                    if(e.Building<1 || e.Building>3999 || e.Owner<1 || e.Owner>8 || !OutpostSchedule.IsOutpost(e.Type) ||
                        !PointValid(e.X,e.Y) || e.Target<0 || e.Target>133 || e.Produced<0 || e.Produced>e.Target || result.Records.ContainsKey(e.Building))
                        throw new InvalidDataException("Invalid rally record.");
                    result.Records.Add(e.Building,e);
                }
                count=Count(r,10000); var units=new HashSet<int>();
                for(int i=0;i<count;i++)
                {
                    var p=new Order { Building=r.ReadInt32(),BuildingGlobal=r.ReadUInt32(),Owner=r.ReadInt32(),Unit=r.ReadInt32(),
                        UnitGlobal=r.ReadUInt32(),Tribe=r.ReadInt32(),TribeGlobal=r.ReadUInt32(),X=r.ReadInt32(),Y=r.ReadInt32(),Wait=r.ReadInt32() };
                    if(!result.Records.TryGetValue(p.Building,out var e) || e.Global!=p.BuildingGlobal || e.Owner!=p.Owner ||
                        p.Unit<1 || p.Unit>10000 || p.Tribe<1 || p.Tribe>=4500 || !PointValid(p.X,p.Y) || p.Wait<0 || p.Wait>80 || !units.Add(p.Unit))
                        throw new InvalidDataException("Invalid saved rally order.");
                    result.Pending.Add(p);
                }
                if(stream.Position!=stream.Length) throw new InvalidDataException("Trailing rally save data.");
            }
            return result;
        }
        private static int Count(BinaryReader r,int max)
        { int count=r.ReadInt32(); if(count<0 || count>max) throw new InvalidDataException("Invalid rally save count."); return count; }
    }
}
