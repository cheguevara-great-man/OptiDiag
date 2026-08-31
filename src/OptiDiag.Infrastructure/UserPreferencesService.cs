using System.Text.Json;

namespace OptiDiag.Infrastructure;

public sealed record UserPreferences(
    string Theme = "Dark",
    int FontSize = 13,
    int PollingIntervalSeconds = 1,
    bool CheckUpdatesOnStartup = true)
{
    public UserPreferences Normalize() => this with
    {
        Theme = Theme is "Dark" or "Light" or "HighContrast" ? Theme : "Dark",
        FontSize = Math.Clamp(FontSize, 11, 18),
        PollingIntervalSeconds = Math.Clamp(PollingIntervalSeconds, 1, 60)
    };
}

public sealed class UserPreferencesService
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public UserPreferencesService(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OptiDiag",
            "settings.json");
    }

    public string Path { get; }

    public async Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            return new UserPreferences();
        }

        try
        {
            await using var stream = File.OpenRead(Path);
            var settings = await JsonSerializer.DeserializeAsync<UserPreferences>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return (settings ?? new UserPreferences()).Normalize();
        }
        catch (JsonException)
        {
            return new UserPreferences();
        }
        catch (IOException)
        {
            return new UserPreferences();
        }
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path)
                ?? throw new InvalidOperationException("设置文件路径缺少目录。");
            Directory.CreateDirectory(directory);
            var temporaryPath = Path + ".tmp";
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    preferences.Normalize(),
                    JsonOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, Path, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
