// Access to Vanilla's engine monitor.
using CrusaderDE;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    // EngineInterface.threadLock serializes DLL_RunTick with Vanilla's UI-to-engine calls.
    // Direct simulation changes from the main thread must hold it too.
    internal static class EngineThreadLock
    {
        private static object gate;

        internal static object Gate => gate ?? (gate = Resolve());

        private static object Resolve()
        {
            FieldInfo field = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(object))
                throw new MissingFieldException("EngineInterface.threadLock");
            return field.GetValue(null) ?? throw new InvalidOperationException("Engine lock is null.");
        }
    }
}
