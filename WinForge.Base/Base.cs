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
            //ToDo: Initialize settings and configuration
            //ToDo: Load updater module here
            await Common.Logger.InitializeAsync();
            Client.RegisterListener(IPCPipeName, IPCMessageReceived);
            HTTPManager.StartServer(IPCPipeName);
            //ToDo: Load Core module
            modules = ModuleLoader.LoadModules();
        }
        private static void IPCMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            IPCMessage message = e.Message;
            if (message.To == IPCPipeName)
            {
                //ToDo: Handle IPC messages here
            }
            else
            {
                if (HTTPManager.GetConnectedTCPClientPipeNames().Contains(message.To))
                {
                    HTTPManager.SendToClient(message.To, message);
                }
                else
                {
                    Logger.Log($"Received message for unknown pipe: {message.To}", Logger.LogLevel.Warning);
                }


            }
        }

        public static void Stop()
        {
            running = false;
            Console.WriteLine("Stopping WinForge...");
        }
    }
}
