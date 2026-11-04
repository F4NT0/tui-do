namespace TuiDo.Presentation.Views;

internal static class HelpContent
{
    public sealed record Entry(string Key, string Description);

    public sealed record Section(string Title, IReadOnlyList<Entry> Entries);

    public const int ListsTab = 0;
    public const int TasksTab = 1;
    public const int DetailsTab = 2;

    public static IReadOnlyList<string> Tabs { get; } = ["Lists", "Tasks", "Details", "Forms & modals"];

    public static IReadOnlyList<Section> Page(int tab) => Pages[tab];

    private static readonly IReadOnlyList<Section>[] Pages =
    [
        [
            new("Lists panel (left)",
            [
                new("Up / k", "Previous list."),
                new("Down / j", "Next list (continues into My Lists)."),
                new("Enter / Right", "Open the task panel for the selected list."),
                new("n", "New task (in My Lists it is created inside that list)."),
                new("l", "New custom list (name + icon)."),
                new("x", "Delete the selected custom list; its tasks go back to My Day / Tasks."),
                new("/", "Search all tasks by #code or title."),
                new("Tab", "Move focus to the task panel."),
                new("o", "Show where the JSON data files are stored."),
                new("?", "Open this help."),
                new("q", "Quit."),
            ]),
        ],
        [
            new("Task panel (right)",
            [
                new("Up / k", "Previous task."),
                new("Down / j", "Next task."),
                new("Enter", "Open task details (markdown preview)."),
                new("n", "New task."),
                new("e", "Edit the selected task."),
                new("space", "Change status (Not started, Doing, Need more details, Blocked, Completed, Cancelled)."),
                new("m", "Move the task to a custom list."),
                new("p", "Reorder: move the task up/down (My Day and custom lists), Enter to finish."),
                new("P", "Set the priority position by number."),
                new("c", "Open the month calendar with task counts and statuses."),
                new("i", "Toggle important."),
                new("d", "Delete the task."),
                new("/", "Search by #code or title."),
                new("o", "Show data file location."),
                new("Tab / Left / Esc", "Back to the lists panel."),
            ]),
        ],
        [
            new("Task details",
            [
                new("Up / k", "Scroll up one line."),
                new("Down / j", "Scroll down one line."),
                new("PgUp / PgDn", "Scroll one page."),
                new("Home / End", "Jump to top / bottom."),
                new("e", "Edit the task."),
                new("space", "Change status."),
                new("m", "Move to a custom list."),
                new("o", "Show data file location."),
                new("Esc", "Back to the task list."),
            ]),
        ],
        [
            new("New / edit task",
            [
                new("Tab", "Next field."),
                new("Enter", "Save."),
                new("Shift+Enter", "New line in About."),
                new("Ctrl+V", "Paste from the clipboard."),
                new("Ctrl+E", "In About: open it in Neovim, Vim or VS Code."),
                new("Esc", "Cancel."),
            ]),
            new("Status, move and blocked-by modals",
            [
                new("Up / Down", "Select an option."),
                new("Enter", "Confirm."),
                new("Esc", "Cancel."),
            ]),
        ],
    ];
}
