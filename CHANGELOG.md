# Changelog

## Unreleased

- Mods tab: settings in the order the mod registers them; a mod's `[Mod] Enabled` switch on top; smaller section headings.

## 1.2.0

- Tenement tab in two columns: the building and the apartments by floor on the left, the picked one's upgrades on the right.
- Tenement: a "Linked upgrades" switch. On (default): upgrading also gets the upgrades it needs, and downgrading lowers the ones that need it. Off: only the picked upgrade changes. Setting `[Tenement] LinkedUpgrades`.
- Items and Furniture search lists are paged, 50 a page (before, only the first 50 results could be reached).
- FPS window: garbage collection info. It counts collections and shows the gap between them, the managed heap size, and whether the last slow frame had a collection in it; collections are marked on the graph.
- The Tools tab is split into Reload (reload the game, hot reload) and FPS (the FPS window, the spike profiler).
- Debug tab: buttons from debug scripts (a static class `DebugScripts` in a plugin of yours), reloadable without restarting the game.
- Spike profiler (FPS tab): times the game's scripts and writes each slow frame to `BepInEx/DevMenu/spikes.txt` with the methods that took the longest, plus a summary when you stop it.
- UI Inspector: the Inspector tab has two pages, World and UI. On the UI page, click the game's inventory (or a container, a station) to select an element and see its layout, its parents and its children; the game doesn't get these clicks. With the menu closed and the inventory open, the Inspect key captures the element under the mouse and saves the report to `BepInEx/DevMenu/ui-inspect-latest.txt`.
- The Inspect key works with the menu open too: it inspects what's under the mouse outside the window (the UI if there is any, else the world) and opens the matching Inspector page.
- Inspector: a green frame around the selected object (World page) or UI element (UI page) while the menu is open, and for 3 seconds after the Inspect key with the menu closed.
- Fix: selecting a huge object in the Inspector (a whole building) froze the game. The report now lists at most 40 renderers, and the window shows the first part of a long report (Save to file has all of it).
- Inspector: the window no longer slows the game down while an object with many children is selected. Children are listed 50 at a time ("Show more").

## 1.1.0

- Teleport: travelling to another scene is refused while sleeping, sitting, waiting, talking or using something (a chair left behind in the old scene kept the player from walking), and sets what a door sets for NPCs that follow the player. The list of unvisited build scenes is gone: it offered menus and test scenes, and the autosave made before the load then pointed at a scene that can't be played.
- Mods tab: key settings are set by pressing the key (click the button, press and release a key; Esc cancels, Backspace clears), instead of typing its name. Shortcuts take Shift/Ctrl/Alt held with the key.
- Hot reload has no default key any more: F10 was easy to press by accident and hangs the game for a moment. Use Tools > Reload changed, or set `[Keys] ReloadScriptsKey`.
- Hot reload reloads only the mods whose dll changed (Tools > Reload changed); Tools > Reload all reloads every one. The log shows how long each mod took to unload and load.
- Fix: hot reload now really loads the new build. Before, a reload ran the old code again.

## 1.0.0

- First release.
