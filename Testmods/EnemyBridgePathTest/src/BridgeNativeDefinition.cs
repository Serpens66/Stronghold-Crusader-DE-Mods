using System;
namespace EnemyBridgePathTest
{
    internal static class BridgeNativeDefinition
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string BackendHash = "0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0";
        internal sealed class Site
        {
            private static int nextIndex;
            internal readonly int Rva, Size, Index;
            internal readonly string Name, Signature;
            internal readonly byte[] Bytes;
            internal Site(int rva,string name,string signature,string bytes,int size)
            { Index=nextIndex++; Rva=rva; Size=size; Name=name; Signature=signature; Bytes=new byte[bytes.Length/2];
              for(int i=0;i<Bytes.Length;i++) Bytes[i]=Convert.ToByte(bytes.Substring(i*2,2),16); }
        }
        internal static readonly Site[] Sites = {
            new Site(0x64460,"lower","V2","48895C2418895424105556574154",349),
            new Site(0x645C0,"raise","V2","48895C241848896C242089542410",312),
            new Site(0xE49D0,"topology","L2","405341574883EC58488BD941BF01000000",1208),
            new Site(0x10D9F0,"planner","V3","48895C240848896C24104889742418",703),
            new Site(0xD95E0,"seed-field","V4","4053415641574883EC304863C24C8D3D0C6AF2FF",1629),
            new Site(0xD9190,"distance-field","V5","44894C2420448944241889542410",1029),
            new Site(0x10F150,"target-region","V2","4863C2488D15A60EEFFFC705408DD80200000000",149),
            new Site(0x10DF60,"candidates","V3","48895C2408448944241889542410",4584),
            new Site(0x1127D0,"region-filter","V0","8B153A55D8024C8D05D3C5F40333C0",61),
            new Site(0x115B10,"weights","V3","405341564883EC384863DA4C8D35DEA4EEFF",1249),
            new Site(0x1150E0,"assign-groups","Assign","48895C24205657415441578B3D0F84DA02",256),
            new Site(0x1151E0,"assign-reachable-groups","AssignWide","40534155415641574883EC388B0D0E83DA02",334),
            new Site(0x10AA20,"dispatch","V2","48895C240848896C24104889742418",3441),
            new Site(0x11A980,"ordinary-attack","V2","48895C240848896C24184889742420",1523),
            new Site(0x122B40,"consume-task","L3","448944241848894C24085356574154",1348),
            new Site(0xE7F60,"work-access","R7","4489442418895424105356574154",989),
            new Site(0x110EC0,"select-110EC0","R2","895424105657415441564883EC48",401),
            new Site(0x111060,"select-111060","R2","48895C2408895424105556574154",714),
            new Site(0x111330,"select-111330","R2","48895C2408895424105556574154",747),
            new Site(0x111620,"select-111620","R2","895424105657415441564883EC48",401),
            new Site(0x111960,"select-111960","R2","448B0D191ED9024533C0448BDA4C8BD1",201),
            new Site(0x111AF0,"select-111AF0","R2","4056574154415541564883EC3033FF",265),
            new Site(0x111C00,"select-111C00","R2","895424105657415441574883EC48",390),
            new Site(0x111D90,"select-111D90","R2","895424105657415441564883EC48",398),
            new Site(0x111F20,"select-111F20","R2","895424104C8BDC5657415441554883EC68",487),
            new Site(0x3C2E0,"attack-phases","V2","48895C241848897424205741544157",1753),
            new Site(0x2D250,"attack-field","V3","48895C240848896C24104889742418",275),
            new Site(0x2C480,"attack-candidates","V2","48895C240848896C24104889742418",277),
            new Site(0x2C5A0,"attack-target-region","V3","4C8D0D593AFDFF4863C24869C83C580000",113),
            new Site(0x3BD50,"attack-position","V2","48895C2420895424105556574154",577),
            new Site(0xCF360,"keep-access-mode0","L3","4883EC384C63D24D69CA3C580000",152),
            new Site(0xCF400,"keep-access-mode1","L3","4883EC384C63D24D69CA3C580000",152),
        };
    }
}
