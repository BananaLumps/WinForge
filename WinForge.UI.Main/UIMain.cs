using WinForge.Common;
using WinForge.IPC;

namespace WinForge.UI.Main
{
    public class UIMain
    {
        ICommunication Communication = new IPC.Client();
        Logger? Logger = null;
        public void Initialize(DependencyService dependencyService)
        {
            // Initialize the communication channel
            Logger = dependencyService.GetDependency<Logger>();
            Communication.PipeName = "WinForge.UI.Main";
            Communication.RegisterListener("WinForge.UI.Main", IPCMessageReceived, IPCResponseReceived, IPCCommandReceived);
            Logger?.Log($"Initializing module WinForge.UI.Main...", LogLevel.Info);
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
    }
}
