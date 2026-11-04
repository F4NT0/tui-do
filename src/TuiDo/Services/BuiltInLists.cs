namespace TuiDo.Services;

internal static class BuiltInLists
{
    public const int MyDay = 0;
    public const int Tasks = 1;
    public const int Important = 2;
    public const int Completed = 3;
    public const int Cancelled = 4;
    public const int Count = 5;

    public static IReadOnlyList<string> Names { get; } = ["My Day", "Tasks", "Important", "Completed", "Cancelled"];
}
