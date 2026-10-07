using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace BridgePlanningTests
{
    // Independent x64 process, private PE image: no LoadLibrary, DllMain or game initialization.
    internal sealed unsafe class NativeImage : IDisposable
    {
        [DllImport("kernel32",SetLastError=true)] private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint kind,uint protection);
        [DllImport("kernel32",SetLastError=true)] private static extern bool VirtualFree(IntPtr address,UIntPtr size,uint kind);
        [DllImport("kernel32",SetLastError=true)] private static extern bool VirtualProtect(IntPtr address,UIntPtr size,uint protection,out uint previous);
        internal readonly IntPtr Base;
        internal readonly int Size;
        internal readonly List<int> Guards=new List<int>();
        private readonly byte[] image;
        internal NativeImage(string dll,string contracts)
        {
            image=File.ReadAllBytes(dll);string[] rows=File.ReadAllLines(contracts);if(Hash(image)!=rows[0])throw new Exception("Full native hash mismatch");
            int pe=BitConverter.ToInt32(image,0x3c),optional=pe+24;Size=BitConverter.ToInt32(image,optional+56);ulong preferred=BitConverter.ToUInt64(image,optional+24);
            if(BitConverter.ToUInt16(image,optional)!=0x20b||Size!=0x8903000)throw new Exception("Native image contract mismatch");
            Base=VirtualAlloc(IntPtr.Zero,(UIntPtr)Size,0x3000,4);if(Base==IntPtr.Zero)throw new Exception("Private native image allocation");
            try
            {
                int sections=BitConverter.ToUInt16(image,pe+6),table=optional+BitConverter.ToUInt16(image,pe+20);
                for(int i=0;i<sections;i++){int s=table+i*40,rva=BitConverter.ToInt32(image,s+12),length=BitConverter.ToInt32(image,s+16),file=BitConverter.ToInt32(image,s+20);if(length>0)Marshal.Copy(image,file,Ptr(rva,length),length);}
                int reloc=BitConverter.ToInt32(image,optional+112+40),end=reloc+BitConverter.ToInt32(image,optional+116+40);long delta=unchecked(Base.ToInt64()-(long)preferred);
                while(reloc<end){int page=Int(reloc),length=Int(reloc+4);if(length<8)throw new Exception("Relocation size");for(int a=reloc+8;a<reloc+length;a+=2){int v=(ushort)Short(a),kind=v>>12,at=page+(v&4095);if(kind==10)Long(at,unchecked(Long(at)+delta));else if(kind!=0)throw new Exception("Unexpected PE relocation");}reloc+=length;}
                for(int i=1;i<rows.Length;i++)
                {
                    string[] part=rows[i].Split('\t');if(part[0]=="GUARD"){Guards.Add(Convert.ToInt32(part[1],16));continue;}
                    int rva=Convert.ToInt32(part[0],16),length=int.Parse(part[1]);var body=new byte[length];Marshal.Copy(Ptr(rva,length),body,0,length);
                    // These bodies have RIP-relative addressing; no PE relocation may alter them.
                    if(Hash(body)!=part[2])throw new Exception("Complete body mismatch "+part[0]);
                    uint old;if(!VirtualProtect(Ptr(rva),(UIntPtr)length,0x40,out old))throw new Exception("Private execute protection");
                }
                foreach(int rva in Guards){Byte(rva,0x0f);Byte(rva+1,0x0b);} // unsupported branch traps; never a permissive search stub.
            }
            catch{VirtualFree(Base,UIntPtr.Zero,0x8000);throw;}
        }
        private static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","");}
        internal IntPtr Ptr(int rva,int bytes=1){if(rva<0||bytes<0||(long)rva+bytes>Size)throw new Exception("Outside private image");return IntPtr.Add(Base,rva);}
        internal int Int(int rva)=>Marshal.ReadInt32(Ptr(rva,4));internal void Int(int rva,int v)=>Marshal.WriteInt32(Ptr(rva,4),v);
        internal long Long(int rva)=>Marshal.ReadInt64(Ptr(rva,8));internal void Long(int rva,long v)=>Marshal.WriteInt64(Ptr(rva,8),v);
        internal short Short(int rva)=>Marshal.ReadInt16(Ptr(rva,2));internal void Short(int rva,int v)=>Marshal.WriteInt16(Ptr(rva,2),unchecked((short)v));
        internal byte Byte(int rva)=>Marshal.ReadByte(Ptr(rva));internal void Byte(int rva,int v)=>Marshal.WriteByte(Ptr(rva),unchecked((byte)v));
        internal void Put(int rva,byte[] v){Marshal.Copy(v,0,Ptr(rva,checked(v.Length*1)),v.Length);}internal void Put(int rva,int[] v){Marshal.Copy(v,0,Ptr(rva,checked(v.Length*4)),v.Length);}internal void Put(int rva,short[] v){Marshal.Copy(v,0,Ptr(rva,checked(v.Length*2)),v.Length);}
        internal byte[] Bytes(int rva,int length){byte[] v=new byte[length];Marshal.Copy(Ptr(rva,checked(length*1)),v,0,length);return v;}
        internal short[] Shorts(int rva,int length){short[] v=new short[length];Marshal.Copy(Ptr(rva,checked(length*2)),v,0,length);return v;}
        internal int[] Ints(int rva,int length){int[] v=new int[length];Marshal.Copy(Ptr(rva,checked(length*4)),v,0,length);return v;}
        internal T Function<T>(int rva) where T:class => Marshal.GetDelegateForFunctionPointer(Ptr(rva),typeof(T)) as T;
        // Unpublished private image only; never touches installed hooks or library.
        internal void PrivateExecutable(int rva,int length)
        {uint previous;if(!VirtualProtect(Ptr(rva,length),(UIntPtr)length,0x40,out previous))throw new Exception("Private stub protection");}
        public void Dispose(){VirtualFree(Base,UIntPtr.Zero,0x8000);} // exclusively unpublished private test memory.
    }
}
