# DevMenu — check list

Mark OK / FAIL + a note. Log: `BepInEx\LogOutput.log` — `Dev, Mod & Cheat Menu 1.0.0 loaded`, `Dev menu section: Utilities Not Included`.

- [ ] F9 opens the window in the Tenement; the cursor is free, the character doesn't move or look around; F9 / X closes it and control comes back.
- [ ] Typing in a search field doesn't trigger game hotkeys (inventory, map, ...).
- [ ] Tenement: building electricity Shit → Good → Best; the level label and ✓ follow; the breaker/wiring visuals change.
- [ ] Tenement: a level with requirements applies them too (the notice lists them). Note any line that errors.
- [ ] Tenement: "Open the shop" removes the plywood at once; "Close the shop again" + reload brings it back.
- [ ] Tenement: "None" for electricity and water works; the save loads fine afterwards.
- [ ] Player: god mode, frozen needs, refill, money, skills.
- [ ] Items: search works; an item and an amount > stack land in the inventory / at the feet. Key items: the passport appears.
- [ ] Furniture: search, add one; it shows in the build menu. Skin > 0 gives that skin.
- [ ] Teleport: save a point, walk away, teleport back; points survive a restart.
- [ ] Teleport: a point saved in another scene loads that scene and puts you on the point.
- [ ] Teleport: Locations fill up as scenes are visited; an entrypoint click loads the scene there; travel is refused while sitting/sleeping/waiting and in a conversation; Interior Bazaar Rob-in loads (player report of an endless loading screen).
- [ ] Mods: sections start collapsed; hovering a setting row shows its description next to the mouse; `[Mod] Enabled` is the last row.
- [ ] Inspect key (set it first) on a station, a fridge, a sink, a lamp: the overlay shows, `inspect-latest.txt` holds that one report.
- [ ] Tools: "Reload game" reloads with the changes kept; "Reload last save" drops them.
- [ ] Time: ±h buttons, 00/06/12/18 jumps, weekday, speed +1/+5/+10 capped at 100x, Reset.
- [ ] Tenement: dropdowns open under their row, picking applies and closes; switching apartment closes an open one.
- [ ] Player: checkbox buttons show the state; OC/RM field + Enter sets the amount; skills grid in 3 columns.
- [ ] Tools: frame monitor overlay; spikes appear in the log.
- [ ] Mods: each of our mods has a section; "Settings" edits its config (toggle a bool, pick an enum, type a number): the .cfg file
      changes and the mod reacts (e.g. UNI `LightsNeedPower` off → lamps back).
- [ ] Mods: EvenFasterSaveAndLoad → "Timings window on / off" shows a draggable window with the last load/save times.
- [ ] Mods: the UNI section shows its status and buttons.
- [ ] Hot reload: Unclicker built with -p:Deploy=scripts loads at start (Tools lists it, Mods tab shows its section); Tools → Reload changed reloads it (and only it):
  no doubled buttons or patches (one click = one craft), the info popup's Plant button comes back when it should, log lines not doubled.
