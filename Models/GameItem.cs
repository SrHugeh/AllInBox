using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using AllInBox.Services;

namespace AllInBox.Models;

public class GameItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = string.Empty;
    private string _executablePath = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _arguments = string.Empty;
    private string _iconPath = string.Empty;
    private string _platform = "PC / Outros";
    private string _category = "Geral";
    private bool _isFavorite;
    private DateTime _dateAdded = DateTime.Now;
    private DateTime? _lastPlayed;
    private int _playTimeMinutes;
    private long _playTimeSeconds;
    private long _installSizeBytes;
    private bool _isSelected;
    private string _notes = string.Empty;
    private bool _isRunning;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetField(ref _executablePath, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetField(ref _workingDirectory, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetField(ref _arguments, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set => SetField(ref _iconPath, value);
    }

    public string Platform
    {
        get => _platform;
        set => SetField(ref _platform, value);
    }

    public string Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public DateTime DateAdded
    {
        get => _dateAdded;
        set => SetField(ref _dateAdded, value);
    }

    public DateTime? LastPlayed
    {
        get => _lastPlayed;
        set
        {
            if (SetField(ref _lastPlayed, value))
            {
                OnPropertyChanged(nameof(LastPlayedText));
            }
        }
    }

    public int PlayTimeMinutes
    {
        get => _playTimeMinutes;
        set
        {
            if (SetField(ref _playTimeMinutes, value))
            {
                if (_playTimeSeconds == 0 && value > 0)
                {
                    _playTimeSeconds = value * 60L;
                }
                OnPropertyChanged(nameof(PlayTimeText));
            }
        }
    }

    public long PlayTimeSeconds
    {
        get => _playTimeSeconds > 0 ? _playTimeSeconds : _playTimeMinutes * 60L;
        set
        {
            if (SetField(ref _playTimeSeconds, value))
            {
                _playTimeMinutes = (int)(value / 60);
                OnPropertyChanged(nameof(PlayTimeMinutes));
                OnPropertyChanged(nameof(PlayTimeText));
            }
        }
    }

    public long InstallSizeBytes
    {
        get => _installSizeBytes;
        set
        {
            if (SetField(ref _installSizeBytes, value))
            {
                OnPropertyChanged(nameof(SizeText));
            }
        }
    }

    [JsonIgnore]
    public string SizeText => FormatSize(_installSizeBytes);

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "--";
        const double KB = 1024.0;
        const double MB = KB * 1024.0;
        const double GB = MB * 1024.0;
        const double TB = GB * 1024.0;

        if (bytes >= TB) return $"{bytes / TB:0.##} TB";
        if (bytes >= GB) return $"{bytes / GB:0.0} GB";
        if (bytes >= MB) return $"{bytes / MB:0.#} MB";
        return $"{bytes / KB:0} KB";
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(PlayButtonText));
            }
        }
    }

    [JsonIgnore]
    public string PlayTimeText => FormatPlayTime(PlayTimeSeconds);

    public static string FormatPlayTime(long totalSeconds)
    {
        if (totalSeconds <= 0) return LocalizationService.Instance.Get("NuncaJogado");
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalDays >= 1)
        {
            int days = (int)ts.TotalDays;
            int hours = ts.Hours;
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }
        if (ts.TotalHours >= 1)
        {
            int hours = (int)ts.TotalHours;
            int minutes = ts.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }
        if (ts.TotalMinutes >= 1)
        {
            int minutes = (int)ts.TotalMinutes;
            int seconds = ts.Seconds;
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }
        return $"{ts.Seconds}s";
    }

    [JsonIgnore]
    public string LastPlayedText
    {
        get
        {
            if (!_lastPlayed.HasValue) return LocalizationService.Instance.Get("NuncaJogado");
            var diff = DateTime.Now - _lastPlayed.Value;
            if (diff.TotalMinutes < 60) return LocalizationService.Instance.Get("Hoje");
            if (diff.TotalDays < 1) return LocalizationService.Instance.Get("Hoje");
            if (diff.TotalDays < 2) return LocalizationService.Instance.Get("Ontem");
            if (diff.TotalDays < 7) return LocalizationService.Instance.Get("DiasAtras", (int)Math.Floor(diff.TotalDays));
            return _lastPlayed.Value.ToString("dd/MM/yyyy");
        }
    }

    [JsonIgnore]
    public string PlayButtonText => IsRunning 
        ? LocalizationService.Instance.Get("EmJogo") 
        : LocalizationService.Instance.Get("Jogar");

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(PlayButtonText));
        OnPropertyChanged(nameof(SizeText));
    }

    [JsonIgnore]
    public bool ExistsOnDisk => !string.IsNullOrWhiteSpace(_executablePath) && File.Exists(_executablePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
