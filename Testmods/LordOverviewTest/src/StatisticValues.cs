namespace LordOverviewTest {
 internal static class StatisticValues {
  internal static long? At(int[] values,int player)=>values!=null && player>=0 && player<values.Length?(long?)values[player]:null;
  // DLL_GetMeritData: nine five-Int32 records; column 1 is the exact signed Vanilla score.
  internal static long? GreatestLord(int[] merit,int player)=>merit!=null && player>=1 && player<=8 && merit.Length>player*5+1 && merit[player*5]>0?(long?)merit[player*5+1]:null;
  internal static long? Kills(int[] matrix,int player,bool losses){if(matrix==null || matrix.Length<81)return null;long total=0;
   // Vanilla: victims 1..8 for kills; killers 0..8 for losses (environment included).
   for(int other=losses?0:1;other<=8;other++)if(other!=player)total+=matrix[losses?other*9+player:player*9+other];return total;}
  internal static long? Popularity(uint raw)=>raw<=10000?(long?)(raw/100):null;
  internal static long? Fear(int raw)=>raw>=-5 && raw<=5?(long?)raw:null;
 }
}
