using System.IO.Compression;
using WinForge.Common;
using WinForge.IPC;

namespace WinForge.Base
{
    class Base
    {
        private const string IPCPipeName = "WinForge.Base";
        public static bool running = true;
        static List<IModule> modules = [];
        static async Task Main()
        {
            Settings.Persistence.Load();
            ReplaceUpdater();
            await Common.Logger.InitializeAsync();
            Client.RegisterListener(IPCPipeName, IPCMessageReceived, IPCMessageReceived);
            HTTPManager.StartServer(IPCPipeName);
            //ToDo: Load Core module
            modules = ModuleLoader.LoadModules();
        }
        private static void IPCMessageReceived(object? sender, IPCMessage e)
        {
            IPCMessage message = e;
            if (message.To == IPCPipeName)
            {
                //ToDo: Handle IPC messages here
            }
        }
        private static void IPCResponseReceived(object? sender, IPCMessage e)
        {
            IPCMessage message = e;
            if (message.To == IPCPipeName)
            { }
        }

        public static void Stop()
        {
            running = false;
            Console.WriteLine("Stopping WinForge...");
        }
        /// <summary> Replaces the Updater.exe with a new version from Updater.zip in the base directory. </summary>        
        private static void ReplaceUpdater()
        {
            string baseDir = AppContext.BaseDirectory;
            string zipPath = Path.Combine(baseDir, "Updater.zip");
            string exePath = Path.Combine(baseDir, "Updater.exe");

            if (!File.Exists(zipPath))
                return; // Nothing to do

            string tempDir = Path.Combine(Path.GetTempPath(), $"UpdaterReplace_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(tempDir);

                // 1. Extract the ZIP
                ZipFile.ExtractToDirectory(zipPath, tempDir);

                string newExe = Path.Combine(tempDir, "Updater.exe");
                if (!File.Exists(newExe))
                    throw new FileNotFoundException("Updater.exe not found in ZIP.");

                // 2. Delete the current Updater.exe (if it's not locked/running)
                if (File.Exists(exePath))
                {
                    try
                    {
                        File.Delete(exePath);
                    }
                    catch (IOException ioEx)
                    {
                        // Likely in use; log and abort
                        Logger.Log($"Could not delete existing Updater.exe: {ioEx.Message}", Logger.LogLevel.Error, "ReplaceUpdater");
                        return;
                    }
                }

                // 3. Move the new Updater.exe into place
                File.Move(newExe, exePath);

                Logger.Log("Updater.exe successfully replaced.", Logger.LogLevel.Info, "ReplaceUpdater");
            }
            catch (Exception ex)
            {
                Logger.Log($"{ex.Message}", Logger.LogLevel.Error, "ReplaceUpdater");
            }
            finally
            {
                // 4. Clean up
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }
    }
}
