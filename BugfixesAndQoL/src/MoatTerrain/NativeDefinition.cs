using System;
using System.Security.Cryptography;
using Iced.Intel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.MoatTerrain
{
    internal static class NativeDefinition
    {
        internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int TileManager = 0x405EDB0, Logic = 0x898400, Soil = 0x9D2500, Structure = 0xB0BCA0;
        internal sealed class Site
        {
            internal readonly TerrainSiteKind Kind;
            internal readonly int Rva, Length, FunctionRva, FunctionLength;
            internal readonly string FunctionHash;
            internal readonly byte[] Bytes;
            internal Site(TerrainSiteKind kind, int rva, int length, int functionRva,
                int functionLength, string hash, string bytes)
            {
                Kind = kind; Rva = rva; Length = length; FunctionRva = functionRva;
                FunctionLength = functionLength; FunctionHash = hash;
                Bytes = new byte[bytes.Length / 2];
                for (int i = 0; i < Bytes.Length; i++) Bytes[i] = Convert.ToByte(bytes.Substring(i * 2, 2), 16);
            }
        }

        // The module hash pins tables/layouts; these immutable function hashes pin the decoded bodies.
        // The independent audit checks every instruction and all incoming interior references.
        internal static readonly Site[] Sites = {
            new Site(TerrainSiteKind.TaskCreated, 0x59359, 19, 0x59210, 0x161,
                "534A34087A1818B1C7BE945CA20252D5C8EB37388C1E066D2D00AEAF63BE81AA", "8BC24963C96689944BF023EA01FF83348E0302"),
            new Site(TerrainSiteKind.Excavated, 0x63A4E, 16, 0x639C0, 0x143,
                "7E25A104B7BCB3C8CE462AB396F637F052F548C8B03900C67E80DE974702445C", "66C7863DEEF3010402488D0D029C0406"),
            new Site(TerrainSiteKind.Filled, 0x694D3, 15, 0x69470, 0xED,
                "F9E2EAEB9B1F1C53D33D402BF419D4459699BB2A3B3EB572F24A7404F1342AE5", "488D0D86410406440FBF8F36EEF301"),
            new Site(TerrainSiteKind.DirectPrepare, 0x705BF, 14, 0x6FE90, 0xB19,
                "87AE5D7E881984B921AE51066BF3C197C3D91DB78637BA7D67ACF3DDE2722B39", "C6843B00259D00004183FF027545"),
            new Site(TerrainSiteKind.BrushFinished, 0x70940, 18, 0x6FE90, 0xB19,
                "87AE5D7E881984B921AE51066BF3C197C3D91DB78637BA7D67ACF3DDE2722B39", "448BC5488D0D16CD0306418BD4E87E870600"),
            new Site(TerrainSiteKind.BridgeRemoved, 0x6240E, 17, 0x622D0, 0x185,
                "10725FB9CEB04AE6DF6558043D5F0E2B9099CDCE2CE7CAC984B89983A4D658EB", "33C0488D2DE9DBF9FF66898473A0BCB000"),
            new Site(TerrainSiteKind.BridgePurged, 0x6CADD, 16, 0x6CA30, 0xE8,
                "7D9A2AEC90A583E3C2874D6288FDAB2789915F257ADDC94AA7EC9B54FCF75734", "4488BC1EE086BA00C6841E400DA20002"),
            new Site(TerrainSiteKind.AreaRemoved, 0xEDA8C, 15, 0xED2E0, 0x8F5,
                "D87CA9A8EB1E044624D2A01890DA7B3D3DD50828192EEA62FB3C3A091BACAE67", "66418907410FB7C16603074983C702"),
            new Site(TerrainSiteKind.Paint, 0x7082E, 17, 0x6FE90, 0xB19,
                "87AE5D7E881984B921AE51066BF3C197C3D91DB78637BA7D67ACF3DDE2722B39", "4181263EFFC51F4183FF027404418326F7"),
            new Site(TerrainSiteKind.BuildingPrepare, 0x6D8DF, 20, 0x6D580, 0xA68,
                "95B0921E3C72C6DE1B61464C06126A76702835D3376738E973E609EB3FBEC004", "F784B300848900004000407426448B8364E70402"),
            new Site(TerrainSiteKind.Replaced, 0x5D76F, 20, 0x5D740, 0xE8,
                "803B0116AECB2706B9FEB40414A92C729A0A54D120F6BE01B7A6A13C01422061", "81A4B900848900F7F4BEFF420FB6841F70B8E204"),
            new Site(TerrainSiteKind.Erased, 0x7AFAF, 17, 0x7AE50, 0x20B,
                "502F2431C0C84C78784C1CEEE2C54D2C101BF14244A817B30B194BFC7887A66F", "C6843D00259D0000420FB68C2D50D3DD04"),
            new Site(TerrainSiteKind.Vegetation, 0x1085D0, 18, 0x108320, 0x33A,
                "50A493A452B3DB4311A43041A54CFE95CD8FB2179073AF96F2DBAB157AAAD9E1", "4863838C00000081A48700848900FFBFFFFF"),
            new Site(TerrainSiteKind.VegetationUpdate, 0x109A05, 17, 0x109930, 0x123,
                "1E3A8029390B2FE3E99FBE0758BA4A36378E7ABF6452895FEA12D60A033775B7", "440FBF4320488BCE0FBF531EE8BA84F5FF"),
        };

        internal static void ValidateLayouts()
        {
            if (GameTileManagerView.NativePackedTileCapacity != TerrainState.Capacity ||
                (int)eStructs.STRUCT_DRAWBRIDGE != 0x31 || (int)TileType.OasisGrass != 0x10 ||
                (uint)TilePropertyFlag.IsMoat != 0x40000000 || (uint)TilePropertyFlag.PlannedMoat != 0x4000)
                throw new InvalidOperationException("Moat terrain enum/capacity contract differs.");
        }

        internal static void ValidateCode(ReadOnlySpan<byte> image)
        {
            using (var sha = SHA256.Create())
            {
                foreach (Site site in Sites)
                {
                    if (site.Rva < site.FunctionRva || site.Rva + site.Length > site.FunctionRva + site.FunctionLength ||
                        site.FunctionRva + site.FunctionLength > image.Length ||
                        !image.Slice(site.Rva, site.Length).SequenceEqual(site.Bytes))
                        throw new InvalidOperationException("Moat terrain span mismatch: " + site.Kind);
                    string hash = BitConverter.ToString(sha.ComputeHash(image.Slice(site.FunctionRva, site.FunctionLength).ToArray())).Replace("-", "");
                    if (hash != site.FunctionHash)
                        throw new InvalidOperationException("Moat terrain function/control-flow contract differs: " + site.Kind);
                    var reader = new ByteArrayCodeReader(site.Bytes);
                    var decoder = Decoder.Create(64, reader, DecoderOptions.None);
                    decoder.IP = (ulong)site.Rva;
                    int length = 0;
                    while (reader.CanReadByte)
                    {
                        decoder.Decode(out Instruction instruction);
                        if (instruction.Code == Code.INVALID) throw new InvalidOperationException("Invalid native instruction.");
                        length += instruction.Length;
                    }
                    if (length != site.Length) throw new InvalidOperationException("Incomplete moat terrain instructions.");
                }
            }
        }
    }
}
