using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using APIShared.WorldMutations;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Per registered APSV record, not the unrelated global mod-settings container.
    // Fixed binary bounds; no compression or live game lookup is required.
    internal static class RescueCompactArchiveCodec
    {
        internal const int MaximumUnits=9999,MaximumLegacy=512,MaximumEntryBytes=1048576,MaximumRecordBytes=8388608;
        internal const int ModernBodyBytes=600,ChunkRecords=256;
        private const uint Signature=0x52434143;
        private const string Manifest="compact/meta",Sources="compact/sources",Owned="owned-workers";
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        // Typed source contracts, never native/API enum ordinals on the wire.
        private static readonly WorldMutationSource[] NativeSources={WorldMutationSource.AivBuildStep,WorldMutationSource.BuildStructure,
            WorldMutationSource.HumanWall,WorldMutationSource.AiWall,WorldMutationSource.EditorTileType,WorldMutationSource.EditorTileTypeClear,
            WorldMutationSource.EditorHeightPreset,WorldMutationSource.EditorHeightRaise,WorldMutationSource.EditorHeightNumber,
            WorldMutationSource.EditorHeightEven,WorldMutationSource.EditorDelete,WorldMutationSource.MoatDig,WorldMutationSource.MoatFill,
            WorldMutationSource.BuildingRemoval,WorldMutationSource.WallRemoval};
        private static bool NativeSource(string source)
        {foreach(var value in NativeSources){string name=value.ToString();if(source==name || source=="replan/"+name)return true;}return false;}
        private sealed class Row
        {internal int Id;internal string Source;internal byte Kind;internal byte[] Body;internal bool Legacy;}
        internal static bool HasManifest(IDictionary<string,byte[]> entries)=>entries!=null && entries.ContainsKey(Manifest);
        internal static bool TryEncode(RescuePendingCandidate<UnitSnapshot>[] pending,RescuePendingGeometry[] geometry,byte[] ownedLedgerBytes,
            out Dictionary<string,byte[]> entries,out string reason)
        {
            entries=null;reason="compact-archive-invalid";
            if(pending==null || geometry==null || pending.Length+geometry.Length>MaximumUnits || !ValidOwned(ownedLedgerBytes))return false;
            try{
                var rows=new List<Row>();var ids=new HashSet<int>();int legacy=0;
                foreach(var record in pending){var u=record.Snapshot;if(u==null || !ids.Add(u.Id))return false;
                    bool old=u.LocalField==null && u.GeometryWitness?.LocalField==null;
                    bool imported=old && (u.Candidates.Count!=0 || u.GeometryWitness!=null || record.LifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge);
                    if(imported && ++legacy>MaximumLegacy || !NativeSource(record.Source) && !imported)return false;
                    using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                        if(!RescuePendingCodec.TryWriteCompactBody(w,record))return false;w.Flush();
                        if(stream.Length>(imported?ModernBodyBytes+3142:ModernBodyBytes))return false;
                        rows.Add(new Row{Id=u.Id,Source=record.Source,Kind=1,Body=stream.ToArray(),Legacy=imported});}}
                foreach(var record in geometry){if(record.Witness==null || !ids.Add(record.Witness.UnitId) || !RescueGeometryCodec.TryEncode(record,out _))return false;
                    bool imported=record.Witness.LocalField==null;if(imported && ++legacy>MaximumLegacy || !NativeSource(record.Source) && !imported)return false;
                    using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                        RescuePendingCodec.WriteLifetime(w,record.Age,record.Attempts,record.RetryIn,record.LifetimeMode,record.ActiveAge,record.AwaitingResources);
                        RescuePendingCodec.WriteField(w,record.Witness.LocalField);RescuePendingCodec.WriteWitnessMetadata(w,record.Witness);
                        if(imported)RescuePendingCodec.WriteBanks(w,record.Witness.CopyCandidates());w.Flush();
                        if(stream.Length>(imported?ModernBodyBytes+3142:ModernBodyBytes))return false;
                        rows.Add(new Row{Id=record.Witness.UnitId,Source=record.Source,Kind=2,Body=stream.ToArray(),Legacy=imported});}}
                rows.Sort((a,b)=>a.Id.CompareTo(b.Id));var pool=new SortedSet<string>(StringComparer.Ordinal);foreach(var row in rows)pool.Add(row.Source);
                if(pool.Count>542)return false;var list=new List<string>(pool);var refs=new Dictionary<string,ushort>(StringComparer.Ordinal);
                var result=new Dictionary<string,byte[]>(StringComparer.Ordinal){{Owned,(byte[])ownedLedgerBytes.Clone()}};
                using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                    w.Write(Signature);w.Write((ushort)1);w.Write((ushort)list.Count);
                    for(int i=0;i<list.Count;i++){byte[] text=Utf8.GetBytes(list[i]);if(text.Length<1 || text.Length>384 || list[i].Length>96)return false;
                        refs.Add(list[i],(ushort)i);w.Write((ushort)text.Length);w.Write(text);}w.Flush();result.Add(Sources,stream.ToArray());}
                int chunks=(rows.Count+ChunkRecords-1)/ChunkRecords;
                for(int i=0;i<chunks;i++)using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                    int count=Math.Min(ChunkRecords,rows.Count-i*ChunkRecords);w.Write(Signature);w.Write((ushort)1);w.Write((ushort)i);w.Write((ushort)count);
                    for(int j=0;j<count;j++){var row=rows[i*ChunkRecords+j];w.Write(row.Kind);w.Write(refs[row.Source]);w.Write((ushort)row.Body.Length);w.Write(row.Body);}w.Flush();result.Add(Key(i),stream.ToArray());}
                using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                    w.Write(Signature);w.Write((ushort)1);w.Write((ushort)pending.Length);w.Write((ushort)geometry.Length);w.Write((ushort)pool.Count);w.Write((ushort)chunks);w.Write((ushort)legacy);
                    w.Flush();result.Add(Manifest,stream.ToArray());}
                if(!WithinEnvelope(result))return false;entries=result;reason=null;return true;
            }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
        private static string Key(int index)=>"compact/"+index.ToString("D4",CultureInfo.InvariantCulture);
        private static bool ValidOwned(byte[] bytes)=>bytes!=null && new WorkerResumeLedger().TryRestore(bytes,0);
        internal static bool WithinEnvelope(IDictionary<string,byte[]> entries)
        {try{if(entries==null || entries.Count>16384)return false;long bytes=32;
            foreach(var pair in entries){if(pair.Key==null || pair.Key.Length>256 || pair.Value==null || pair.Value.Length>MaximumEntryBytes)return false;
                int name=Utf8.GetByteCount(pair.Key);if(name<1 || name>1024)return false;bytes+=2L+name+4+pair.Value.Length;if(bytes>MaximumRecordBytes)return false;}return true;}
            catch(ArgumentException){return false;}}
        internal static bool TryDecode(IDictionary<string,byte[]> entries,out RescuePendingCandidate<UnitSnapshot>[] pending,
            out RescuePendingGeometry[] geometry,out string reason)
        {
            pending=null;geometry=null;reason="compact-archive-invalid";
            try{
                if(!WithinEnvelope(entries) || !entries.TryGetValue(Owned,out var owned) || !ValidOwned(owned) || !entries.TryGetValue(Manifest,out var meta) || !entries.TryGetValue(Sources,out var names))return false;
                int np,ng,ns,nc,nl;
                using(var stream=new MemoryStream(meta,false))using(var r=new BinaryReader(stream)){
                    if(r.ReadUInt32()!=Signature || r.ReadUInt16()!=1)return false;np=r.ReadUInt16();ng=r.ReadUInt16();ns=r.ReadUInt16();nc=r.ReadUInt16();nl=r.ReadUInt16();
                    if(stream.Position!=stream.Length || np+ng>MaximumUnits || ns>542 || nl>MaximumLegacy || nc!=(np+ng+ChunkRecords-1)/ChunkRecords || entries.Count!=nc+3)return false;}
                var pool=new string[ns];var used=new bool[ns];
                using(var stream=new MemoryStream(names,false))using(var r=new BinaryReader(stream)){
                    if(r.ReadUInt32()!=Signature || r.ReadUInt16()!=1 || r.ReadUInt16()!=ns)return false;
                    for(int i=0;i<ns;i++){int length=r.ReadUInt16();if(length<1 || length>384)return false;pool[i]=Utf8.GetString(RescuePendingCodec.ReadExact(r,length));
                        if(pool[i].Length<1 || pool[i].Length>96 || i>0 && StringComparer.Ordinal.Compare(pool[i-1],pool[i])>=0)return false;}
                    if(stream.Position!=stream.Length)return false;}
                var ps=new List<RescuePendingCandidate<UnitSnapshot>>();var gs=new List<RescuePendingGeometry>();int lastId=0,legacy=0;
                for(int i=0;i<nc;i++){
                    if(!entries.TryGetValue(Key(i),out var bytes))return false;
                    using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream)){
                        int expected=Math.Min(ChunkRecords,np+ng-i*ChunkRecords);
                        if(r.ReadUInt32()!=Signature || r.ReadUInt16()!=1 || r.ReadUInt16()!=i || r.ReadUInt16()!=expected)return false;
                        for(int j=0;j<expected;j++){
                            int kind=r.ReadByte(),source=r.ReadUInt16(),length=r.ReadUInt16();if(source>=ns || length<1 || length>ModernBodyBytes+3142)return false;
                            used[source]=true;var body=RescuePendingCodec.ReadExact(r,length);bool imported;int id;
                            using(var sub=new MemoryStream(body,false))using(var reader=new BinaryReader(sub)){
                                if(kind==1){if(!RescuePendingCodec.TryReadCompactBody(reader,pool[source],out var record))return false;
                                    var u=record.Snapshot;id=u.Id;bool old=u.LocalField==null && u.GeometryWitness?.LocalField==null;
                                    imported=old && (u.Candidates.Count!=0 || u.GeometryWitness!=null || record.LifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge);ps.Add(record);}
                                else if(kind==2){RescuePendingCodec.ReadLifetime(reader,out int age,out int attempts,out int retry,out var mode,out int active,out bool awaiting);
                                    var field=RescuePendingCodec.ReadField(reader);var witness=RescuePendingCodec.ReadWitness(reader,field);
                                    var record=new RescuePendingGeometry(pool[source],witness,age,attempts,retry,mode,active,awaiting);
                                    if(!RescueGeometryCodec.TryEncode(record,out _))return false;id=witness.UnitId;imported=field==null;gs.Add(record);}
                                else return false;
                                if(sub.Position!=sub.Length)return false;
                            }
                            if(id<=lastId || id>MaximumUnits || imported && ++legacy>MaximumLegacy || !NativeSource(pool[source]) && !imported || !imported && length>ModernBodyBytes)return false;lastId=id;
                        }
                        if(stream.Position!=stream.Length)return false;
                    }
                }
                foreach(bool referenced in used)if(!referenced)return false;
                if(ps.Count!=np || gs.Count!=ng || legacy!=nl)return false;pending=ps.ToArray();geometry=gs.ToArray();reason=null;return true;
            }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
    }
}
