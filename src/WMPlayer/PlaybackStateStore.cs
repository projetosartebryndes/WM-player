using System.Text.Json;

namespace WMPlayer;

public sealed class PlaybackStateStore
{
    private readonly string _storageFilePath;
    private readonly Dictionary<string, PlaybackState> _stateByFile;

    public PlaybackStateStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "WM-player");
        Directory.CreateDirectory(appFolder);
        _storageFilePath = Path.Combine(appFolder, "resume-state.json");
        _stateByFile = LoadState();
    }

    public long GetResumePosition(string mediaPath)
    {
        var key = BuildKey(mediaPath);
        return _stateByFile.TryGetValue(key, out var state) ? state.LastPositionMs : 0;
    }

    public void SetResumePosition(string mediaPath, long currentPositionMs, long mediaLengthMs)
    {
        if (mediaLengthMs <= 0) return;

        var key = BuildKey(mediaPath);
        var completion = mediaLengthMs == 0 ? 0 : currentPositionMs / (double)mediaLengthMs;

        _stateByFile[key] = new PlaybackState
        {
            LastPositionMs = completion > 0.98 ? 0 : Math.Max(currentPositionMs, 0),
            MediaLengthMs = mediaLengthMs,
            UpdatedAtUtc = DateTime.UtcNow
        };

        PersistState();
    }

    private Dictionary<string, PlaybackState> LoadState()
    {
        if (!File.Exists(_storageFilePath))
        {
            return new Dictionary<string, PlaybackState>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var raw = File.ReadAllText(_storageFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, PlaybackState>>(raw)
                   ?? new Dictionary<string, PlaybackState>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, PlaybackState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void PersistState()
    {
        var json = JsonSerializer.Serialize(_stateByFile, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storageFilePath, json);
    }

    private static string BuildKey(string mediaPath)
    {
        var normalized = Path.GetFullPath(mediaPath).ToLowerInvariant();
        return normalized;
    }

    private sealed class PlaybackState
    {
        public long LastPositionMs { get; init; }
        public long MediaLengthMs { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
