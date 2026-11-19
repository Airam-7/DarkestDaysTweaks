# Changelog

## 0.1.4

- Revised inventory stack calculations for inventories and individual stacks. Nested calculations apply the multiplier once.
- Moved melee and firearm wear adjustments to the game's weapon wear calculation. Firearms must already support durability loss.
- Replaced the increment buttons with sliders. Click a number to enter an exact multiplier.
- Open the panel with F6 or L3+R3. Use the D-pad or left stick to navigate and adjust values, A / Cross to select, and B / Circle to close.
- Renamed the reset button to **Restore settings**. It saves vanilla multipliers and turns both silent movement options off.
- Opening the panel requests exclusive game input and suspends navigation of the underlying Unity menus. Closing it restores the previous game controls after the navigation and close buttons are released.

### Installation

With the tested loader setup already installed, extract the mod ZIP into the game root and replace `Mods/DarkestDaysTweaks.dll`. Your settings carry over. Fresh installations also require official MelonLoader 0.7.3 x64, .NET 6 Desktop Runtime x64 and the separate compatibility ZIP. See the [installation guide](https://github.com/Airam-7/DarkestDaysTweaks#installation).

