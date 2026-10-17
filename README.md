# Dev, Mod & Cheat Menu for Obenseuer

A BepInEx 6 (Unity Mono) plugin for **Obenseuer**. I made this menu to develop and debug my own mods; it's a tool, not a polished
cheat trainer. Press **F9** in game.

## Features

- **Mod settings**: every installed BepInEx mod's config, editable in game; changes apply at once.
- **Cheats**: god mode, frozen needs, money, skills, items, furniture, Tenement upgrades, time of day and speed.
- **Teleport**: your own saved points, and every location you have visited.
- **Inspector**: click an object in the world to see what it is made of.
- **Hot reload**: rebuild your mod and reload it without restarting the game.
- **FPS window** with a frame-time graph.

## Mod settings (Mods tab)

- Every mod gets a collapsible section with its BepInEx config: on/off switches, dropdowns, numbers with − and +, text fields.
  Nothing is needed from the mod: the menu reads the plugin's standard `Config`.
- Changes are saved to the mod's `.cfg` right away and the mod is notified, the same way ConfigurationManager does it.
- Hover a setting to see its description. Some settings only take effect after a restart; that depends on the mod.

## Cheats

- **Player**: god mode, frozen needs, refill needs and health, set or add OC and RM, skills 1 to 10.
- **Items**: search all items and spawn any amount (what doesn't fit drops at your feet). Key items like the passport.
  Clear inventory (worn items stay).
- **Furniture**: search and add to the build inventory, with amount and skin. Clear the build inventory.
- **Tenement**: every building and apartment upgrade as a dropdown of its levels. Upgrading pulls in what it requires;
  downgrading lowers what depended on it. Open the shop without waiting for the cleaning.
- **Time**: shift the clock by hours, jump to 00:00, 06:00, 12:00 or 18:00, pick the day of the week, game speed up to 100x.
- **Teleport**: save a point with a name and click to go back; a point in another scene loads that scene. Every location you
  have entered is listed too.

## Dev tools

- **Inspector** (see below).
- **Reload game**: saves to a quick save and loads it, or reloads the last save without saving.
- **FPS window**: current FPS, 90th and 99th percentile frame times, and a graph of the last 5 seconds. It stays open with the
  menu closed.

### Inspector

1. Open the menu (F9) and go to the Inspector tab.
2. Left-click an object in the world, outside the menu window. Your own character is skipped.
3. You get its parents and children as buttons (click one to select it), and the details: components, collider, materials with
   their shader values, and which scene components reference it.
4. **Refresh** runs the full scan, including references (a short hitch). **Save to file** writes the report to
   `BepInEx/DevMenu/inspect-latest.txt`.

Without opening the menu: set `Keys > Inspect` in the menu's own settings. The key inspects whatever is under the crosshair,
shows the report on screen and saves it.

### Adding your mod to the menu

Your mod's settings show up by themselves. To add buttons and a status line, put a static class named `DevMenu` in the same
namespace as your plugin class. No reference to this mod is needed.

```csharp
namespace MyMod
{
    internal static class DevMenu
    {
        // Optional: a live line at the top of your section.
        public static string Status() => $"Things tracked: {Tracker.Count}";

        // Every public static void method without parameters becomes a button.
        [System.ComponentModel.Description("Reset the tracker")]
        public static void ResetTracker() => Tracker.Reset();
    }
}
```

- The button label is the `[Description]`, or the method name.
- An exception in your method is caught and logged; the menu keeps working.
- Without this mod installed the class is just a few unused lines.

### Hot reload

1. Put your mod's dll in `BepInEx/scripts/<YourMod>/` instead of `BepInEx/plugins`. This menu loads it from there; BepInEx doesn't.
2. Rebuild the dll while the game runs, then press **F10** (or Tools → Reload scripts). The old copy is unloaded and the new one loaded.
3. Your plugin must clean up after itself in `OnDestroy`: unpatch Harmony (`harmony.UnpatchSelf()`), unsubscribe from events,
   destroy any UI or objects it created. Otherwise the old and the new copy both run.
4. Anything your mod only sets up when a scene or object starts won't happen again for what's already there. Set it up for
   existing objects in `Awake`, or change the scene.

- The dll is loaded from memory, so the file isn't locked and you can overwrite it.
- Old copies stay in memory until you quit the game. That's fine for development.
- Don't keep the same mod in both `plugins` and `scripts`: the copy in `scripts` is then skipped.

## Keys

| Key | Action |
|---|---|
| F9 | open or close the menu |
| F10 | reload the mods in `BepInEx/scripts` |
| Inspect | none by default |

All keys, the font size and the colours are in the menu's own settings (Mods tab), or in
`BepInEx/config/obenseuer.absurdmane.devmenu.cfg`.

## Install

1. Install [BepInEx 6 (Unity Mono)](https://builds.bepinex.dev/projects/bepinex_be) into the game folder.
2. Put `DevModCheatMenu.dll` into `BepInEx/plugins/DevModCheatMenu/`.

Cheats change your save like normal play does: make a backup save if you care about it. Made for game version 0.5.08.

## Building

Requirements: .NET SDK 6+ and the game with BepInEx 6 installed.

1. Copy `local.props.example` to `local.props` and set `GameDir` to your Obenseuer folder.
2. `dotnet build DevMenu -c Release` (add `-p:Deploy=true` to copy the dll into the game's plugins folder).

The sources call it DevMenu; the dll, its folder and the release archive use the full name (`DevModCheatMenu`).

## License

MIT © absurdmane, see [LICENSE](LICENSE).
