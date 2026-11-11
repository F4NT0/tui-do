# tuido 1.0.2

## New
- `tuido --update`: checks the latest GitHub release and, when it is newer, downloads `tuido.exe` and replaces the installed copy.
- Custom lists now have a **color**. Pick it in the list form with `←` / `→`; the list icon and name use it in the sidebar, task rows and calendar.
- Tasks with the *Event* status take the color of the list they belong to.
- Lists can be edited: press `e` on a custom list in the lists panel to change name, icon and color.
- The color palette avoids the status colors and favors light tones for dark backgrounds.

## Changed
- Calendar: removed the "Tasks on dd/MM" block below the grid. `Enter` on a day shows its tasks together with the list each one is saved in.
- List form footer says `Enter: save`.

## Release assets
Attach `tuido.exe` (and `tuido.Installer.exe`) to the GitHub release; `--update` looks for `tuido.exe`.
