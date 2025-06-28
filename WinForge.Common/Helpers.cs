namespace WinForge.Common
{
    public static class Helpers
    {
        /// <summary>
        /// Gets the current version of the application.
        /// If a version file is present, it reads the version from there; otherwise, it returns a default version.
        /// </summary>
        /// <returns>The current version as a string.</returns>
        public static string GetVersionFromFile()
        {
            try
            {
                string versionPath = Path.Combine(AppContext.BaseDirectory, "version.txt");
                if (File.Exists(versionPath))
                {
                    string version = File.ReadAllText(versionPath).Trim();
                    return string.IsNullOrEmpty(version) ? string.Empty : version;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error($"Failed to read version file: {ex.Message}");
            }
            return string.Empty;
        }
    }
}
