using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace EnemyBridgePathTest
{
    // Publisher-only transport: no game reads, semantic filtering, definitions or
    // native work. Every original field/row is retained and hash checked offline.
    internal static class BridgeTraceTransport
    {
        internal const int MaximumBytes=65536;
        private static string Hash(byte[] data)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
        internal static string EncodeBatch(System.Collections.Generic.IList<string> lines,out int rawBytes,out int savedBytes)
        {
            if(lines.Count<1||lines.Count>16)throw new InvalidDataException("packed trace record count");
            string original=string.Join("\n",lines);byte[] bytes=Encoding.UTF8.GetBytes(original);
            rawBytes=bytes.Length;savedBytes=0;
            if(rawBytes>MaximumBytes)throw new InvalidDataException("packed batch extent");
            if(rawBytes<512)return null;
            byte[] packed;using(var output=new MemoryStream())
            {using(var codec=new DeflateStream(output,CompressionLevel.Fastest,true))codec.Write(bytes,0,bytes.Length);packed=output.ToArray();}
            if(packed.Length*4L/3+256>=rawBytes)return null;
            int start=lines[0].IndexOf(",kind=",StringComparison.Ordinal);if(start<0)throw new InvalidDataException("packed batch first envelope");
            string wire=lines[0].Substring(0,start+1)+"kind=packed-batch,schema=1,records="+lines.Count+",rawBytes="+bytes.Length+",sha256="+Hash(bytes)+",payload=["+Convert.ToBase64String(packed)+"]";
            int size=Encoding.UTF8.GetByteCount(wire);if(size+64>=rawBytes)return null;
            savedBytes=rawBytes-size;return wire;
        }
        internal static string[] DecodeLines(string line)
        {
            const string marker=",kind=packed-batch,";int start=line.IndexOf(marker,StringComparison.Ordinal);
            if(start<0)return new[]{line};
            string[] fields=line.Substring(start+marker.Length).Split(',');
            if(fields.Length!=5||fields[0]!="schema=1"||!fields[1].StartsWith("records="))throw new InvalidDataException("packed batch envelope");
            int count=int.Parse(fields[1].Substring(8),System.Globalization.CultureInfo.InvariantCulture);
            if(count<1||count>16||!fields[2].StartsWith("rawBytes=")||!fields[3].StartsWith("sha256=")||!fields[4].StartsWith("payload=[")||!fields[4].EndsWith("]"))throw new InvalidDataException("packed batch bounds");
            int length=int.Parse(fields[2].Substring(9),System.Globalization.CultureInfo.InvariantCulture);
            if(length<1||length>MaximumBytes||fields[4].Length>MaximumBytes*2)throw new InvalidDataException("packed batch extent");
            byte[] compressed=Convert.FromBase64String(fields[4].Substring(9,fields[4].Length-10)),bytes=new byte[length];int copied=0;
            using(var input=new MemoryStream(compressed))using(var codec=new DeflateStream(input,CompressionMode.Decompress))
            {while(copied<length){int n=codec.Read(bytes,copied,length-copied);if(n==0)throw new InvalidDataException("packed batch truncated");copied+=n;}if(codec.ReadByte()!=-1)throw new InvalidDataException("packed batch excess output");}
            if(!string.Equals(Hash(bytes),fields[3].Substring(7),StringComparison.Ordinal))throw new InvalidDataException("packed batch hash");
            string[] result=new UTF8Encoding(false,true).GetString(bytes).Split('\n');
            if(result.Length!=count)throw new InvalidDataException("packed batch record count mismatch");
            foreach(string row in result)if(!row.Contains("bridge trace seq=")||!row.Contains(",kind=")||row.Contains(",kind=packed-")||row.IndexOf('\r')>=0)throw new InvalidDataException("packed batch record body");
            return result;
        }
    }
}
