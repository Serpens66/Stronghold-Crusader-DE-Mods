namespace ExtendedData
{
    // One immutable publication separates content authority from personal authoring tools.
    internal sealed class ExtendedDataFeatureActivation
    {
        internal static readonly ExtendedDataFeatureActivation Disabled =
            new ExtendedDataFeatureActivation(false, false, false, false, false, false, false);

        internal ExtendedDataFeatureActivation(bool host, bool client, bool mission, bool coop,
            bool lords, bool editor, bool workshop)
        {
            MissionModSettings = host && mission;
            CoopPackages = host && coop;
            LordDataIntegration = host && lords;
            EditorFiles = client && editor;
            WorkshopUploads = client && workshop;
        }

        internal bool MissionModSettings { get; }
        internal bool CoopPackages { get; }
        internal bool LordDataIntegration { get; }
        internal bool EditorFiles { get; }
        internal bool WorkshopUploads { get; }
    }
}
