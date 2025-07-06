using WinForge.Common;
using WinForge.IPC;

namespace WinForge.UI.Main
{
    public class UIMain
    {
        public static ICommunication Communication = new IPC.Client();
        public DependencyService DependencyService { get; private set; } = new DependencyService();
        Logger? Logger = null;
        public void Initialize(DependencyService dependencyService)
        {
            // Initialize the communication channel
            Logger = dependencyService.GetDependency<Logger>();
            Communication.PipeName = "WinForge.UI.Main";
            Communication.RegisterListener("WinForge.UI.Main", IPCMessageReceived, IPCResponseReceived, IPCCommandReceived);
            Logger.Log($"Initializing module WinForge.UI.Main...", LogLevel.Info, "WinForge.UI.Main");
        }
        private void IPCCommandReceived(object? sender, IPCMessage e)
        {
            string lowerMessage = e.Message.ToLowerInvariant();
            switch (lowerMessage)
            {
                case "showForm":
                    ShowForm((Form)e.Data[0]);
                    break;
                default:
                    break;
            }
        }

        private void IPCResponseReceived(object? sender, IPCMessage e)
        {
            throw new NotImplementedException();
        }

        private void IPCMessageReceived(object? sender, IPCMessage e)
        {
            throw new NotImplementedException();
        }
        private void ShowForm(Form form)
        {
            form.Show();

        }
        public UIMain()
        {
        }
        public UIMain(DependencyService dependencyService)
        {
            Initialize(dependencyService);
        }
    }
}
