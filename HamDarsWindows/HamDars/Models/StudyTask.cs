using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace HamDars.Models;

public sealed class StudyTask : INotifyPropertyChanged
{
    private bool _done;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Subject { get; set; } = "";
    public string Category { get; set; } = "سایر درس‌ها";
    public int Duration { get; set; } = 30;
    public string Time { get; set; } = "";
    [JsonIgnore]
    public string DisplayTime => string.IsNullOrWhiteSpace(Time) ? "بدون ساعت" : Time;
    public bool Done
    {
        get => _done;
        set { if (_done == value) return; _done = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
