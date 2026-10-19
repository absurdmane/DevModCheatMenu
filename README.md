# Dev, Mod & Cheat Menu for Obenseuer

A BepInEx 6 (Unity Mono) plugin for **Obenseuer**. I made this menu to develop and debug my own mods; it's a tool, not a polished
cheat trainer. Press **F9** in game.

## Features

- **Mod settings**: every installed BepInEx mod's config, editable in game; changes apply at once.
- **Cheats**: god mode, frozen needs, money, skills, items, furniture, Tenement upgrades, time of day and speed.
- **Teleport**: your own saved points, and every location you have visited.
- **Inspector**: click an object in the world to see what it is made of.
- **UI Inspector**: point at a piece of the game's inventory screen to see its layout (sizes, anchors, layout groups).
- **Hot reload**: rebuild your mod and reload it without restarting the game.
- **FPS window** with a frame-time graph.

## Mod settings (Mods tab)

- Every mod gets a collapsible section with its BepInEx config: on/off switches, dropdowns, numbers with − and +, text fields.
  Nothing is needed from the mod: the menu reads the plugin's standard `Config`.
- Changes are saved to the mod's `.cfg` right away and the mod is notified, the same way ConfigurationManager does it.
- Hover a setting to see its description. Some settings only take effect after a restart; that depends on the mod.

## Cheats

- **Player**: god mode, frozen needs, refill needs and health, set or add OC and RM, skills 1 to 10.
- **Items**: search all items (50 a page) and spawn any amount (what doesn't fit drops at your feet). Key items like the passport.
  Clear inventory (worn items stay).
- **Furniture**: search (50 a page) and add to the build inventory, with amount and skin. Clear the build inventory.
- **Tenement**: pick the building or an apartment (listed floor by floor) on the left, its upgrades on the right, each a
  dropdown of its levels. Upgrading pulls in what it requires;
  downgrading lowers what depended on it (switch "Linked upgrades" off to change only the picked line). Open the shop without waiting for the cleaning.
- **Time**: shift the clock by hours, jump to 00:00, 06:00, 12:00 or 18:00, pick the day of the week, game speed up to 100x.
- **Teleport**: save a point with a name and click to go back; a point in another scene loads that scene. Every location you
  have entered is listed too.

## Dev tools

- **Inspector** and **UI Inspector** (see below).
- **Reload** tab: save to a quick save and load it, or reload the last save without saving; hot reload (Reload changed /
  Reload all) of the mods and debug scripts in `BepInEx/scripts`.
- **FPS** tab: the FPS window and the spike profiler.
- **FPS window**: current FPS, 90th and 99th percentile frame times, and a graph of the last 5 seconds. It stays open with the
  menu closed. GC lines help tell garbage collection hitches apart: how many collections ran since the window opened and how
  long ago the last one was, the average gap between them, the managed heap size, and the last frame over 33 ms with whether
  a collection ran in it. A cyan tick on the graph marks a collection.
- **Spike profiler**: finds what makes a frame slow. Start it on the FPS tab; it times the game's scripts and writes every
  frame over 33 ms to `BepInEx/DevMenu/spikes.txt` with the methods that took the longest. Stop adds a summary over all slow
  frames. Starting takes a few seconds (the game keeps running), and the game is a bit slower while it's on. Rendering,
  physics and loading aren't timed; the report shows how much of the frame the scripts account for. The threshold and
  whether coroutines are timed are in the config (`[Spike profiler]`).
- **Debug** tab: buttons of debug scripts, one-off probes that change and run again without restarting the game (see below).

### Inspector

1. Open the menu (F9) and go to the Inspector tab.
2. Left-click an object in the world, outside the menu window. Your own character is skipped.
3. You get its parents and children as buttons (click one to select it), and the details: components, collider, materials with
   their shader values, and which scene components reference it.
4. **Refresh** runs the full scan, including references (a short hitch). **Save to file** writes the report to
   `BepInEx/DevMenu/inspect-latest.txt`.
5. While the menu is open, a green box shows the selected object in the world.

Without opening the menu: set `Keys > Inspect` in the menu's own settings. The key inspects whatever is under the crosshair,
shows the report on screen and saves it, and the green box shows for 3 seconds.

### UI Inspector

For modders who change the game's screens (inventory, containers, stations). It's the **UI** page of the Inspector tab;
the other page, **World**, is the Inspector above.

1. Open the game's inventory (or a container, a station), then the menu: Inspector tab, **UI** page.
2. Click an element of the game's screen, outside the menu window. The game doesn't act on these clicks while the UI page
   is open.
3. You get the element in full (active state, anchors, pivot, size, position, rect in screen pixels, layout groups and
   their settings, layout elements, size fitters, images, texts, scroll views, masks), and its parents and children as
   buttons (click one to select it). A green frame shows the selected element.
4. **Save to file** writes `BepInEx/DevMenu/ui-inspect-latest.txt`: every element under the mouse, the canvas and its
   scaler, the element and each parent up to the canvas, and its children one line each.

Without opening the menu: with the game's inventory open, the `Keys > Inspect` key captures the element under the mouse,
shows a short note, saves the report and shows the frame for 3 seconds. With the menu open the key takes what's under the
mouse outside the window (the UI if there is any, else the object in the world) and opens the matching page.

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
2. Rebuild the dll while the game runs, then press Reload → Reload changed (or your `ReloadScriptsKey`, none by default). The mods whose dll changed are unloaded and loaded again; Reload all reloads every one.
3. Your plugin must clean up after itself in `OnDestroy`: unpatch Harmony (`harmony.UnpatchSelf()`), unsubscribe from events,
   destroy any UI or objects it created. Otherwise the old and the new copy both run.
4. Anything your mod only sets up when a scene or object starts won't happen again for what's already there. Set it up for
   existing objects in `Awake`, or change the scene.

- The dll is loaded from memory, so the file isn't locked and you can overwrite it.
- Old copies stay in memory until you quit the game. That's fine for development.
- Don't keep the same mod in both `plugins` and `scripts`: the copy in `scripts` is then skipped.

### Debug scripts

A one-off test (a few lines calling the game) doesn't need its own mod or a game restart. Put it in a small plugin of yours
with GUID `obenseuer.absurdmane.<name>` and a static class `DebugScripts` in the same namespace as the plugin class:

```csharp
internal static class DebugScripts
{
    [System.ComponentModel.Description("Render reflection probes now")]
    public static void RenderProbes() { /* ... */ }

    public static string Status() => "shown above the buttons, refreshed while the tab is open";
}
```

It gets a section on the Debug tab, by the same rules as the Mods tab's `DevMenu` class. Build it with `-p:Deploy=scripts`;
after each change press Reload → Reload changed and run the button again.

## Keys

| Key | Action |
|---|---|
| F9 | open or close the menu |
| `ReloadScriptsKey` (none by default) | reload the mods in `BepInEx/scripts` |
| Inspect | none by default; with the game's inventory open it inspects the UI under the mouse |

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
