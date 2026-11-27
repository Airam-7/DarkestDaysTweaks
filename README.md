# Darkest Days Tweaks

Source code and MelonLoader compatibility files for **Into the Dead: Our Darkest Days** on Windows x64.

**NexusMods distributes the mod-only ZIP. It does not include the MelonLoader compatibility fix.** Download the fix separately from [Releases](https://github.com/Airam-7/DarkestDaysTweaks/releases), as described below. Both the compiled mod and the fix are also available as separate Release downloads. The mod DLL is not stored in the Git source tree.

## Downloads

| Download | Contents |
|---|---|
| [MelonLoader compatibility fix](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.7/MelonLoader-Compatibility-Fix-Unity-6000.5.11f1.zip) | Only the six compatibility files; no mod |
| [Darkest Days Tweaks 0.1.7](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.7/DarkestDaysTweaks-0.1.7-Mod.zip) | Only `Mods/DarkestDaysTweaks.dll` |

See the [v0.1.7 release page](https://github.com/Airam-7/DarkestDaysTweaks/releases/tag/v0.1.7) and the [changelog](CHANGELOG.md) for changes. Official MelonLoader must be downloaded separately.

## Mod features

- Survivor backpack capacity, configurable up to 32 total slots. Set 1 for vanilla capacity. A survivor with 11 slots gains four when set to 15.
- Survivor experience gain multiplier, from 1 to 10. It applies to XP earned after saving the setting.
- Inventory stack size multiplier, from 1 to 1,000.
- Separate melee and firearm durability multipliers, from 1 to 1,000,000.
- Independent silent walking and silent running ON/OFF switches for footstep detection; audible footsteps are unchanged.
- Location loot quantity (up to ×5), scavenging range (up to ×3), food shelf life (up to ×5) and visual detection grace (up to +2 seconds).
- Separate recovery sliders for item healing, food satiety and morale. Deferred item healing follows the game's phase timing and cancellation rules.
- Notification volume slider for popup sounds, adjustable from 0 to 100%.
- Open settings with F6 or L3+R3 even when the on-screen button is hidden. Use the arrows or D-pad/left stick to navigate or adjust, Enter or A / Cross to select, and Esc or B / Circle to close. Click a numeric field to type an exact value.
- The panel scales to fit the screen. Background opacity, text size and window size are adjustable. **Restore settings** applies the defaults.

Version **0.1.7** uses English interface text and settings comments. Fresh installations default to vanilla gameplay.

## Saved games and local backups

Open **Saved games** from the mod menu to browse up to five compatible game saves and their five most recent captured phases. The game can load current saves and restore phases from the main menu or during a run. Confirming a phase restore in this view backs up and replaces the selected native slot.

Choose **Back up last game save** to create or update that campaign's manual copy. **Local backups** shows the first captured save, the latest five phases and the manual copy for each archived campaign. For existing campaigns, the first captured save is the earliest copy available to the mod. Files are stored under `UserData/DarkestDaysTweaks.Backups/Local/` in the game folder.

**Load local copy** opens the selected archive in a free local game slot. This workflow was verified with Steam in offline mode and preserves existing saves. A free slot is required.

## Installation

1. Close the game and its loader console. Install the **official MelonLoader 0.7.3 x64** and its **.NET 6 Desktop Runtime x64** prerequisite. Get MelonLoader from [LavaGang's official release](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3), not from this repository. **Do not launch the game yet.**
2. Download [**MelonLoader-Compatibility-Fix-Unity-6000.5.11f1.zip**](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.7/MelonLoader-Compatibility-Fix-Unity-6000.5.11f1.zip) from GitHub Releases and extract it into the game root, next to `IntoTheDeadOurDarkestDays.exe`. Merge the `MelonLoader/` folder and replace the supplied compatibility files. This is the separate compatibility installation required when using the NexusMods download.
3. Extract **DarkestDaysTweaks-0.1.7-Mod.zip**, downloaded from NexusMods or the separate mod link above, into the same game root. The result must be `Mods/DarkestDaysTweaks.dll`, not a second nested game or ZIP-name folder.
4. Start the game through Steam. Internet access is needed for the initial download of Unity reference libraries and generation of local IL2CPP references.
5. Press **F6** or **L3+R3**, or click **Mod settings - F6 / L3+R3**. Pause the game before changing settings during a run. Choose **Apply and save** to confirm changes.

**For a fresh installation, use both ZIPs: the mod-only ZIP from NexusMods and the compatibility-fix ZIP from GitHub Releases.** Neither ZIP includes official MelonLoader.

If you need **only the MelonLoader compatibility fix**, use its separate Release download or copy the contents of [`compatibility/files`](compatibility/files) into the game root after installing official MelonLoader 0.7.3. These files do not install the mod.

The generated settings file is `UserData/DarkestDaysTweaks.txt`. Existing values are preserved when updating the mod. You can edit and save the file while playing to reload the settings automatically, or select **Reload file** to apply them immediately. An existing working installation of this compatibility stack can update by replacing the mod DLL.

## Supported configuration

| Component | Tested version |
|---|---|
| Platform | Windows x64 / Steam |
| Steam game build | 26306995 |
| In-game version | v0.19.1.22694 |
| Unity | 6000.5.11f1 / metadata 107 |
| MelonLoader | 0.7.3 Open-Beta x64 |
| Mod source | 0.1.7 |

Compatibility with other game builds, loader versions, platforms or other mods is not claimed.

## Build the mod

Use a .NET SDK that supports targeting `net6.0`, official MelonLoader, the compatibility files, and IL2CPP references generated from your own installed copy of the game:

```powershell
dotnet build DarkestDaysTweaks.csproj -c Release -p:GameDir="YOUR_GAME_FOLDER"
```

The existing Visual Studio solution opens the same project. Build output goes to `bin/Release/net6.0`; it is ignored by Git. No game assemblies or generated references are supplied.

## Build the compatibility libraries

The modified source required for the three Il2CppInterop libraries is under [`compatibility/src/Il2CppInterop`](compatibility/src/Il2CppInterop). With SDK 9 or a compatible newer SDK, NuGet access, and your locally generated references:

```powershell
./scripts/Build-Compatibility.ps1 -GameDir "YOUR_GAME_FOLDER"
```

The script builds the libraries without installing them. The separate Cpp2IL executable is an unmodified official-project CI artifact; its origin and hashes are recorded in the [compatibility documentation](compatibility/README.md).

## Validation

The release build and local regression checks passed. An archived phase loaded into a free local slot with Steam in offline mode. SHA-256 checks confirmed that all existing save files retained their contents. See [validation and behavior limits](docs/VALIDATION.md).

## Repository contents

| Path | Purpose |
|---|---|
| `src/DarkestDaysTweaks` | Original mod source and embedded third-party notices |
| `compatibility/files` | Only the files needed for the loader compatibility fix |
| `compatibility/src/Il2CppInterop` | Corresponding modified library source |
| `scripts/Build-Compatibility.ps1` | Rebuild the modified libraries |
| `docs` | Behavior and validation limits |
| `licenses` | Full third-party license texts |

## Credits and licensing

The compatibility work uses **LavaGang/MelonLoader**, **BepInEx/Il2CppInterop**, the Unity 6000.5 correction by **RTBRuhan** and **XLG-WSM**, **Cpp2IL** by **Sam Byass/SamboyCoding**, and **TerraFX**. This is an **unofficial compatibility bundle**, not an official release of those projects. The upstream correction is [Il2CppInterop PR #292](https://github.com/BepInEx/Il2CppInterop/pull/292).

See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) and [`licenses`](licenses). Corresponding modified LGPL source is included. When redistributing the compatibility binaries, preserve their notices/licenses and provide the corresponding source on equivalent terms. Users may replace the dynamically linked libraries and debug modifications to them.

The original mod source is provided for review and reproducibility. No new open-source license is granted for Airam's original mod by this README. Third-party components retain their own licenses.
