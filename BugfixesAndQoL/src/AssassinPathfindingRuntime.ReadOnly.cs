using System;
using APIShared;
namespace BugfixesAndQoL
{
    internal sealed unsafe partial class AssassinPathfindingRuntime
    {
        private readonly ReadOnlyTraversal[] readOnlyTraversalViews=new ReadOnlyTraversal[9];
        private IAssassinTraversalView CaptureReadOnlyTraversal(int player, int speed)
        {
            if (!IsInstalled || !settings.EnableMod || !settings.EnableImprovedAssassinPathfinding ||
                player < 1 || player > 8 || !EnsureCoordinateTileMappingValidated() ||
                !AssassinGateRoutePolicy.TryCapture(EnemyGatePathPolicyBridge.Current, player, out IEnemyGateRoutePolicySnapshot policy)) return null;
            ReadOnlyTraversal cached=readOnlyTraversalViews[player];
            if(cached!=null && cached.Speed==speed && ReferenceEquals(cached.Policy,policy) && cached.ValidateTopology()) return cached;
            return readOnlyTraversalViews[player]=new ReadOnlyTraversal(this, player, speed, policy);
        }
        private sealed class ReadOnlyTraversal : IAssassinTraversalView
        {
            private readonly AssassinPathfindingRuntime runtime;
            internal readonly int Speed;
            internal IEnemyGateRoutePolicySnapshot Policy=>policy;
            private readonly int player, epoch, cardinal, diagonal;
            private readonly bool climb, reserved, direct;
            private readonly IEnemyGateRoutePolicySnapshot policy;
            private readonly IEnemyGatePathPolicy provider;
            private readonly uint[] flagsSnapshot;
            private readonly byte[] connectionsSnapshot, heightsSnapshot, specialSnapshot;
            private readonly ushort[] buildingsSnapshot;
            private readonly ulong[] identities;
            private readonly uint[] globals;
            internal ReadOnlyTraversal(AssassinPathfindingRuntime runtime, int player, int speed, IEnemyGateRoutePolicySnapshot policy)
            {
                this.runtime=runtime; this.player=player; this.policy=policy; provider=EnemyGatePathPolicyBridge.Current; Speed=speed; epoch=runtime.mapEpoch;
                flagsSnapshot=new ReadOnlySpan<uint>(runtime.tileFlags,TileCount).ToArray();
                connectionsSnapshot=new ReadOnlySpan<byte>(runtime.occupancyLayer,TileCount).ToArray();
                buildingsSnapshot=new ReadOnlySpan<ushort>(runtime.buildingLayer,TileCount).ToArray();
                heightsSnapshot=new ReadOnlySpan<byte>(runtime.heightLayer,TileCount).ToArray();
                specialSnapshot=new byte[TileCount];
                for(int tile=0;tile<TileCount;tile++) if((flagsSnapshot[tile]&NativeSpecialTileFlag)!=0)
                    specialSnapshot[tile]=runtime.specialTilePredicate(IntPtr.Add(runtime.libraryHandle,SpecialTilePredicateContextRva),tile);
                var records=SHCDESE.API.GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                identities=new ulong[records.Length]; globals=new uint[records.Length];
                for(int i=0;i<records.Length;i++) { identities[i]=Identity(in records[i]); globals[i]=records[i].r_GlobalId; }
                climb=runtime.climbRuntime.IsClimbingAllowed(player); reserved=runtime.reconstructionPatch?.IsApplied==true;
                direct=AssassinPathAPI.DirectGatehouseClimbingEnabled;
                cardinal=AssassinClimbCostPolicy.GetCardinalMovementTicks(speed);
                diagonal=AssassinClimbCostPolicy.GetDiagonalMovementTicks(speed);
            }
            public bool IsCurrent => epoch==runtime.mapEpoch && runtime.settings.EnableMod &&
                runtime.settings.EnableImprovedAssassinPathfinding && climb==runtime.climbRuntime.IsClimbingAllowed(player) &&
                reserved==(runtime.reconstructionPatch?.IsApplied==true) && direct==AssassinPathAPI.DirectGatehouseClimbingEnabled &&
                AssassinGateRoutePolicy.IsPublicationCurrent(provider,policy,EnemyGatePathPolicyBridge.Current);
            private static ulong Identity(in SHCDESE.Interop.GameBuilding b) => (ulong)(ushort)b.r_AliveState |
                ((ulong)(ushort)b.r_BuildingType<<16) | ((ulong)b.r_PlayerIdOwner<<32) | ((ulong)b.r_CapturedByPlayerId<<48);
            public bool ValidateTopology()
            {
                if(!IsCurrent || !new ReadOnlySpan<uint>(runtime.tileFlags,TileCount).SequenceEqual(flagsSnapshot) ||
                    !new ReadOnlySpan<byte>(runtime.occupancyLayer,TileCount).SequenceEqual(connectionsSnapshot) ||
                    !new ReadOnlySpan<ushort>(runtime.buildingLayer,TileCount).SequenceEqual(buildingsSnapshot) ||
                    !new ReadOnlySpan<byte>(runtime.heightLayer,TileCount).SequenceEqual(heightsSnapshot)) return false;
                for(int tile=0;tile<TileCount;tile++) if((flagsSnapshot[tile]&NativeSpecialTileFlag)!=0 && specialSnapshot[tile]!=
                    runtime.specialTilePredicate(IntPtr.Add(runtime.libraryHandle,SpecialTilePredicateContextRva),tile)) return false;
                var records=SHCDESE.API.GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                if(records.Length!=identities.Length) return false;
                for(int i=0;i<records.Length;i++) if(Identity(in records[i])!=identities[i] || globals[i]!=records[i].r_GlobalId) return false;
                return IsCurrent;
            }
            public bool TryGetEdgeCost(int x,int y,int nx,int ny,out int cost)
            {
                cost=0;
                if (!IsCurrent || !runtime.IsValidCoordinate(x,y) || !runtime.IsValidCoordinate(nx,ny)) return false;
                int direction=GetDirectionIndex(nx-x,ny-y);
                if (direction<0) return false;
                int from=runtime.GetTileId(x,y), to=runtime.GetTileId(nx,ny);
                if (!IsNativeTile(from) || !IsNativeTile(to)) return false;
                bool ground=runtime.HasOrdinaryConnection(from,to,direction);
                if (!ground && ((direction&1)!=0 || !climb ||
                    !runtime.IsVanillaAssassinFallback(from,to,runtime.tileFlags[from],reserved))) return false;
                // Use the functional validator directly: no diagnostic observation or path publication.
                if (!runtime.EvaluateAssassinTransition(policy,from,to,direction,climb,reserved,out _)) return false;
                int move=(direction&1)==0 ? cardinal : diagonal;
                int extra=ground ? 0 : runtime.GetClimbTicks(from,to);
                cost=move>int.MaxValue-extra ? int.MaxValue : move+extra;
                return true;
            }
        }
    }
}
