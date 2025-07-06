using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WinForge.Common;

namespace WinForge.Core
{
    /// <summary> Core handles keeping track of all modules and their instances. It will also handle all UI Form registration and instances </summary>
    public class Core : IModule
    {

        public static IDependencyService DependencyService { get; private set; } = new DependencyService();
        public static ILogger Logger = null;
        public static ICommunication Communication;
        //ToDo: Add data loading and saving functionality if required
        public static Core Instance { get; } = new Core();
        public Core(IDependencyService dependencyService)
        {
            Initialize(dependencyService);
        }
        public Core()
        {
            // Default constructor for singleton pattern
        }

        /// <summary> Dictionary of all Form objects registered by modules. </summary>
        public Dictionary<string, object> Forms { get; } = new Dictionary<string, object>();

        /// <summary> Dictionary of all Options Form objects registered by modules. </summary>
        public Dictionary<string, object> OptionsForms { get; } = new Dictionary<string, object>();

        /// <summary> Dictionary of all active Form instances. </summary>
        public Dictionary<string, object> ActiveForms { get; } = new Dictionary<string, object>();

        public string Name => "WinForge.Core";

        public string Version => Helpers.GetVersionFromFile();

        public ModuleStatus Status { get; set; } = ModuleStatus.NotStarted;

        public List<string> Dependencies => new List<string> { };

        public void Initialize(IDependencyService dependencyService)
        {
            Status = ModuleStatus.Starting;
            DependencyService = dependencyService ?? throw new ArgumentNullException(nameof(dependencyService));

            if (!DependencyService.TryGetDependency<Logger>(out var logger))
            {
                throw new InvalidOperationException("Logger dependency is not registered.");
            }

            Logger = dependencyService.GetDependency<Logger>();
            dependencyService.Register<Core>(Instance);
            Logger.Log($"Initializing module {Name} v{Version}...", LogLevel.Info, "WinForge.Core");
            Status = ModuleStatus.Running;
            Communication = new IPC.Client
            {
                PipeName = "WinForge.Core"
            };
        }

        /// <summary>
        /// Registers a Form type that can be instantiated later.
        /// To avoid conflicts, ensure that the key is unique across all registered forms. 
        /// <para> It is recommended to use the project name as a prefix to the key. Eg, WinForge.Core.UICore</para>
        /// </summary>
        /// <param name="key">The key to register the form type under.</param>
        /// <param name="formType">The Type of the form to register.</param>
        /// <returns>True if registration succeeded; otherwise, false.</returns>s
        public bool RegisterForm(string key, Type formType)
        {
            if (!typeof(Form).IsAssignableFrom(formType))
            {
                Logger?.Log($"Type '{formType.Name}' is not a Form type.", LogLevel.Error, "WinForge.Core");
                return false;
            }

            return Forms.TryAdd(key, formType);
        }

        /// <summary>
        /// Registers a OptionsForm type that will be loaded into the options forms
        /// </summary>
        /// <param name="key">The key to register the form type under.</param>
        /// <param name="formType">The Type of the form to register.</param>
        /// <returns>True if registration succeeded; otherwise, false.</returns>
        public bool RegisterOptionsForm(string key, Type formType)
        {
            if (!typeof(Form).IsAssignableFrom(formType))
            {
                Logger?.Log($"Type '{formType.Name}' is not a Form type.", LogLevel.Error, "WinForge.Core");
                return false;
            }

            return OptionsForms.TryAdd(key, formType);
        }

        /// <summary>
        /// Loads a new Form instance by its string key from the Forms dictionary.
        /// </summary>
        /// <param name="key">The string key of the Form type.</param>
        /// <returns>A new instance of the Form if the type is found; otherwise, null.</returns>
        public Form LoadForm(string key)
        {
            if (IsFormActive(key))
            {
                Logger?.Log($"Form key '{key}' is already active. Either rename the instance or use GetActiveForm.", LogLevel.Warning, "WinForge.Core");
                return null;
            }
            if (Forms.TryGetValue(key, out var formType))
            {
                if (formType is Type type && typeof(Form).IsAssignableFrom(type))
                {
                    try
                    {
                        var form = (Form)Activator.CreateInstance(type)!;
                        return form;
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log($"Failed to create instance of form '{key}': {ex.Message}", LogLevel.Error, "WinForge.Core");
                        return null;
                    }
                }
                Logger?.Log($"Form type '{key}' is not a valid Form type.", LogLevel.Warning, "WinForge.Core");
                return null;
            }
            Logger?.Log($"Form type '{key}' not found in Forms dictionary.", LogLevel.Warning, "WinForge.Core");
            return null;
        }

        /// <summary>
        /// Closes a Form by its string key from the active forms dictionary.s
        /// </summary>
        /// <param name="key">The string key of the Form.</param>
        /// <returns>True if the form was found and closed; otherwise, false.</returns>
        public bool CloseForm(string key)
        {
            if (ActiveForms.TryGetValue(key, out var formObj) && formObj is Form form)
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                    return true;
                }
                Logger?.Log($"Form '{key}' is already disposed.", LogLevel.Warning, "WinForge.Core");
                return false;
            }
            Logger?.Log($"Form '{key}' not found in Forms dictionary.", LogLevel.Warning, "WinForge.Core");
            return false;
        }

        /// <summary> Tries to get a Form instance by its string key from the active forms dictionary or creates a new instance if there is no active instance available. </summary>
        public Form GetActiveForm(string key)
        {
            if (ActiveForms.TryGetValue(key, out var formObj) && formObj is Form form)
            {
                return form;
            }
            Logger?.Log($"Active form '{key}' not found in ActiveForms dictionary. Creating new instance of {key}.", LogLevel.Warning, "WinForge.Core");
            return LoadForm(key);
        }
        public bool IsFormActive(string key)
        {
            return ActiveForms.ContainsKey(key);
        }
        public void Stop()
        {
            Status = ModuleStatus.Stopping;
            Logger?.Log($"Stopping module {Name}...", LogLevel.Info);
            // Close all active forms
            foreach (var key in ActiveForms.Keys.ToList())
            {
                CloseForm(key);
            }
            // Clear all dictionaries
            Forms.Clear();
            OptionsForms.Clear();
            ActiveForms.Clear();
            Status = ModuleStatus.Stopped;
            Logger?.Log($"Module {Name} stopped successfully.", LogLevel.Info, "WinForge.Core");
        }
    }
}
