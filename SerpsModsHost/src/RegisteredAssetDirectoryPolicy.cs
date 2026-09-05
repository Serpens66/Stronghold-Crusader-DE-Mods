namespace SerpsModsHost
{
    internal static class RegisteredAssetDirectoryPolicy
    {
        internal static bool TryValidate(
            string expectedDirectory,
            bool guidIsRegistered,
            string registeredDirectory,
            out string failure)
        {
            if (!guidIsRegistered)
            {
                failure = "The Script Extender did not expose the registered GUID afterwards.";
                return false;
            }
            if (!DuplicateInstallationDetector.PathsEqual(expectedDirectory, registeredDirectory))
            {
                failure = $"The GUID is already registered from a different directory: {registeredDirectory}";
                return false;
            }

            failure = null;
            return true;
        }
    }
}
