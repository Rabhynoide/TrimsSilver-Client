using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AlbionDataAvalonia.Settings;

public class SettingsManager
{
    private string deafultUserSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "DefaultUserSettings.json");
    private string defaultAppSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "DefaultAppSettings.json");

    private readonly WindowsStartupService windowsStartupService;

    public UserSettings UserSettings { get; } = new();
    public AppSettings AppSettings { get; private set; } = new();
    public SettingsManager(WindowsStartupService windowsStartupService)
    {
        this.windowsStartupService = windowsStartupService;
    }

    public Task InitializeSettings()
    {
        LoadUserSettings();
        windowsStartupService.Sync(UserSettings.StartWithWindows);
        LoadAppSettingsFromLocal();
        return Task.CompletedTask;
    }


    private void LoadUserSettings()
    {
        try
        {
            // Get the path to the user settings file in the local app data directory
            string localUserSettingsFilePath = Path.Combine(AppData.LocalPath, "UserSettings.json");

            // If the user settings file doesn't exist in the local app data directory, use the default settings file
            if (!File.Exists(localUserSettingsFilePath))
            {
                localUserSettingsFilePath = deafultUserSettingsFilePath;

                // If we are in development mode, use the default settings file
#if DEBUG
                localUserSettingsFilePath = deafultUserSettingsFilePath;
#endif

                _ = LoadUserSettingsFromJson(File.ReadAllText(localUserSettingsFilePath));
                SaveUserSettings(); // Save the default settings to a new user settings file
            }
            else
            {
                if (LoadUserSettingsFromJson(File.ReadAllText(localUserSettingsFilePath)))
                {
                    SaveUserSettings();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load user settings; using defaults.");
        }

        UserSettings.PropertyChanged += UserSettings_PropertyChanged;
    }

    private void UserSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SaveUserSettings();

        if (e.PropertyName == nameof(UserSettings.StartWithWindows))
        {
            windowsStartupService.Sync(UserSettings.StartWithWindows);
        }
    }

    private bool LoadUserSettingsFromJson(string json)
    {
        var hasStartupWindowMode = HasJsonProperty(json, nameof(UserSettings.StartupWindowMode));
        var settings = JsonSerializer.Deserialize<UserSettings>(json);

        if (settings == null)
        {
            return false;
        }

        UserSettings.UpdateFrom(settings);

        if (!hasStartupWindowMode)
        {
            UserSettings.StartupWindowMode = settings.StartHidden
                ? StartupWindowMode.HiddenToTray
                : StartupWindowMode.ShowMainWindow;
        }

        return !hasStartupWindowMode;
    }

    private static bool HasJsonProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(propertyName, out _);
    }

    private void LoadAppSettingsFromLocal()
    {
        try
        {
            if (File.Exists(defaultAppSettingsFilePath))
            {
                string json = File.ReadAllText(defaultAppSettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    AppSettings = settings;
                }
                else
                {
                    throw new Exception("Failed to load app settings from local default file.");
                }
                Log.Information("App settings loaded successfully from local default file.");
            }
            else
            {
                throw new Exception("Local default app settings file not found.");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error in LoadAppSettingsFromLocal: {ex}");
            AppSettings ??= new AppSettings();
        }
    }

    private void SaveUserSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(UserSettings);

            // Ensure the directory exists
            Directory.CreateDirectory(AppData.LocalPath);

            // Get the path to the user settings file in the local app data directory
            string userSettingsFilePath = Path.Combine(AppData.LocalPath, "UserSettings.json");

            File.WriteAllText(userSettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Log.Error($"Error in SaveSettings: {ex}");
        }
    }
}
