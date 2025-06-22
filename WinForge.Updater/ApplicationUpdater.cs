namespace WinForge.Updater
{
    public static class Application
    {
        /// <summary> True if there is a new version available to download </summary>        
        public static bool IsUpdateAvailable { get; private set; } = false;
        /// <summary> Version of the update available to download </summary>        
        public static string UpdateVersion { get; private set; } = string.Empty;
        /// <summary> True if the update is downloaded and ready to apply </summary>        
        public static bool IsUpdatePending { get; private set; } = false;

        /// summary> Triggers UpdateAvailable event if an update is available.
        public static event EventHandler? UpdateAvailable;

        /// <summary> Triggers UpdatePending event if an update is downloaded and ready to apply. </summary>       
        public static event EventHandler? UpdatePending;

        /// <summary> Triggers UpdateApplied event if the update is successfully applied. </summary>        
        public static event EventHandler? UpdateApplied;

        /// <summary> Triggers DownloadComplete event when the update download is complete. </summary>        
        public static event EventHandler? DownloadComplete;

        /// <summary> Checks for an update and sets the IsUpdateAvailable property accordingly. </summary>        
        public static async Task CheckForUpdate()
        {
        }
        /// <summary> Downloads the update if available and sets the IsUpdatePending property accordingly. </summary>       
        static async Task DownloadUpdate() { }
        /// <summary> Applies the update if it is downloaded and clears the IsUpdatePending property if successful. </summary>       
        public static async Task ApplyUpdate() { }
    }
}
