using System;
using System.Threading;

namespace LordOverviewTest {
 // Both production and fixtures execute this exact nonblocking critical section.
 internal sealed class PreparedRead {
  private readonly object gate;
  private readonly Action copy;
  private int reading;
  internal PreparedRead(object gate,Action copy){this.gate=gate;this.copy=copy;}
  internal bool TryRead(){
   if(Interlocked.CompareExchange(ref reading,1,0)!=0)return false;
   bool taken=false;
   try {Monitor.TryEnter(gate,ref taken);if(!taken)return false;copy();return true;}
   finally {if(taken)Monitor.Exit(gate);Volatile.Write(ref reading,0);}
  }
 }

 internal struct ResourceSnapshot {internal bool Present;internal uint Popularity;internal int Fear;}
 // Private working storage, never delivered to providers or the view. Prepare runs before TryEnter.
 internal sealed class RawSnapshot {
  internal readonly int[] Merit=new int[9*5];
  internal readonly ResourceSnapshot[] Resources=new ResourceSnapshot[9];
  internal readonly byte[] History;
  internal bool HasHistory,LocalValid,Eliminated;
  internal int Controlled;
  internal RawSnapshot(int historyBytes){History=new byte[historyBytes];}
  internal void Prepare(bool historyDue,RawSnapshot previous){
   Array.Clear(Merit,0,Merit.Length);Array.Clear(Resources,0,Resources.Length);Controlled=0;LocalValid=Eliminated=false;
   if(historyDue)Array.Clear(History,0,History.Length);else Buffer.BlockCopy(previous.History,0,History,0,History.Length);
   HasHistory=historyDue || previous.HasHistory;
  }
  internal void Clear(){Array.Clear(Merit,0,Merit.Length);Array.Clear(Resources,0,Resources.Length);Array.Clear(History,0,History.Length);HasHistory=LocalValid=Eliminated=false;Controlled=0;}
 }

 // OnTick only counts. All clock/cadence decisions belong to the GUI/main thread.
 internal sealed class HybridRefreshGate {
  private long ticks,served,attemptTicks;
  private int requested,attemptRequest;
  private bool retry,tried,working;
  private double lastAttempt,lastSuccess;
  internal void Tick(bool visible){if(visible)Interlocked.Increment(ref ticks);}
  internal void Request(){requested++;}
  internal void Reset(double now){Interlocked.Exchange(ref ticks,0);served=attemptTicks=0;requested=attemptRequest=0;retry=tried=working=false;lastAttempt=lastSuccess=now;}
  internal void Cancel(){served=Interlocked.Read(ref ticks);attemptRequest=requested;retry=false;}
  internal bool TryBegin(double now){
   if(working || tried && now-lastAttempt<2)return false;
   long current=Interlocked.Read(ref ticks);
   if(requested==attemptRequest && !retry && current-served<200 && (current==served || now-lastSuccess<5))return false;
   tried=working=true;lastAttempt=now;attemptTicks=current;attemptRequest=requested;return true;
  }
  internal void End(double now,bool success){working=false;retry=!success;if(success){served=attemptTicks;lastSuccess=now;}}
 }
}
