# tui-do

Terminal to-do app (.NET 10, Spectre.Console). Solution: `TuiDo.slnx`.

## Layout
- `src/TuiDo` - the app. `Program.cs` is the composition root, `TuiApp` is the I/O-free application, `ConsoleHost` owns the real terminal loop.
  - `Domain` (entities), `Services` (business rules + persistence orchestration), `Abstractions` (I/O interfaces), `Infrastructure` (real implementations),
    `Input` (one key handler per UI mode), `Presentation` (state, views, forms, markdown).
- `tests/TuiDo.Tests` - xUnit. Rendering tests drive the real `TuiApp` through `Support/TestApp` (fake clock/repository/clipboard/editor/folder opener) and
  render into Spectre's `TestConsole`; unit tests cover services, parsers, repository and the markdown renderer.

## Commands
- Run: `dotnet run --project src/TuiDo`
- Test: `dotnet test`
- Close the running app before building, otherwise `TuiDo.exe` is locked.

## Conventions
- Data lives in `%LOCALAPPDATA%\tui-do\tasks.json` and `lists.json`.
- Nerd Font glyphs are written as `\uXXXX` escapes; keep them in `Theme`/`Glyph` instead of repeating literals.
- New UI modes get their own `IKeyHandler` registered (in priority order) in `TuiApp.Create` and a renderer in `Presentation/Views`.

## CLI commands (`src/TuiDo/Cli`)
- `tuido --version | --help | --install | --uninstall` are handled by `CliApp` before the TUI starts (see `Program.cs`). The assembly is named `tuido`; the version lives in `<Version>` in `TuiDo.csproj` (starts at 1.0.0).
- `--version` compares against the latest GitHub release (`AppInfo.Repository`). `--install` copies the app folder to `%LOCALAPPDATA%\Programs\tuido` and adds it to the user PATH; `--uninstall` also deletes the data folder.
- Publish a self-contained build for distribution: `dotnet publish src/TuiDo -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist`
