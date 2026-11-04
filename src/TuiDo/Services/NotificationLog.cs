using TuiDo.Abstractions;

namespace TuiDo.Services;

internal enum NotificationKind
{
    Success,
    Error,
    Warning,
}

internal sealed record Notification(DateTime At, string Message, NotificationKind Kind);

internal sealed class NotificationLog(IClock clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);
    private const int MaxEntries = 200;

    private readonly List<Notification> _entries = [];

    public IReadOnlyList<Notification> All => _entries;

    /// <summary>The newest notification, while it is still young enough to be shown.</summary>
    public Notification? Active =>
        _entries.Count > 0 && clock.Now - _entries[^1].At < Lifetime ? _entries[^1] : null;

    /// <summary>Changes whenever what <see cref="Active"/> would display changes (used to trigger redraws).</summary>
    public int Signature => Active is null ? 0 : _entries.Count;

    public void Success(string message) => Add(message, NotificationKind.Success);

    public void Error(string message) => Add(message, NotificationKind.Error);

    public void Warning(string message) => Add(message, NotificationKind.Warning);

    private void Add(string message, NotificationKind kind)
    {
        _entries.Add(new Notification(clock.Now, message, kind));
        if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
    }
}
