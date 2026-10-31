#if SERP_DEVDIAG
using System;
namespace LordOverviewTest {
 internal static class OverviewAllocationProbe {
  private static readonly Func<long> read=CreateReader();
  private static Func<long> CreateReader(){var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);return method==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);}
  internal static long Read()=>read==null?-1:read();
 }
}
#endif
