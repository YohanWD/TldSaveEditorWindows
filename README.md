# TLD Survival Save Editor

A save editor for **The Long Dark, Survival mode**, for Windows.

- **Player:** health, calories, thirst, fatigue, cold, Well Fed, skill levels, and curing afflictions
- **Inventory:** change quantities and condition, remove items, and add new items from a list of 440+ game items
- **Overview:** experience mode, region, days survived
- Makes a backup before every save, so you can always roll back

Based on [TLD-Save-Editor-3](https://github.com/FINDarkside/TLD-Save-Editor-3) by FINDarkside. This is a C# / WPF
rewrite of its save format logic: LZF compression, nested JSON sections, the inventory structure and the
original item list.

> ⚠️ **Always quit The Long Dark completely before editing a save.** See [why](#why-the-game-must-be-closed).

---

## Download

1. Open the **Releases** page of this repository.
2. Download the latest `TldSaveEditor-x.y.z-win-x64.zip`.
3. Extract the zip anywhere (e.g. your Desktop) and run **`TldSaveEditor.exe`**.

Nothing else to install: the .NET runtime is included in the exe. It needs **Windows 10 or 11, 64-bit**.

The first time, Windows may show *"Windows protected your PC"*, because the exe isn't code-signed.
Click **More info**, then **Run anyway**.

---

## How to use

### 1. Quit the game
Exit The Long Dark **completely**, back to the desktop. Going back to the main menu is not enough.

### 2. Open your save
Start `TldSaveEditor.exe`. The left panel lists your Survival saves, with the **same names as in the game's
load menu** (the file name, e.g. `sandbox1`, is shown underneath). Click the save you want to edit.

Only Survival saves are listed. Story and challenge saves are not supported.

### 3. Make your changes

| Tab | What you can do |
|---|---|
| **Overview** | Check the save: name, experience mode, region, days survived, condition |
| **Player** | Move the sliders for health, calories, thirst, fatigue and cold. For thirst, fatigue and cold, **0 = best**. *Restore all* sets everything to full. Raise skills (*Max* per skill, or *Max all skills*). Cure afflictions one by one or with *Cure all* |
| **Inventory** | Items are grouped by category like in the game. Select an item to change its **quantity** (stackable items like wood, matches, ammo) or its **condition**, or to **remove** it. Click **Add item…** to search the item list and add new items |
| **Raw JSON** | Read-only view of the whole save, for the curious |

The window title shows a `*` while you have unsaved changes.

### 4. Save
Click **Save (with backup)**. The editor first copies the current save file to the backup folder (see below),
then writes your changes. The status bar at the bottom shows where the backup went.

### 5. Play
Start the game and load the save. Your changes are there.

---

## Why the game must be closed

The game keeps your save in memory while it runs:

- If you edit while the game is open and then go back to the menu and reload, the game **uses its old copy**
  and your changes don't appear.
- When the game saves again (sleeping, changing area, quitting), it **overwrites the file** and erases your edit.

So: quit the game completely, edit, save in the editor, then start the game.

---

## Where your saves are

The editor reads Survival saves from:

```
%LOCALAPPDATA%\Hinterland\TheLongDark\Survival\
```

That is `C:\Users\<your name>\AppData\Local\Hinterland\TheLongDark\Survival\`. Paste the first line into the
Windows Explorer address bar to open it. Each save is a file named `sandbox1`, `sandbox2`, ... (no extension).

If your saves are somewhere else, use **Open file…** in the editor to pick one.

## Backups and rolling back

Every time you click **Save**, the file as it was **before** your change is copied to:

```
Documents\TldSaveEditor\Backups\
```

Backups are named after the save and the date and time, e.g. `sandbox1_20261025_143000`. Nothing is ever
deleted there; clean the folder yourself when you no longer need old backups.

**To roll back to a backup:**
1. Quit the game completely.
2. In the backup folder, find the backup you want (check the date and time in its name).
3. Copy it into the save folder (`%LOCALAPPDATA%\Hinterland\TheLongDark\Survival\`).
4. Rename it to the original save name, e.g. `sandbox1_20261025_143000` → `sandbox1`, replacing the existing file.
5. Start the game.

**Tip:** before your first edit, also make your own copy of the save file somewhere safe.

## Good to know

- Some values are approximations. Item categories are guessed, since the save doesn't store them. Skill levels
  below 5 are only shown for Fire starting and Cooking.
- Items marked *story/challenge only* or *unverified name* in Add item were never seen in a Survival save and
  may not work. They are hidden unless you uncheck *Only items found in Survival saves*.
- Curing afflictions has not been tested in game yet.
- Use at your own risk, and keep backups.

---
---

# For developers

Requires the .NET 10 SDK. Run from source:

```
dotnet run --project src/TldSaveEditor
```

Or open `TldSaveEditor.slnx` in Visual Studio.

## Projects

| Project | What it does |
|---|---|
| `src/TldSave.Core` | Save file library, no UI. `Lzf` (compression), `TldJson` (JSON with NaN/Infinity and nested JSON strings), `SaveFile` (load/save), `Inventory`, `PlayerState` (status, skills, afflictions), `ItemCatalog`, `SaveLocator` (find saves, backups) |
| `src/TldSaveEditor` | WPF app: save list, Overview, Player, Inventory, Raw JSON viewer |
| `tools/CatalogBuilder` | Generates `ItemCatalog.json` from the saves on this PC (see below) |

## Save format

```
file                 = LZF( JSON "SlotData" )         m_DisplayName, m_GameMode, m_Dict, ...
SlotData.m_Dict[key] = base64 -> LZF( JSON )          boot, global, info, screenshot, one per visited scene
inside sections      = many values are JSON strings containing more JSON (TldJson.ReadNested / WriteNested)
```

## Player tab fields

| Value | Save field (in `global`) | Meaning |
|---|---|---|
| Condition | `m_Condition_Serialized.m_CurrentHPProxy` | 0–100, up to 105 with Well Fed. Also copied to `info.m_Condition`, which the load menu shows |
| Calories | `m_Hunger_Serialized.m_CurrentReserveCaloriesProxy` | Reserve kcal (max 2500), 0 = starving |
| Thirst / Fatigue / Freezing | `m_Thirst_Serialized`, `m_Fatigue_Serialized`, `m_Freezing_Serialized` | **0 = best**, 100 = worst (a brand-new game starts at ~0) |
| Skills | `m_SkillsManagerSerialized.m_Skill_*Serialized.m_Points` | Points. Level 5 = 200 fire, 350 cooking, 250 ice fishing, 125 archery, 350 mending, 100 for the others |
| Afflictions | `m_SprainedAnkleSerialized`, `m_BurnsSerialized`, ... | Wounds are parallel lists (cure = empty them); others use an `m_Active` flag |

## Item catalog ("Add item…")

`src/TldSave.Core/ItemCatalog.json` lists the items you can add. Notes, collectibles and keys are left out.
It is generated by `tools/CatalogBuilder`, which reads every save on this PC: the player inventory, plus the
containers and loose items of every visited scene. It can also read the original TS editor's item list.

For each item found in a save, a real sample becomes a "fresh item" template: full condition, stack of 1,
full calories, dry, not equipped. Position, ID and quest links are removed.

To regenerate it after playing more or after a game update:

```
dotnet run --project tools/CatalogBuilder -- src/TldSave.Core/ItemCatalog.json ../TLD-Save-Editor-3-main/src/tldSave/availableItems.ts
```

## Building the exe

```
dotnet publish src/TldSaveEditor -p:PublishProfile=win-x64
```

This produces a single self-contained `publish\win-x64\TldSaveEditor.exe` (~62 MB). When sharing, include
`THIRD_PARTY_NOTICES.md`: the original editor's MIT license requires its notice.

### Releases (GitHub Actions)

[.github/workflows/build.yml](.github/workflows/build.yml) builds the exe when a version tag is pushed, and
creates a GitHub Release with the zip. **Push the commit first, then the tag.**

```
git push origin master
git tag v1.0.1
git push origin v1.0.1
```

The tag sets the version (`v1.0.1` → 1.0.1). To test a build without releasing, open the Actions tab, select
*Build*, and click *Run workflow*. The exe is attached to the run under *Artifacts*.

## Testing

- Load and save were checked against 22 real saves: decoded data is identical after a round trip.
- Validated in game (2026-10-25) with [docs/TEST_SAVE_CHECKLIST.md](docs/TEST_SAVE_CHECKLIST.md): 66 added items
  of every component type, quantity / condition / remove edits, a game save, reload and re-edit. Player status
  meters, Well Fed and skill levels too. Curing afflictions is not tested in game yet.
