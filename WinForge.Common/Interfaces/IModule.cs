using WinForge.Common;

namespace WinForge
{
    public interface IModule
    {
        /// <summary> The name of the module. This will be use as the modules pipe name for IPC.</summary>
        public string Name { get; }
        /// <summary> The current version of the module.</summary>
        public string Version { get; }
        /// <summary> The status of the module.</summary>
        public ModuleStatus Status { get; }
        /// <summary> Used to initialize the module with the dependency service. The module should register itself with the dependency service if it intends to allow others to interact with it</summary>
        public void Initialize(IDependencyService dependencyService);
        /// <summary> List of dependencies that this module requires to function. This is used to ensure that all dependencies are loaded before the module starts.</summary>
        public List<string> Dependencies { get; }
        /// <summary> Called when the module is stopped. </summary>
        public void Stop();

    }
}
