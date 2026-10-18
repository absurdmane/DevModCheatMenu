# Changelog

## 1.1.0

- Teleport: travelling to another scene is refused while sleeping, sitting, waiting, talking or using something
  (a chair left behind in the old scene kept the player from walking), and sets what a door sets for NPCs that
  follow the player. The list of unvisited build scenes is gone: it offered menus and test scenes, and the autosave
  made before the load then pointed at a scene that can't be played.
- Mods tab: key settings are set by pressing the key (click the button, press and release a key; Esc cancels,
  Backspace clears), instead of typing its name. Shortcuts take Shift/Ctrl/Alt held with the key.
- Hot reload has no default key any more: F10 was easy to press by accident and hangs the game for a moment.
  Use Tools > Reload changed, or set `[Keys] ReloadScriptsKey`.
- Hot reload reloads only the mods whose dll changed (Tools > Reload changed); Tools > Reload all reloads every one.
  The log shows how long each mod took to unload and load.
- Fix: hot reload now really loads the new build. Before, a reload ran the old code again.

## 1.0.0

- First release.
