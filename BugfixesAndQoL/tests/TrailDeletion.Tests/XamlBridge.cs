using System;
using System.Collections.Generic;
namespace SHCDESE.Logging {
    public static class LogHelper {
        public static void Information(string message) { }
        public static void Warning(string message) { throw new Exception(message); }
        public static void Error(Exception exception, string message) { throw new Exception(message, exception); }
    }
}
public static class TrailXamlBridge {
    public static string Apply(string source, List<SHCDESE.API.Components.Noesis.XAML.XmlPatchOperation> operations) =>
        SHCDESE.API.Components.Noesis.XAML.XamlPatcher.ApplyPatches(source, operations);
}
