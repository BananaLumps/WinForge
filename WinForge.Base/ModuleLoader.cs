using System.Reflection;
using System.Text.RegularExpressions;
using WinForge.Common;

namespace WinForge.Base
{
    public static class ModuleLoader
    {
        /// <summary> Loads modules from the specified directory and initializes them in dependency order.</summary>
        public static List<IModule> Initialize(DependencyService dependencyService)
        {
            var modules = DependencyOrderedList(LoadModules());
            LoadModulesToAppDomain(modules.ToList());
            InitializeModules(modules, dependencyService);
            Logger.Instance.Log("ModuleLoader initialized.", LogLevel.Info, "WinForge.Common.ModuleLoader");
            return modules.ToList();
        }

        /// <summary> Loads modules from the specified directory and creates an instance.</summary>
        public static List<IModule> LoadModules(string path = "./modules")
        {
            var modules = new List<IModule>();

            if (!Directory.Exists(path))
            {
                Logger.Instance.Log($"Module directory not found: {path}", LogLevel.Info, "WinForge.Common.ModuleLoader");
                return modules;
            }

            string[] dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);

            foreach (string dllPath in dllFiles)
            {
            Try:
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
                                Logger.Instance.Log($"Duplicate module found: {module.Name} v{module.Version} Skipping.", LogLevel.Warning, "WinForge.Common.ModuleLoader");
                                continue;
                            }
                            modules.Add(module);

                            Logger.Instance.Log($"Loaded module: {module.Name} v{module.Version}", LogLevel.Info, "WinForge.Common.ModuleLoader");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Error loading from {dllPath}: {ex.Message}. Attempting to resolve", LogLevel.Warning, "WinForge.Common.ModuleLoader");

                    if (string.IsNullOrWhiteSpace(ex.Message))
                        return null;

                    // This regex looks for: 'Assembly.Name, Version=...'
                    var match = Regex.Match(ex.Message, @"'([^',]+),\s*Version=");
                    LoadFromExternalLibrary(match.Groups[1].Value + ".dll");
                    goto Try;
                }
            }
            return modules;
        }
        public static void LoadModulesToAppDomain(List<IModule> modules)
        {
            foreach (var module in modules)
            {
                try
                {
                    // Path to the DLL based on module name
                    string dllPath = $"./modules/{module.Name}.dll";

                    if (!File.Exists(dllPath))
                    {
                        Logger.Instance.Log($"Module DLL not found: {dllPath}", LogLevel.Error, "WinForge.Common.ModuleLoader");
                        continue;
                    }

                    // Check if already loaded to avoid duplicates (optional)
                    var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                        .Any(a => string.Equals(Path.GetFileNameWithoutExtension(a.Location), module.Name, StringComparison.OrdinalIgnoreCase));

                    if (alreadyLoaded)
                    {
                        Logger.Instance.Log($"Module already loaded: {module.Name}", LogLevel.Info, "WinForge.Common.ModuleLoader");
                        continue;
                    }

                    // Load the DLL into the current AppDomain
                    Assembly.LoadFrom(dllPath);
                    Logger.Instance.Log($"Loaded module assembly: {module.Name}", LogLevel.Info, "WinForge.Common.ModuleLoader");
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to load module '{module.Name}': {ex.Message}", LogLevel.Info, "WinForge.Common.ModuleLoader");
                }
            }
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

        /// <summary>
        /// Loads a DLL from the ./ExternalLibraries folder.
        /// </summary>
        /// <param name="dllName">The name of the DLL (with or without .dll extension).</param>
        /// <returns>The loaded Assembly, or null if it could not be loaded.</returns>
        public static Assembly? LoadFromExternalLibrary(string dllName)
        {
            if (string.IsNullOrWhiteSpace(dllName))
                return null;

            // Ensure it ends with .dll
            if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                dllName += ".dll";

            // Build full path
            string basePath = Path.Combine(AppContext.BaseDirectory, "ExternalLibraries");
            string fullPath = Path.Combine(basePath, dllName);

            // Attempt to load
            if (File.Exists(fullPath))
            {
                try
                {
                    return Assembly.LoadFrom(fullPath);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to load assembly '{dllName}': {ex.Message}", LogLevel.Error, "WinForge.Common.ModuleLoader");
                }
            }
            else
            {
                Logger.Instance.Log($"Assembly not found at: {fullPath}", LogLevel.Error, "WinForge.Common.ModuleLoader");
            }

            return null;
        }

        /// <summary> Initializes the modules in the specified order.</summary>
        public static void InitializeModules(Queue<IModule> modules, DependencyService dependencyService)
        {
            try
            {
                while (modules.Count > 0)
                {
                    var module = modules.Dequeue();
                    module.Initialize(dependencyService);
                    Logger.Instance.Log($"Initialized module: {module.Name} v{module.Version}", LogLevel.Info, "WinForge.Common.ModuleLoader");
                    if (module.Status == ModuleStatus.NotStarted) module.Initialize(dependencyService);
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Log($"Error initializing modules: {e.Message}", LogLevel.Error, "WinForge.Common.ModuleLoader");
            }
        }
    }
}