using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using WinForge.Common;
using WinForge.IPC;
using WinForge.UI.Main;


namespace WinForge.Base
{
    class Base
    {
        private const string IPCPipeName = "WinForge.Base";
        public static bool running = true;
        static ICommunication Communication = new IPC.Client();
        static Client IPC = new Client();
        static List<IModule> modules = [];
        static PipeMessenger? pipeMessenger = null;
        static DependencyService dependencyService = new DependencyService();
        public static ILogger? Logger;
        public static bool Headless = false; // Set to true if you want to run without a UI
        public static Form? MainForm = null; // Reference to the main form if needed
        static bool runOnce = false;
        public static async Task Initialize()
        {
            Settings.Persistence.LoadApplicationSettings();
            Settings.Persistence.LoadUserSettings();

            ReplaceUpdater();
            //ToDo:Run Updater

            dependencyService.Register<Logger>((Logger)Logger! ?? new Logger());
            Logger = dependencyService.GetDependency<Logger>();
            Communication.PipeName = "WinForge.Base";
            pipeMessenger = Communication.RegisterListener(IPCPipeName, IPCMessageReceived, IPCResponseReceived, IPCCommandReceived);

            HTTPManager.StartServer(IPCPipeName);

            modules = ModuleLoader.Initialize(dependencyService);

            WaitForMainFormWithCancellation();
            var method = typeof(MainForm).GetMethod("Init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && MainForm != null)
            {
                method.Invoke(MainForm, new object[] { dependencyService });
            }

            await RunMainLoopAsync();
        }
        public static Form? WaitForMainFormWithCancellation(int timeoutMilliseconds = 5000)
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(timeoutMilliseconds);
            var token = cts.Token;

            while (!token.IsCancellationRequested)
            {
                var form = Application.OpenForms["MainForm"];
                if (form != null)
                    return form;

                Application.DoEvents(); // Optional for responsiveness
                Thread.Sleep(50); // Light polling
            }
            Logger.Log("MainForm not found within the timeout period.", LogLevel.Warning, "WinForge.Base");
            return null; // Timed out
        }
        public static void Main()
        {

            if (!Headless)
            {
                // Create a separate thread for running the UI with STA
                Thread uiThread = new Thread(() =>
                {
                    RunUI();
                });
                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();

                // Run Initialize on the main thread
                Initialize().GetAwaiter().GetResult();
            }
        }
        [STAThread]

        private static void RunUI()
        {
            // Load the assembly
            var assembly = Assembly.LoadFrom("WinForge.UI.Main.dll");

            // Get the Form1 type
            var formType = assembly.GetType("WinForge.UI.Main.MainForm");

            // Create an instance of the form
            MainForm = (Form?)Activator.CreateInstance(formType!);

            // Start the WinForms message loop with the form
            Application.EnableVisualStyles();
            // Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(MainForm!);


        }
        private static async Task RunMainLoopAsync()
        {
            while (running)
            {
                await Task.Delay(100);
            }
        }
        private static void IPCMessageReceived(object? sender, IPCMessage e)
        {
            Logger.Log($"Received message: {e.Message}", LogLevel.Info, "IPCMessageReceived");
            IPCMessage message = e;
            if (message.To == IPCPipeName)
            {
                //ToDo: Handle IPC messages here
            }
        }
        private static void IPCResponseReceived(object? sender, IPCMessage e)
        {
            Logger.Log($"Received response: {e.Message}", LogLevel.Info, "IPCResponseReceived");
            IPCMessage message = e;
            if (message.To == IPCPipeName)
            {
                Logger.Log($"Received response with ID: {message.ResponseId}", LogLevel.Info, "WinForge.Base.IpcResponseReceived");
            }
        }
        private static void IPCCommandReceived(object? sender, IPCMessage e)
        {
            Logger.Log($"Received command: {e.Message}", LogLevel.Info, "WinForge.Base.IPCCommandReceived");
            IPCMessage message = e;
            bool valid = false;
            if (message.To == IPCPipeName || message.To == "WinForge.UI.Main")
            {
                switch (message.Message.ToLowerInvariant())
                {
                    case "stop":
                        valid = true;
                        Stop();
                        break;
                    case "showForm":
                        Logger.Log($"Received command to show form: {message.Data[0]}", LogLevel.Info, "WinForge.Base");
                        valid = true;
                        break;
                    case "replaceupdater":
                        valid = true;
                        ReplaceUpdater();
                        break;
                    case "version":
                        valid = true;
                        Communication.SendMessageAsync(new IPCMessage(message, $"{Assembly.GetExecutingAssembly().GetName().Version?.ToString()}"));
                        break;
                    default:
                        valid = false;
                        Logger.Log($"Unknown command: {message.Message}", LogLevel.Warning, "WinForge.Base");
                        break;
                }
            }
            if (valid) Logger.Log($"Received and processed command: {message.Message}", LogLevel.Info, "WinForge.Base");
        }
        public static void Stop()
        {
            running = false;
            Logger.Log("Stopping WinForge...", LogLevel.Info, "WinForge.Base");
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
                        Logger.Log($"Could not delete existing Updater.exe: {ioEx.Message}", LogLevel.Error, "WinForge.Base");
                        return;
                    }
                }

                // 3. Move the new Updater.exe into place
                File.Move(newExe, exePath);

                Logger.Log("Updater.exe successfully replaced.", LogLevel.Info, "WinForge.Base");
            }
            catch (Exception ex)
            {
                Logger.Log($"{ex.Message}", LogLevel.Error, "WinForge.Base");
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
