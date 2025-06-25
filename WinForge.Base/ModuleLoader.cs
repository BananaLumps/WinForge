using System.Reflection;
using WinForge.Common;

namespace WinForge.Base
{
    public static class ModuleLoader
    {
        /// <summary> Loads modules from the specified directory and initializes them in dependency order.</summary>
        public static void Initialize(DependencyService dependencyService)
        {
            InitializeModules(DependencyOrderedList(LoadModules()), dependencyService);
            Logger.Instance.Log("ModuleLoader initialized.", LogLevel.Info, "ModuleLoader");
        }
        /// <summary> Loads modules from the specified directory and creates an instance.</summary>
        public static List<IModule> LoadModules(string path = "./modules")
        {
            var modules = new List<IModule>();

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Module directory not found: {path}");
                return modules;
            }

            string[] dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);

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
                            if (modules.Any(m => m.Name == module.Name))
                            {
                                Logger.Instance.Log($"Duplicate module found: {module.Name} v{module.Version} Skipping.", LogLevel.Warning, "ModuleLoader");
                                continue;
                            }
                            modules.Add(module);
                            Logger.Instance.Log($"Loaded module: {module.Name} v{module.Version}", LogLevel.Info, "ModuleLoader");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Error loading from {dllPath}: {ex.Message}", LogLevel.Error, "ModuleLoader");
                }
            }

            return modules;
        }

        /// <summary> Returns a dependency-ordered list of modules.</summary>
        public static Queue<IModule> DependencyOrderedList(List<IModule> modules)
        {
            var result = new Queue<IModule>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            var moduleMap = modules.ToDictionary(m => m.Name);

            foreach (var module in modules)
            {
                if (!visited.Contains(module.Name))
                {
                    if (!VisitModule(module))
                    {
                        throw new Exception($"Circular dependency detected while processing module: {module.Name}");
                    }
                }
            }

            return result;

            bool VisitModule(IModule module)
            {
                if (visiting.Contains(module.Name))
                {
                    return false; // Circular dependency detected
                }

                if (visited.Contains(module.Name))
                {
                    return true; // Already processed
                }

                visiting.Add(module.Name);

                // Process dependencies first
                foreach (var dependencyName in module.Dependencies)
                {
                    if (!moduleMap.TryGetValue(dependencyName, out var dependency))
                    {
                        throw new Exception($"Missing dependency '{dependencyName}' required by module '{module.Name}'");
                    }

                    if (!VisitModule(dependency))
                    {
                        return false;
                    }
                }

                visiting.Remove(module.Name);
                visited.Add(module.Name);
                result.Enqueue(module);
                return true;
            }
        }
        /// <summary> Initializes the modules in the specified order.</summary>
        public static void InitializeModules(Queue<IModule> modules, DependencyService dependencyService)
        {
            while (modules.Count > 0)
            {
                var module = modules.Dequeue();
                module.Initialize(dependencyService);
            }
        }
    }
}