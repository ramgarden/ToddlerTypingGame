namespace ToddlerTypingGame;

using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

static class Program
{
    private static string SettingsPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToddlerTypingGame", "settings.json");

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Check if this is the first run
        bool isFirstRun = CheckFirstRun();

        // Show auto-start prompt on first run
        if (isFirstRun)
        {
            ShowAutoStartPrompt();
        }

        // Apply auto-start setting
        ApplyAutoStartSetting();

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }

    private static bool CheckFirstRun()
    {
        if (!System.IO.File.Exists(SettingsPath))
        {
            SaveSettings(new Settings { FirstRunShown = false, AutoStartWithWindows = false });
            return true;
        }

        var settings = LoadSettings();
        return !settings.FirstRunShown;
    }

    private static void ShowAutoStartPrompt()
    {
        var result = MessageBox.Show(
            "Would you like this game to start automatically with Windows every time it starts?",
            "Auto-Start with Windows",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        var settings = LoadSettings();
        settings.FirstRunShown = true;
        settings.AutoStartWithWindows = result == DialogResult.Yes;
        SaveSettings(settings);
    }

    private static void ApplyAutoStartSetting()
    {
        var settings = LoadSettings();

        if (settings.AutoStartWithWindows)
        {
            AddToWindowsRegistryRun();
        }
        else
        {
            RemoveFromWindowsRegistryRun();
        }
    }

    private static void AddToWindowsRegistryRun()
    {
        const string appName = "ToddlerTypingGame";
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(runKey);
            key.SetValue(appName, Application.ExecutablePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add to registry: {ex.Message}");
        }
    }

    private static void RemoveFromWindowsRegistryRun()
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key != null)
            {
                key.DeleteValue("ToddlerTypingGame", false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to remove from registry: {ex.Message}");
        }
    }

    private static Settings LoadSettings()
    {
        try
        {
            var json = System.IO.File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    private static void SaveSettings(Settings settings)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private class Settings
    {
        public bool FirstRunShown { get; set; } = false;
        public bool AutoStartWithWindows { get; set; } = false;
    }
}