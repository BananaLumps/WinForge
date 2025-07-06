using System.Reflection;
using System.Text.Json;

namespace WinForge.Settings
{
    public static class Application
    {
        public static string ModuleDirectory { get; set; } = "./modules";
        public static string LogFilePath { get; set; } = "./logs/app.log";
        public static int MaxLogFiles { get; set; } = 10;
        public static int IPCResponseTimeout { get; set; } = 30000; // in milliseconds
        public static bool StartMinimized { get; set; } = false;
        public static bool StartWithWindows { get; set; } = false;

        public static Dictionary<string, object> CustomSettings { get; set; } = new();
        public static object? GetSetting(string key)
        {
            return CustomSettings.TryGetValue(key, out var value) ? value : null;
        }
        public static void SetSetting(string key, object value)
        {
            CustomSettings[key] = value;
            Persistence.SaveApplicationSettings();
        }
    }
    public static class User
    {
        public static Dictionary<string, object> CustomSettings { get; set; } = new();
        public static object? GetSetting(string key)
        {
            return CustomSettings.TryGetValue(key, out var value) ? value : null;
        }
        public static void SetSetting(string key, object value)
        {
            CustomSettings[key] = value;
            Persistence.SaveUserSettings();
        }
    }
    public static class Persistence
    {
        private const string ApplicationSettingsFile = "ApplicationSettings.json";
        private const string UserSettingsFile = "UserSettings.json";

        /// <summary> Saves the application settings to a JSON file. </summary>
        public static bool SaveApplicationSettings()
        {
            try
            {
                var dict = typeof(Application)
                    .GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .ToDictionary(p => p.Name, p => p.GetValue(null));

                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ApplicationSettingsFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                return false;
            }
        }

        /// <summary> Loads the application settings from a JSON file. </summary>   
        public static bool LoadApplicationSettings()
        {
            try
            {
                if (!File.Exists(ApplicationSettingsFile))
                    return false;

                var json = File.ReadAllText(ApplicationSettingsFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict == null) return false;

                foreach (var prop in typeof(Application).GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!dict.TryGetValue(prop.Name, out var jsonValue))
                        continue;

                    object? value = null;

                    try
                    {
                        value = JsonSerializer.Deserialize(jsonValue.GetRawText(), prop.PropertyType);
                        prop.SetValue(null, value);
                    }
                    catch
                    {
                        // Ignore invalid or incompatible values
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return false;
            }
        }

        /// <summary> Saves the user settings to a JSON file. </summary>
        public static bool SaveUserSettings()
        {
            try
            {
                var dict = typeof(User)                                   // <- static user container
                    .GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .ToDictionary(p => p.Name, p => p.GetValue(null));

                var json = JsonSerializer.Serialize(dict,
                              new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(UserSettingsFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user settings: {ex.Message}");
                return false;
            }
        }

        /// <summary> Loads the user settings from a JSON file. </summary>s
        public static bool LoadUserSettings()
        {
            try
            {
                if (!File.Exists(UserSettingsFile))
                    return false;

                var json = File.ReadAllText(UserSettingsFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict == null) return false;

                foreach (var prop in typeof(User).GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!dict.TryGetValue(prop.Name, out var jsonValue))
                        continue;

                    try
                    {
                        var value = JsonSerializer.Deserialize(jsonValue.GetRawText(),
                                                                prop.PropertyType);
                        prop.SetValue(null, value);
                    }
                    catch
                    {
                        // Ignore incompatible or malformed values
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user settings: {ex.Message}");
                return false;
            }
        }
    }
}
