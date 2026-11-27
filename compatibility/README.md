# MelonLoader compatibility for Unity 6000.5.11f1

This directory contains the fix used to run MelonLoader 0.7.3 with Steam build 26306995 of Into the Dead: Our Darkest Days. It does **not** contain MelonLoader or the compiled Darkest Days Tweaks mod.

## Apply the fix

Download the [MelonLoader compatibility fix ZIP](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.7/MelonLoader-Compatibility-Fix-Unity-6000.5.11f1.zip) from the [0.1.7 release](https://github.com/Airam-7/DarkestDaysTweaks/releases/tag/v0.1.7).

**The ZIP on NexusMods contains only `Mods/DarkestDaysTweaks.dll`. It does not include these compatibility files.** For a fresh installation, download and apply this fix separately.

1. Close the game and the loader console.
2. Install [official MelonLoader 0.7.3 x64](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3) and .NET 6 Desktop Runtime x64. **Do not launch the game yet.**
3. Extract the fix ZIP into the game root, beside `IntoTheDeadOurDarkestDays.exe`. Merge the `MelonLoader/` folder and replace the supplied files listed below. Do not place the archive inside `Mods/` or an extra nested folder.
4. Extract the mod-only ZIP from NexusMods or [GitHub Releases](https://github.com/Airam-7/DarkestDaysTweaks/releases/download/v0.1.7/DarkestDaysTweaks-0.1.7-Mod.zip) into the same game root, producing `Mods/DarkestDaysTweaks.dll`.
5. Start through Steam and let local reference generation finish. Internet access is required for the initial download of Unity reference libraries.

The original manual route remains available: copy **the contents of `files/`** into the game root before the first launch. If a first launch already failed with the original loader dependencies, close the game and console before applying the fix and retrying through Steam.

This fix download does not include the mod. If the same compatibility stack is already installed and working, updating the mod only requires the mod ZIP; do not replace the generator configuration again unnecessarily.

| Installed path | Purpose |
|---|---|
| `MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/Cpp2IL.exe` | Read this game's metadata 107 |
| `MelonLoader/Dependencies/Il2CppAssemblyGenerator/Config.cfg` | Retain the updated generator and regenerate local references |
| `MelonLoader/net6/Il2CppInterop.Common.dll` | Runtime interoperability support |
| `MelonLoader/net6/Il2CppInterop.Runtime.dll` | Corrected Unity 6000.5 class layout and initialization |
| `MelonLoader/net6/Il2CppInterop.HarmonySupport.dll` | Harmony interoperability support |
| `MelonLoader/net6/TerraFX.Interop.Windows.dll` | Required native Windows interop dependency |

`Config.cfg` contains **no game hash or generated references**. Its `DumperVersion` value intentionally matches MelonLoader 0.7.3's default package selector, `2022.1.0-pre-release.21`, so the loader does not overwrite the newer executable. That cache selector is **not** the executable's actual version. Unity reference libraries are downloaded and game references are generated on each user's computer when needed.

## Origins and modifications

- **Il2CppInterop:** [upstream PR #292](https://github.com/BepInEx/Il2CppInterop/pull/292), revision `72a5fd3cefbc580e1311d2e8242673e23e432023`. The upstream patch is by RTBRuhan, building on XLG-WSM's work. Corresponding source for the three redistributed projects is in `src/Il2CppInterop`.
- **Local source changes:** `global.json` permits the installed SDK through `rollForward=latestMajor`; `nameof(MonoField.GetValueInternal)` is replaced with the equivalent literal `"GetValueInternal"` to compile against this game's generated core library. No different behavior is introduced by that literal replacement.
- **Library build:** assembly version `1.5.1.0`, forced Release rebuild, `DebugType=none`, `DebugSymbols=false`. No personal debugging paths are embedded in the rebuilt libraries. The original TerraFX vendor binary is unchanged.
- **Cpp2IL:** unmodified official-project CI artifact 13427221023, reporting `2022.1.0-pr-629.1756+fda4977`, revision `fda4977b2450c181d6b86a40465f4ad966bda9a9`. [Source revision](https://github.com/SamboyCoding/Cpp2IL/tree/fda4977b2450c181d6b86a40465f4ad966bda9a9). The single-file manifest includes its own .NET runtime.
- **TerraFX.Interop.Windows:** NuGet version `10.0.22621.2`, source revision `fadce5a41fa5e6f0282e80e96f033d0a2c130991`.

Binary SHA-256 checksums are in `SHA256SUMS.txt`. These identify the distributed files; they are not a claim of approval by the upstream projects.

The remaining source directories from the upstream Il2CppInterop repository (CLI, generator, struct generator and unrelated development files) are not vendored here because they are not used to build these three libraries. The required Common, Runtime and HarmonySupport projects, build properties, assets and license are retained. The mod uses the official MelonLoader-supplied assembly generator; this fix does not replace that generator DLL.

## Rebuild

From the repository root:

```powershell
./scripts/Build-Compatibility.ps1 -GameDir "YOUR_GAME_FOLDER"
```

The script uses only a locally generated `Il2Cppmscorlib.dll` reference from the user's game. It never installs files into the game. No original game DLL is distributed in this repository.

## Limits

This combination was checked against Unity 6000.5.11f1 and MelonLoader 0.7.3. Updating the loader can replace these files. Another game update may require a new correction. Do not present this bundle as a general fix for every MelonLoader crash.

For support, record the game build, loader version and relevant error from `MelonLoader/Latest.log`; do not upload private account identifiers or save files unnecessarily. See the root validation document for the scope of testing.
