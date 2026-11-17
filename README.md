# Darkest Days Tweaks

Source code and MelonLoader compatibility files for **Into the Dead: Our Darkest Days** on Windows x64.

**The compiled mod and MelonLoader compatibility fix are available as separate downloads in [Releases](https://github.com/Airam-7/DarkestDaysTweaks/releases).** The mod DLL is not stored in the Git source tree. The combined clean installation package is also distributed through NexusMods.

## Downloads

| Download | Contents |
|---|---|
| [MelonLoader compatibility fix](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.3/MelonLoader-Compatibility-Fix-Unity-6000.5.11f1.zip) | Only the six compatibility files; no mod |
| [Darkest Days Tweaks 0.1.3](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.3/DarkestDaysTweaks-0.1.3-Mod.zip) | Only `Mods/DarkestDaysTweaks.dll` |

See the [v0.1.3 release page](https://github.com/Airam-7/DarkestDaysTweaks/releases/tag/v0.1.3) for installation details, source archives and validation limits. Official MelonLoader must be downloaded separately.

## Mod features

- Inventory stack size multiplier, from 1 to 1,000.
- Separate melee and firearm durability multipliers, from 1 to 1,000,000.
- Independent silent walking and silent running ON/OFF switches for footstep detection; audible footsteps are unchanged.
- F6 settings panel, manual numeric input, cumulative +2 / +5 / +10 buttons, and persistent settings.
- Reset to 1.0 restores the multipliers and switches silent footsteps OFF.

Version **0.1.3** uses English interface text and settings comments. Fresh installations default to vanilla values.

## Installation

1. Close the game and install the **official MelonLoader 0.7.3 x64** and its **.NET 6 Desktop Runtime x64** prerequisite. Get MelonLoader from [LavaGang's official release](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3), not from this repository.
2. Download the **MelonLoader compatibility fix** above and extract it into the game root, next to `IntoTheDeadOurDarkestDays.exe`. Merge folders and replace the compatibility files before the first launch.
3. Download the separate **Darkest Days Tweaks mod** above and extract it into the same game root. The DLL belongs in `Mods/`.
4. Start the game through Steam. Internet access is needed for the initial download of Unity reference libraries and generation of local IL2CPP references.
5. Press **F6**, or click **Mod settings - F6**. Pause the game before changing settings during a run. Choose **Apply and save** to confirm changes.

Alternatively, the **combined clean installation ZIP from NexusMods includes both packages** and can be extracted once after installing official MelonLoader. It contains the same seven installation files as the two Release downloads together.

If you need **only the MelonLoader compatibility fix**, use its separate Release download or copy the contents of [`compatibility/files`](compatibility/files) into the game root after installing official MelonLoader 0.7.3. These files do not install the mod.

The generated settings file is `UserData/DarkestDaysTweaks.txt`. Existing values are preserved when updating the mod. An already working installation of this exact compatibility stack only needs the separate mod download from Releases or the newer mod DLL from NexusMods; there is no need to replace the generator configuration again.

## Supported configuration

| Component | Tested version |
|---|---|
| Platform | Windows x64 / Steam |
| Steam game build | 26306995 |
| In-game version | v0.19.1.22694 |
| Unity | 6000.5.11f1 / metadata 107 |
| MelonLoader | 0.7.3 Open-Beta x64 |
| Mod source | 0.1.3 |

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

## Tests and validation

Run the small settings regression suite without installing the game or MelonLoader:

```powershell
dotnet run --project tests/SettingsChecks -c Release
```

The eleven checks cover native defaults, increased stacks, rounding, native sentinel values, overflow, numeric input, bounds and independent ON/OFF persistence. This repository deliberately excludes local diagnostic helpers, dumps, logs, temporary assets and personal test data.

Steam startup, reference regeneration without the old generated assemblies, the English panel, manual editing and settings persistence were checked locally. Full weapon-by-weapon gameplay, zombie response and save/load coverage remain incomplete. See [validation and behavior limits](docs/VALIDATION.md).

## Repository contents

| Path | Purpose |
|---|---|
| `src/DarkestDaysTweaks` | Original mod source and embedded third-party notices |
| `compatibility/files` | Only the files needed for the loader compatibility fix |
| `compatibility/src/Il2CppInterop` | Corresponding modified library source |
| `scripts/Build-Compatibility.ps1` | Rebuild the modified libraries |
| `tests/SettingsChecks` | Settings regression checks |
| `docs` | Behavior and validation limits |
| `licenses` | Full third-party license texts |

## Credits and licensing

The compatibility work uses **LavaGang/MelonLoader**, **BepInEx/Il2CppInterop**, the Unity 6000.5 correction by **RTBRuhan** and **XLG-WSM**, **Cpp2IL** by **Sam Byass/SamboyCoding**, and **TerraFX**. This is an **unofficial compatibility bundle**, not an official release of those projects. The upstream correction is [Il2CppInterop PR #292](https://github.com/BepInEx/Il2CppInterop/pull/292).

See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) and [`licenses`](licenses). Corresponding modified LGPL source is included. When redistributing the compatibility binaries, preserve their notices/licenses and provide the corresponding source on equivalent terms. Users may replace the dynamically linked libraries and debug modifications to them.

The original mod source is provided for review and reproducibility. No new open-source license is granted for Airam's original mod by this README. Third-party components retain their own licenses.
