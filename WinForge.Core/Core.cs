using WinForge.Common;

namespace WinForge.Core
{
    /// <summary> Core handles keeping track of all modules and their instances. It will also handle all UI Form registration and instances </summary>
    public class Core
    {

        public static IDependencyService DependencyService { get; private set; } = new DependencyService();
        public static ILogger? Logger = null;
        //ToDo: Add data loading and saving functionality if required
        public static Core Instance { get; } = new Core();

        /// <summary> Dictionary of all Form objects registered by modules. </summary>
        public Dictionary<string, object> Forms { get; } = new Dictionary<string, object>();

        /// <summary> Dictionary of all Options Form objects registered by modules. </summary>
        public Dictionary<string, object> OptionsForms { get; } = new Dictionary<string, object>();

        /// <summary> Dictionary of all active Form instances. </summary>
        public Dictionary<string, object> ActiveForms { get; } = new Dictionary<string, object>();
        public static void Initialize(IDependencyService dependencyService)
        {
            DependencyService = dependencyService ?? throw new ArgumentNullException(nameof(dependencyService));

            if (!DependencyService.TryGetDependency<ILogger>(out var logger))
            {
                throw new InvalidOperationException("Logger dependency is not registered.");
            }

            Logger = logger;
            dependencyService.Register<Core>(Instance);
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
                Logger?.Error($"Type '{formType.Name}' is not a Form type.");
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
                Logger?.Error($"Type '{formType.Name}' is not a Form type.");
                return false;
            }

            return OptionsForms.TryAdd(key, formType);
        }

        /// <summary>
        /// Loads a new Form instance by its string key from the Forms dictionary.
        /// </summary>
        /// <param name="key">The string key of the Form type.</param>
        /// <returns>A new instance of the Form if the type is found; otherwise, null.</returns>
        public Form? LoadForm(string key)
        {
            if (IsFormActive(key))
            {
                Logger?.Warn($"Form key '{key}' is already active. Either rename the instance or use GetActiveForm.");
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
                        Logger?.Error($"Failed to create instance of form '{key}': {ex.Message}");
                        return null;
                    }
                }
                Logger?.Warn($"Form type '{key}' is not a valid Form type.");
                return null;
            }
            Logger?.Warn($"Form type '{key}' not found in Forms dictionary.");
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
                Logger?.Warn($"Form '{key}' is already disposed.");
                return false;
            }
            Logger?.Warn($"Form '{key}' not found in Forms dictionary.");
            return false;
        }

        /// <summary> Tries to get a Form instance by its string key from the active forms dictionary or creates a new instance if there is no active instance available. </summary>
        public Form? GetActiveForm(string key)
        {
            if (ActiveForms.TryGetValue(key, out var formObj) && formObj is Form form)
            {
                return form;
            }
            Logger?.Warn($"Active form '{key}' not found in ActiveForms dictionary. Creating new instance of {key}.");
            return LoadForm(key);
        }
        public bool IsFormActive(string key)
        {
            return ActiveForms.ContainsKey(key);
        }
    }
}
