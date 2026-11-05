using System;
using System.IO;
using System.Text.Json;

namespace SignalSort.Models;

/// <summary>
/// Persisted user settings. Stored under %LOCALAPPDATA%\SignalSort,
/// never inside scanned folders.
/// </summary>
public class AppSettings
{
    public bool IsDarkTheme { get; set; } = true;
    public string? LastFolder { get; set; }
    public bool RecursiveScan { get; set; }
    public bool IncludeHiddenFiles { get; set; }
    public bool IncludeSystemFiles { get; set; }
    public bool PreserveExtension { get; set; } = true;
    public NamingStyle NamingStyle { get; set; } = NamingStyle.CategoryPrefixed;
    public SortOrder SortOrder { get; set; } = SortOrder.OriginalName;

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SignalSort");

    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or unreadable settings are not fatal - start fresh.
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath))
                File.Replace(tmp, FilePath, null);
            else
                File.Move(tmp, FilePath);
        }
        catch
        {
            // Failing to persist settings must never break the app.
        }
    }
}
