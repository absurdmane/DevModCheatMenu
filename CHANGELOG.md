# Changelog

## 1.1.0

- Hot reload has no default key any more: F10 was easy to press by accident and hangs the game for a moment.
  Use Tools > Reload scripts, or set `[Keys] ReloadScriptsKey`.
- Mods tab: key settings are set by pressing the key (click the button, press a key; Esc cancels, Backspace clears),
  instead of typing its name. Shortcuts take Shift/Ctrl/Alt held with the key.
- Fix: hot reload now really loads the new build. Before, Mono handed back the copy loaded first (same assembly name),
  so a reload ran the old code again.

## 1.0.0

- First release.
