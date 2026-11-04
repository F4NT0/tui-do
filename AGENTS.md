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
