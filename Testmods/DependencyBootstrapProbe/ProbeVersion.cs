namespace DependencyBootstrapProbe
{
    internal static class ProbeVersion
    {
#if UPDATE_B
        internal const string Value = "0.1.2";
        internal const string Assembly = "0.1.2.0";
#else
        internal const string Value = "0.1.1";
        internal const string Assembly = "0.1.1.0";
#endif
    }
}
