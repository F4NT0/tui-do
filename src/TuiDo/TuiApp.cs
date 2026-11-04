using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Abstractions;
using TuiDo.Input;
using TuiDo.Presentation;
using TuiDo.Presentation.Views;
using TuiDo.Services;

namespace TuiDo;

/// <summary>The application without any terminal I/O: feed it keys with <see cref="Press"/>, draw it with <see cref="Render"/>.</summary>
internal sealed class TuiApp
{
    private readonly IReadOnlyList<IKeyHandler> _handlers;
    private readonly AppView _view;

    public TuiApp(AppState state, TodoService service, NotificationLog notifications, IReadOnlyList<IKeyHandler> handlers, AppView view)
    {
        State = state;
        Service = service;
        Notifications = notifications;
        _handlers = handlers;
        _view = view;
    }

    public AppState State { get; }

    public TodoService Service { get; }

    public NotificationLog Notifications { get; }

    public bool Quit => State.Quit;

    public bool IsTextEntry => State.IsTextEntry;

    public void Press(ConsoleKeyInfo key) => _handlers.First(h => h.IsActive).Handle(key);

    public IRenderable Render(IAnsiConsole console, int width, int height) => _view.Render(console, width, height);

    public static TuiApp Create(ITodoRepository repository, IClock clock, IClipboard clipboard, IKeySource keys, IEditorLauncher editors, IFolderOpener folders)
    {
        var log = new NotificationLog(clock);
        var service = new TodoService(repository, clock, log);
        var state = new AppState();
        var browse = new BrowseModel(state, service);
        var shortcuts = new GlobalShortcuts(state, clock);

        // Order matters: modals first, then the browsing modes, with the main handler as the fallback.
        IKeyHandler[] handlers =
        [
            new EditorPickerKeyHandler(state, editors, log),
            new CalendarKeyHandler(state, clock),
            new DataLocationKeyHandler(state, service, folders, log),
            new TaskFormKeyHandler(state, service, clipboard, keys, log),
            new ListFormKeyHandler(state, service, clipboard, log),
            new MovePickerKeyHandler(state, service),
            new BlockedByPickerKeyHandler(state, service),
            new StatusPickerKeyHandler(state, service),
            new PriorityKeyHandler(state, browse, service),
            new HelpKeyHandler(state),
            new SearchKeyHandler(state),
            new DetailKeyHandler(state, browse, service, shortcuts),
            new ReorderKeyHandler(state, browse, service, shortcuts),
            new MainKeyHandler(state, browse, service, clock, shortcuts),
        ];

        return new TuiApp(state, service, log, handlers, new AppView(state, browse, service, editors, clock, log));
    }
}
