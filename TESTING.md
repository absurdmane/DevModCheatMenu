# DevMenu — check list

Mark OK / FAIL + a note. Log: `BepInEx\LogOutput.log` — `Dev, Mod & Cheat Menu 1.0.0 loaded`, `Dev menu section: Utilities Not Included`.

- [ ] F9 opens the window in the Tenement; the cursor is free, the character doesn't move or look around; F9 / X closes it and control comes back.
- [ ] Typing in a search field doesn't trigger game hotkeys (inventory, map, ...).
- [ ] Tenement: building electricity Shit → Good → Best; the level label and ✓ follow; the breaker/wiring visuals change.
- [ ] Tenement: a level with requirements applies them too (the notice lists them). Note any line that errors.
- [ ] Tenement: "Linked upgrades" on (default): lowering building electricity to None also lowers what required it
  (apartment electricity, stairwell levels if they need it); the notice lists them. Off: only the picked line changes, both ways.
  The checkbox state survives a restart (`Tenement.LinkedUpgrades` in the config).
- [ ] Tenement: "Open the shop" removes the plywood at once; "Close the shop again" + reload brings it back.
- [ ] Tenement: "None" for electricity and water works; the save loads fine afterwards.
- [ ] Player: god mode, frozen needs, refill, money, skills.
- [ ] Items: search works; an item and an amount > stack land in the inventory / at the feet. Key items: the passport appears.
- [ ] Furniture: search, add one; it shows in the build menu. Skin > 0 gives that skin.
- [ ] Items / Furniture pager: with more than 50 results, "Page N / M" with First / Prev / Next / Last above and below the rows;
  Prev/First greyed on page 1, Next/Last on the last; "X of Y, 51–100 shown" follows; the bottom pager jumps back to the top of
  the page; typing a new search goes back to page 1; the buttons don't shift as the page number grows. Narrow window / big font:
  First and Last drop out, nothing sticks out. Key items (a short list): no pager.
- [ ] Teleport: save a point, walk away, teleport back; points survive a restart.
- [ ] Teleport: a point saved in another scene loads that scene and puts you on the point.
- [ ] Teleport: Locations fill up as scenes are visited; an entrypoint click loads the scene there; travel is refused while sitting/sleeping/waiting and in a conversation; Interior Bazaar Rob-in loads (player report of an endless loading screen).
- [ ] Mods: sections start collapsed; hovering a setting row shows its description next to the mouse; `[Mod] Enabled` is the last row.
- [ ] Inspect key (set it first) on a station, a fridge, a sink, a lamp: the overlay shows, `inspect-latest.txt` holds that one report.
- [ ] UI Inspector: inventory open, Inspect key over a slot: the note at the top right names the slot, `ui-inspect-latest.txt`
      has the hits, canvas, the slot and its parents up to the canvas, its children; the screen rects match what you see.
      Same with a container and a crafting station open, and over the empty striped block below the inventory.
- [ ] UI Inspector: Inspector → UI page; clicking an inventory slot selects it (notice "Selected …", frame) and the game does
      nothing with the click (no item picked up); World page → clicks pick objects again and the inventory reacts to clicks
      again after leaving the page / closing the menu.
- [ ] UI Inspector: the UI page shows the capture; clicking a parent / a child selects it; Save to file writes that one.
      With the inventory closed the Inspect key still inspects the world.
- [ ] Inspect key with the menu open: over an inventory element → Inspector, UI page with it and the green frame; over the world →
      Inspector, World page with that object; over the window → nothing.
- [ ] Inspector, World page: select `Apartments` (the whole building) from the parents list: no freeze; the report says
      "... N more" after 40 renderers; the box is drawn around the building.
- [ ] Frames: switching World ↔ UI shows only that page's frame.
- [ ] FPS window, GC lines: the GC count grows over time and the "last" time resets when it does; cyan ticks appear on the
      graph; a slow frame line appears after a hitch and says GC / no GC; the window keeps its height and width (no line
      wraps, also at a bigger UI font) and its right edge stays on screen; closing and reopening the window resets the count.
      If the GC count stays 0 for minutes while the heap MB jumps down, CollectionCount doesn't see Unity's collections: report it.
- [ ] Reflection probes → Render now, outdoors by day and by night: the info lists the probes and their period (no
      exception); compare the hitch in the FPS window with the periodic one. Indoors: "No timed probes" or the interior's.
- [ ] Spike profiler: Start shows "patching N / M" without freezing the game, then "on"; slow frames appear in
      `BepInEx/DevMenu/spikes.txt` with methods and the draw calls / GC line (or "?"); "Last:" shows the latest one; Stop
      writes the summary and the game runs at its usual FPS again; Cancel during patching works; hot reload / quitting
      with it on doesn't throw. Check the timed-scripts ms is plausible next to the frame ms.
- [ ] FPS window with the Inspector open on a big selection (Apartments, a canvas with many children): no big drop vs the
      menu closed; "Show more" adds 50 rows.
- [ ] Frames (#00FF00): with the menu open, a box around the Inspector's object (in its rotation, through walls) and a
      frame around the UI element selected on the UI page (moves with the selection); none with the menu closed, except
      for 3 s after the Inspect key. A big object (a scene root) and an object partly behind the camera draw sanely.
- [ ] Tabs: Tools is gone; Reload, FPS and Debug are there (tab rows wrap, nothing cut off at font 18).
- [ ] Reload: "Reload game" reloads with the changes kept; "Reload last save" drops them.
- [ ] Time: ±h buttons, 00/06/12/18 jumps, weekday, speed +1/+5/+10 capped at 100x, Reset.
- [ ] Tenement: two columns. Left: "Building", then "Floor N" headings with their apartments (floor 1: Player apartment, Shop;
  others "Apartment N · size"); exactly one item lit. Right: the picked one's upgrades under its name; "Open the shop" only on the Shop.
- [ ] Tenement: each column scrolls on its own (shrink the window's height); picking another item puts the right column back to the top;
  no horizontal scrollbar in the left column.
- [ ] Tenement: narrow window (or big font): the list goes above the upgrades in one scroll view; the page doesn't widen
  (no "tab is ... px wide" warning in the log).
- [ ] Tenement: dropdowns open under their row, picking applies and closes; switching apartment closes an open one.
- [ ] Player: checkbox buttons show the state; OC/RM field + Enter sets the amount; skills grid in 3 columns.
- [ ] FPS: the FPS window switch and the spike profiler work as before on the Tools tab.
- [ ] Debug: with no debug script loaded, the help text shows. A scripts plugin with a DebugScripts class gets a section
      (collapsible, open by default): its buttons run, Status refreshes; after Reload → Reload changed the section is the new
      code (no doubled section) and stays collapsed if it was. A button that throws shows a notice, the menu keeps working.
- [ ] Mods: each of our mods has a section; "Settings" edits its config (toggle a bool, pick an enum, type a number): the .cfg file
      changes and the mod reacts (e.g. UNI `LightsNeedPower` off → lamps back).
- [ ] Mods: EvenFasterSaveAndLoad → "Timings window on / off" shows a draggable window with the last load/save times.
- [ ] Mods: the UNI section shows its status and buttons.
- [ ] Hot reload: Unclicker built with -p:Deploy=scripts loads at start (Reload tab lists it, Mods tab shows its section); Reload → Reload changed reloads it (and only it):
  no doubled buttons or patches (one click = one craft), the info popup's Plant button comes back when it should, log lines not doubled.
