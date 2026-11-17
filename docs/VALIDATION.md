# Behavior and validation

## Behavior

All three multipliers default to **1.0** and reject values below 1 in the menu. Settings files are clamped to supported bounds. `+2`, `+5` and `+10` add to the displayed value and respect its limit. **Apply and save** confirms edits. A fresh installation leaves both silence switches OFF.

The stack multiplier applies to the native difficulty-adjusted stack calculator. It preserves native sentinel values and does not lower an already larger native stack limit. Increased limits are capped at 1,000,000 units per stack. Native restrictions on loaded ammo and differing item durability remain in force.

The durability options reduce future native wear, rather than repairing weapons or multiplying the maximum durability stored in a save. Firearm durability only affects weapons that already have native durability wear. This mod does not add a new firearm degradation system or modify ammunition consumption.

The silence switches suppress matching survivor footstep detection events independently for walking and running. They preserve the audible footsteps. The implementation targets native running states and excludes nonmatching noise assets, including shots, impacts, executions and environmental noise. Actual zombie response across gameplay situations still requires verification.

The settings panel does not pause the game or block every underlying gameplay input. Pause before editing during a run. The file is generated at `UserData/DarkestDaysTweaks.txt`; decimal point and comma input are accepted. Reset returns all multipliers to 1 and turns both silence switches OFF.

## Checked locally

- Release mod compilation, with no errors.
- Eleven automated settings regression checks.
- Steam startup with the corrected loader dependencies.
- Generation of fresh IL2CPP references without relying on the old generated assembly directory.
- Retention and execution of the updated Cpp2IL through the shipped generator configuration.
- Rendering of the complete English settings panel.
- User confirmation that manual editing, settings saving and the revised menu work.
- Package contents checked: the mod-only ZIP distributed on NexusMods contains one DLL; the separate compatibility ZIP on GitHub Releases contains six files. Neither includes personal configuration, game binaries, generated references or local diagnostic files.
- Corresponding source/license archive inspection for game binaries and personal paths.

## Not yet fully validated

- Every weapon type, wear event and difficulty/trait combination.
- Zombie response to all walking/running situations.
- Save/load behavior with enlarged stacks, particularly after lowering the multiplier or uninstalling.
- Other game builds, loaders, platforms or mod combinations.

Treat the current gameplay feature set as experimental. The numerical caps are mod safeguards, not proven maximum stable values of the game engine. Divide enlarged stacks before returning to vanilla limits or removing the mod.
