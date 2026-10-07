using System;
namespace EnemyBridgePathTest
{
    // Hash-bound native storage, NOT the Extender's enum-count-sized per-class view.
    internal sealed class NativePathfindingTableCopy
    {
        internal const string Hash="FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int Types=90,Classes=6,ProfileRva=0x322540,PermissionsRva=0x32BDB0,StrideBytes=0x168;
        private readonly int[] profiles,permissions;
        private NativePathfindingTableCopy(int[] p,int[] c){profiles=p;permissions=c;}
        internal int[] CopyProfiles()=>(int[])profiles.Clone();
        internal int[] CopyPermissions()=>(int[])permissions.Clone();
        internal int? Profile(int type)=>(uint)type<Types?(int?)profiles[type]:null;
        internal bool? Permission(int kind,int type)
        {if(kind<1||kind>Classes||(uint)type>=Types)return null;int v=permissions[(kind-1)*Types+type];return v==0?false:v==1?true:(bool?)null;}
        internal bool SameContent(NativePathfindingTableCopy other)
        {if(other==null)return false;for(int i=0;i<profiles.Length;i++)if(profiles[i]!=other.profiles[i])return false;for(int i=0;i<permissions.Length;i++)if(permissions[i]!=other.permissions[i])return false;return true;}
        internal static bool TryCapture(string hash,int moduleLength,Func<int,int> read,out NativePathfindingTableCopy result,out string reason)
        {
            result=null;reason="unvalidated-table-source";
            if(hash!=Hash||read==null||moduleLength<PermissionsRva+Types*Classes*4||moduleLength<ProfileRva+Types*4)return false;
            try
            {
                var p=new int[Types];var c=new int[Types*Classes];
                for(int i=0;i<p.Length;i++)p[i]=read(ProfileRva+i*4);for(int i=0;i<c.Length;i++)c[i]=read(PermissionsRva+i*4);
                for(int i=0;i<p.Length;i++)if(p[i]!=read(ProfileRva+i*4)){reason="profile-table-changed-during-copy";return false;}
                for(int i=0;i<c.Length;i++)if(c[i]!=read(PermissionsRva+i*4)){reason="permission-table-changed-during-copy";return false;}
                result=new NativePathfindingTableCopy(p,c);reason="copied-90-profile-540-permission-values";return true;
            }
            catch(Exception ex){reason="table-read-failed:"+ex.GetType().Name;return false;}
        }
    }
}
