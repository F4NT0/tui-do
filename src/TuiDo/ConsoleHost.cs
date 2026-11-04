using Spectre.Console;
using TuiDo.Abstractions;

namespace TuiDo;

/// <summary>The only place that loops on real terminal I/O: redraw on resize / clock tick / notification change, route keys to the app.</summary>
internal sealed class ConsoleHost(TuiApp app, IKeySource keys, ITerminal terminal, IAnsiConsole console, IClock clock)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public void Run()
    {
        terminal.CursorVisible = false;
        terminal.Clear();
        int width = 0, height = 0;

        void Draw()
        {
            if (width != terminal.Width || height != terminal.Height)
            {
                width = terminal.Width;
                height = terminal.Height;
                terminal.Clear();
            }

            terminal.Home();
            console.Profile.Height = height - 1;
            console.Write(app.Render(console, width, height - 1));
        }

        while (!app.Quit)
        {
            Draw();
            WaitForKey(Draw, () => width != terminal.Width || height != terminal.Height);

            app.Press(keys.Read());
            while (app.IsTextEntry && keys.Available) app.Press(keys.Read());
        }

        terminal.CursorVisible = true;
        terminal.Clear();
    }

    private void WaitForKey(Action draw, Func<bool> resized)
    {
        var second = clock.Now.Second;
        var signature = app.Notifications.Signature;
        while (!keys.Available)
        {
            Thread.Sleep(PollInterval);
            if (clock.Now.Second == second && app.Notifications.Signature == signature && !resized()) continue;
            draw();
            second = clock.Now.Second;
            signature = app.Notifications.Signature;
        }
    }
}
