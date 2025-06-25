using WinForge.Common;

namespace WinForge
{
    public interface IModule
    {
        public string Name { get; }
        public string Version { get; }
        public int Status { get; }
        public void Initialize(IDependencyService dependencyService);
    }
}
