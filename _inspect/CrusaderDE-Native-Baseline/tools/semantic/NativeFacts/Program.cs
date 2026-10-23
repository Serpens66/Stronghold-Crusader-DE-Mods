using Iced.Intel;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

// Offline decoder. Never loads CrusaderDE.dll or creates executable buffers.
static class Program {
    static readonly InstructionInfoFactory Info = new();
    static ulong Number(string s) => Convert.ToUInt64(s.Replace("0x", "", StringComparison.OrdinalIgnoreCase),16);
    static string Hex(ulong n) => $"0x{n:X}";
    static int Main(string[] args) {
        if(args.Length!=3)throw new ArgumentException("native-file expected-sha work-directory");
        byte[] pe=File.ReadAllBytes(args[0]);
        if(!Convert.ToHexString(SHA256.HashData(pe)).Equals(args[1],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Native SHA mismatch");
        int header=BitConverter.ToInt32(pe,0x3C), optional=header+24, sections=BitConverter.ToUInt16(pe,header+6);
        ulong imageBase=BitConverter.ToUInt64(pe,optional+24);int table=optional+BitConverter.ToUInt16(pe,header+20);
        byte[] Read(ulong rva,int count) {
            for(int i=0;i<sections;i++){
                int p=table+i*40;uint va=BitConverter.ToUInt32(pe,p+12),size=BitConverter.ToUInt32(pe,p+16),offset=BitConverter.ToUInt32(pe,p+20);
                if(rva>=va && rva+(ulong)count<=va+size)return pe.AsSpan(checked((int)(offset+rva-va)),count).ToArray();
            }
            throw new InvalidDataException("Instruction has no file-backed PE range");
        }
        int total=0;
        foreach(string path in Directory.EnumerateFiles(args[2],"*.json").Order()){
            if(Path.GetFileName(path) is "COMPLETE.json" or "TYPES.json")continue;
            JsonObject f=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            foreach(JsonNode? value in f["instructions"]!.AsArray()){
                JsonObject row=value!.AsObject();ulong rva=Number(row["rva"]!.GetValue<string>());int length=row["length"]!.GetValue<int>();
                byte[] bytes=Read(rva,length);bool bytesAgree=Convert.ToHexString(bytes)==row["bytes"]!.GetValue<string>();
                Decoder decoder=Decoder.Create(64,new ByteArrayCodeReader(bytes),imageBase+rva);Instruction ins=decoder.Decode();
                bool valid=bytesAgree && !ins.IsInvalid && decoder.LastError==DecoderError.None && ins.Length==length;
                row["icedVerified"]=valid;
                if(!valid){row["decoderError"]="Ghidra/Iced/PE disagreement";continue;}
                InstructionInfo facts=Info.GetInfo(ins);JsonArray registers=new(),memory=new(),operands=new();
                foreach(UsedRegister reg in facts.GetUsedRegisters())registers.Add(new JsonObject{["register"]=reg.Register.ToString(),["access"]=reg.Access.ToString()});
                foreach(UsedMemory mem in facts.GetUsedMemory())memory.Add(new JsonObject{
                    ["segment"]=mem.Segment.ToString(),["base"]=mem.Base.ToString(),["index"]=mem.Index.ToString(),["scale"]=mem.Scale,
                    ["displacement"]=Hex(mem.Displacement),["size"]=mem.MemorySize.ToString(),["access"]=mem.Access.ToString()});
                for(int j=0;j<ins.OpCount;j++)operands.Add(new JsonObject{["kind"]=ins.GetOpKind(j).ToString(),["access"]=facts.GetOpAccess(j).ToString()});
                row["registers"]=registers;row["memory"]=memory;row["operands"]=operands;
                row["flagsRead"]=(uint)ins.RflagsRead;row["flagsWritten"]=(uint)ins.RflagsWritten;
                row["flagsCleared"]=(uint)ins.RflagsCleared;row["flagsSet"]=(uint)ins.RflagsSet;row["flagsUndefined"]=(uint)ins.RflagsUndefined;
                row["icedFlow"]=ins.FlowControl.ToString();row["mnemonic"]=ins.Mnemonic.ToString();
                row["stackIncrement"]=ins.IsStackInstruction ? ins.StackPointerIncrement : null;
                row["ripTarget"]=ins.IsIPRelativeMemoryOperand?Hex(ins.IPRelativeMemoryAddress-imageBase):null;
            }
            f["icedStatus"]="complete";
            f["icedAssemblySha256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Decoder).Assembly.Location)));
            string tmp=path+".tmp";File.WriteAllText(tmp,f.ToJsonString()+"\r\n",new System.Text.UTF8Encoding(false));File.Move(tmp,path,true);
            File.WriteAllText(path+".sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))+"\r\n",new System.Text.UTF8Encoding(false));
            if(++total%100==0)Console.WriteLine($"ICED_PROGRESS functions={total}");
        }
        Console.WriteLine($"ICED_COMPLETE functions={total}");return 0;
    }
}
