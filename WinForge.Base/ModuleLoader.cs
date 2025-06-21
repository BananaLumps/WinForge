using System.Reflection;

namespace WinForge.Base
{
    public static class ModuleLoader
    {
        public static List<IModule> LoadModules(string path = "./modules")
        {
            var modules = new List<IModule>();

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Module directory not found: {path}");
                return modules;
            }

            string[] dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (string dllPath in dllFiles)
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllPath);

                    var moduleTypes = assembly.GetTypes()
                        .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in moduleTypes)
                    {
                        if (Activator.CreateInstance(type) is IModule module)
                        {
                            module.Initialize();
                            modules.Add(module);
                            Console.WriteLine($"Loaded module: {module.Name} v{module.Version}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading from {dllPath}: {ex.Message}");
                }
            }

            return modules;
        }
    }
}