namespace TrappedUnitRescueTest
{
    internal sealed class WorkerBindingSnapshot
    {
        internal static readonly WorkerBindingSnapshot None=new WorkerBindingSnapshot(WorkerExternalBindings.None,0,0,0,0,0,0);
        internal bool IsEmpty=>Required==WorkerExternalBindings.None && WorkplaceId==0 && WorkplaceGlobal==0 &&
            StoreId==0 && StoreGlobal==0 && VegetationId==0 && VegetationGlobal==0;
        internal readonly WorkerExternalBindings Required;
        internal readonly int WorkplaceId,StoreId,VegetationId;
        internal readonly uint WorkplaceGlobal,StoreGlobal,VegetationGlobal;
        internal WorkerBindingSnapshot(WorkerExternalBindings required,int home,uint homeGlobal,int store,uint storeGlobal,int vegetation,uint vegetationGlobal)
        {Required=required;WorkplaceId=home;WorkplaceGlobal=homeGlobal;StoreId=store;StoreGlobal=storeGlobal;VegetationId=vegetation;VegetationGlobal=vegetationGlobal;}
        internal bool MatchesSchema(WorkerJourneySnapshot job)
        {
            if(job==null || Required!=job.Rule.RequiredExternalBindings || (Required&WorkerExternalBindings.LinkedEntity)!=0)return false;
            return Reference(job,WorkerExternalBindings.Workplace,0x990,0x9C0,WorkplaceId,WorkplaceGlobal) &&
                Reference(job,WorkerExternalBindings.Store,0x98E,0x6F8,StoreId,StoreGlobal) &&
                ((Required&WorkerExternalBindings.Vegetation)==0 ? VegetationId==0 && VegetationGlobal==0 :
                VegetationId>0 && job.TryGetValue(0x9A0,out var tree) && tree==(uint)VegetationId &&
                (job.Rule.VegetationSerialNativeOffset==0 || job.TryGetValue(job.Rule.VegetationSerialNativeOffset,out var serial) && serial==VegetationGlobal));
        }
        private bool Reference(WorkerJourneySnapshot job,WorkerExternalBindings bit,int idOffset,int globalOffset,int id,uint global)
        {
            if((Required&bit)==0)return id==0 && global==0;
            return id>0 && job.TryGetValue(idOffset,out var actualId) && actualId==(uint)id &&
                job.TryGetValue(globalOffset,out var actualGlobal) && actualGlobal==global;
        }
    }
}
