# Changelog

## 0.1.7

### Gameplay and accessibility

- Added **Location loot amount** (Vanilla to ×5) for eligible loot on first interaction and **Scavenging range** (Vanilla to ×3) for reachable locations.
- Added **Food shelf life** with Vanilla, ×2, ×3 and ×5 options for newly tracked perishables.
- Added **Visual detection grace**, with up to 2 seconds of extra reaction time in 0.5-second steps.
- Added **Recovering Health**, **Food satiety** and **Restoring Morale**, with Vanilla, ×2, ×3 and Max options. Deferred healing applies when the game resolves it, respecting vanilla phase timing and cancellation.
- Added **Notification volume** (0–100%) for popup sounds such as trait and status notices. Consumable-use sounds remain audible.

### Settings

- Reorganized settings into two columns, with automatic scaling to fit smaller resolutions, adjustable background opacity, and text and window size sliders in 10-point steps.
- Added an option to hide the settings button. F6 and L3+R3 still open the menu.
- Added keyboard navigation with arrows, Enter and Escape alongside controller and mouse support. Default gameplay values are labeled **Vanilla**.
- Added live reloading of saved edits to `UserData/DarkestDaysTweaks.txt`, with supported values documented in the generated file.

### Saved games and local backups

- Added **Saved games** with up to five compatible native saves and five recent phase backups per campaign. Browse phases with **Previous** and **Next**. Saves marked incompatible by the game are filtered out.
- Load a current save or restore a phase from the main menu or during a run. Restoring over the original slot requires confirmation and backs up that slot first.
- Added **Local backups**, which keeps the first captured save, the latest five phases and one manual copy per campaign. **Back up last game save** copies the last completed game save.
- **Load local copy** recovers a backup into an empty local slot without replacing an existing save. Verified with Steam in offline mode.

## 0.1.6

- Added an **Experience gain** multiplier from **×1 (vanilla)** to **×10** for future survivor XP gains.

## 0.1.5

- Added **Survivor inventory slots** to the settings panel. Set a minimum backpack capacity of up to **32 total slots** per survivor. The default (**1**) keeps vanilla capacity.
- Lowering the setting keeps occupied slots accessible.

## 0.1.4

- Revised inventory stack calculations for inventories and individual stacks. Nested calculations apply the multiplier once.
- Moved melee and firearm wear adjustments to the game's weapon wear calculation. Firearms must already support durability loss.
- Replaced the increment buttons with sliders. Click a number to enter an exact multiplier.
- Open the panel with F6 or L3+R3. Use the D-pad or left stick to navigate and adjust values, A / Cross to select, and B / Circle to close.
- Renamed the reset button to **Restore settings**. It saves vanilla multipliers and turns both silent movement options off.
- Opening the panel requests exclusive game input and suspends navigation of the underlying Unity menus. Closing it restores the previous game controls after the navigation and close buttons are released.

### Installation

With the tested loader setup already installed, extract the mod ZIP into the game root and replace `Mods/DarkestDaysTweaks.dll`. Your settings carry over. Fresh installations also require official MelonLoader 0.7.3 x64, .NET 6 Desktop Runtime x64 and the separate compatibility ZIP. See the [installation guide](https://github.com/Airam-7/DarkestDaysTweaks#installation).

