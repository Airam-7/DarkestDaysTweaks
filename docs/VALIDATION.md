# Behavior and validation

## Behavior

All three multipliers default to **1.0** and reject values below 1 in the menu. Settings files are clamped to supported bounds. Adjust a slider with the mouse or controller, or click the number to type an exact value. **Apply and save** confirms edits. A fresh installation leaves both silence switches OFF.

The stack multiplier applies to the native difficulty-adjusted stack calculator. It preserves native sentinel values and does not lower an already larger native stack limit. Increased limits are capped at 1,000,000 units per stack. Native restrictions on loaded ammo and differing item durability remain in force.

The durability options reduce future native wear, rather than repairing weapons or multiplying the maximum durability stored in a save. Firearm durability only affects weapons that already have native durability wear. This mod does not add a new firearm degradation system or modify ammunition consumption.

The silence switches suppress matching survivor footstep detection events independently for walking and running. They preserve the audible footsteps. The implementation targets native running states and excludes nonmatching noise assets, including shots, impacts, executions and environmental noise. Actual zombie response across gameplay situations still requires verification.

Opening the panel pushes the game's disabled action set at critical priority and suspends Unity menu dispatch. The mod reads the controller directly until the panel closes and its navigation controls are released. The simulation continues, so pause first if you need time to edit during a run. Settings are stored in `UserData/DarkestDaysTweaks.txt`. Decimal points and commas are accepted. **Restore settings** returns all multipliers to 1 and turns both silence switches OFF.

## Checked locally

- Release mod compilation, with no errors.
- Eleven automated settings regression checks.
- Steam startup with the corrected loader dependencies.
- Generation of fresh IL2CPP references without relying on the old generated assembly directory.
- Retention and execution of the updated Cpp2IL through the shipped generator configuration.
- Rendering of the complete English settings panel in 0.1.3.
- User confirmation that manual editing, settings saving and the 0.1.3 menu work.
- Package contents checked: the mod-only ZIP distributed on NexusMods contains one DLL; the separate compatibility ZIP on GitHub Releases contains six files. Neither includes personal configuration, game binaries, generated references or local diagnostic files.
- Corresponding source/license archive inspection for game binaries and personal paths.

## Not yet fully validated

- Every weapon type, wear event and difficulty/trait combination.
- Zombie response to all walking/running situations.
- Save/load behavior with enlarged stacks, particularly after lowering the multiplier or uninstalling.
- Other game builds, loaders, platforms or mod combinations.

Treat the current gameplay feature set as experimental. The numerical caps are mod safeguards, not proven maximum stable values of the game engine. Divide enlarged stacks before returning to vanilla limits or removing the mod.
