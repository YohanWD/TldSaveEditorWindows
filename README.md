# TLD Survival Save Editor (C# / WPF)

Editor for The Long Dark **Survival mode** saves (`%LOCALAPPDATA%\Hinterland\TheLongDark\Survival\sandbox*`).

```
dotnet run --project src/TldSaveEditor
```

Or open `TldSaveEditor.slnx` in Visual Studio. Requires the .NET 10 SDK.

## Projects

| Project | What it does |
|---|---|
| `src/TldSave.Core` | Save file library, no UI. `Lzf` (compression), `TldJson` (JSON with NaN/Infinity and nested JSON strings), `SaveFile` (load/save), `Inventory`, `SaveLocator` (find saves, backups) |
| `src/TldSaveEditor` | WPF app: save list, Overview, Inventory (condition, add/remove, raw gear JSON), Raw JSON viewer |

## Applying changes in game

**Quit the game completely before loading an edited save.** The game keeps the save in memory, so going
back to the main menu and reloading uses the old data and ignores your changes. It may also overwrite
the edited file on its next save.

1. Exit The Long Dark completely (not just to the main menu).
2. Edit and save with the editor.
3. Start the game and load the save.

## Safety

- Every save writes a backup first, to `Documents\TldSaveEditor\Backups\`.
- Only saves with `m_GameMode = SANDBOX` can be opened.
- Load and save were checked against 22 real saves: decoded data is identical after a round trip.
  The game has not loaded a file written by this editor yet. Test on a save you don't mind losing.
- Prefab names in "Add item" are not validated.
