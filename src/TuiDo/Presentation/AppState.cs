using TuiDo.Domain;
using TuiDo.Presentation.Documents;
using TuiDo.Presentation.Forms;

namespace TuiDo.Presentation;

/// <summary>Everything the UI remembers between key presses. Plain data: handlers mutate it, views read it.</summary>
internal sealed class AppState
{
    // browsing
    public int Menu { get; set; }
    public int Selected { get; set; }
    public bool FocusRight { get; set; }
    public bool Detail { get; set; }
    public bool Reorder { get; set; }
    public bool Searching { get; set; }
    public string? Query { get; set; }
    public int Scroll { get; set; }
    public ScrollView? View { get; set; }
    public bool Quit { get; set; }

    // modals (null / false = closed)
    public TaskForm? TaskForm { get; set; }
    public ListForm? ListForm { get; set; }
    public int? StatusPick { get; set; }
    public int? BlockPick { get; set; }
    public int? MovePick { get; set; }
    public int? EditorPick { get; set; }
    public int? HelpTab { get; set; }
    public string? PriorityInput { get; set; }
    public DateTime? Calendar { get; set; }
    public bool ShowPath { get; set; }

    // context shared by the pickers
    public TodoItem? Target { get; set; }
    public IReadOnlyList<TodoItem> BlockerCandidates { get; set; } = [];

    public bool ModalOpen =>
        TaskForm != null || ListForm != null || StatusPick != null || BlockPick != null || MovePick != null ||
        EditorPick != null || HelpTab != null || PriorityInput != null || Calendar != null || ShowPath;

    public bool IsTextEntry => TaskForm != null;
}
