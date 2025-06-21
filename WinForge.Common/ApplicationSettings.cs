using System.Reflection;
using System.Text.Json;

namespace WinForge.Settings
{
    public static class Application
    {
        public static string ModuleDirectory { get; set; } = "./modules";
        public static string LogFilePath { get; set; } = "./logs/app.log";
        public static int MaxLogFiles { get; set; } = 10;
    }
    public static class Persistence
    {
        private const string SettingsFile = "ApplicationSettings.json";

        public static bool Save()
        {
            try
            {
                var dict = typeof(Application)
                    .GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .ToDictionary(p => p.Name, p => p.GetValue(null));

                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                return false;
            }
        }

        public static bool Load()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return false;

                var json = File.ReadAllText(SettingsFile);
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
    }
}
