# Steps to Set Up a Race

A concrete, step-by-step guide to what's built and how to finish/extend it. Written
against the actual `RaceCourse_CitySprint` setup in `TestScene`, which is now a working
5-car (player + 4 AI) point-to-point sprint with a hover-trigger start popup.

---

## 1. What's already done for you

- **4 opponents wired in**: `Assets/Racing/OpponentPresets/SupraRival.asset`,
  `WrxRival.asset`, `LfaRival.asset`, `GolfRival.asset` — using the Toyota Supra MK4,
  Subaru WRX STi, Lexus LFA, and VW Golf GTI "- AI" prefabs respectively. All 4 are
  assigned into `Assets/Racing/Races/CitySprintTest.asset` (`opponentCount = 4`).
- **Grid expanded to 5 slots** (`GridSlot_Player` + 4 AI slots) under
  `RaceCourse_CitySprint`, repositioned relative to the path's actual start point
  (the one you hand-corrected to avoid the building), not the course object's original
  position.
- **`RaceTrigger_CitySprint`** placed 20m behind the grid, along the path's approach
  direction — drive toward the start line and you'll enter it naturally.
- **Hover-to-prompt**: the trigger no longer prompts instantly on entry. Stay inside it
  for `hoverSecondsToPrompt` (default 1.5s) and the popup appears.
- **Start/Cancel popup**: shows the race name and details (opponent count, countdown
  length) pulled live from the `RaceDefinition`, with Start and Cancel buttons plus Esc
  as a Cancel shortcut.
- **Countdown freeze**: every car (player included) is physically frozen (kinematic
  Rigidbody) from the moment Start is pressed until the countdown hits zero — nobody can
  jump the start regardless of input source.
- **Results popup**: shows standings on finish, closes on Esc, which also returns
  `RaceManager` to `Idle` (via `ResetToIdle()`) so free-roam resumes cleanly.
- **`RaceDevTrigger`/`R` key removed** — the real trigger replaces it.

## 2. Test it

1. Open `TestScene`, press Play.
2. Drive toward `RaceTrigger_CitySprint` (it's ~20m behind the grid start, along the
   corrected path's approach direction).
3. Hold inside the trigger for ~1.5s — the popup should appear with "City Sprint",
   opponent count, and countdown length.
4. Press **Start** (or **Cancel** to back out and keep free-roaming).
5. All 5 cars should sit frozen through "3-2-1", then launch together on "GO".
6. Watch the position readout (top-right) update as you race.
7. Cross the finish line — results popup appears. Press **Esc** to close it and return
   to free roam.

**I have not personally watched this exact 5-car flow run live** (this session's
back-and-forth was mostly building/wiring it) — please do one full pass yourself and
flag anything that looks off, the same way you caught the earlier countdown/positioning
bugs.

## 3. Things you'll probably want to adjust yourself

### 3.1 Swap in different cars
The 4 presets default to Supra/WRX STi/Lexus LFA/Golf GTI. To change one: select the
`.asset` in `Assets/Racing/OpponentPresets/`, drag a different `"... - AI.prefab"` into
its `Vehicle Prefab` field. Available AI-ready prefabs not currently used:
`Assets/BxB Studio/MVC Getting Started/Prefabs/Vehicles/Cars/2020 Porsche Taycan Turbo S
- AI.prefab` (and the BMW, which the player already drives).

### 3.2 Make the path more curved
The path (`AIPath_Main` under `RaceCourse_CitySprint`) currently has the 5 waypoints you
already adjusted once to dodge a building — I left it as-is rather than redesigning it,
since I don't know what shape you actually want. To add curvature:
1. Select `AIPath_Main`, expand it in the Hierarchy.
2. Duplicate a `Waypoint_N` (Ctrl+D), drag it sideways off the straight line in the
   Scene view — the AI follower steers through waypoints in array order, so an offset
   waypoint between two others creates a curve.
3. Select `AIPath_Main` itself and confirm the new waypoint is in the `Waypoints` array
   at the right index (drag it into place if it landed at the end instead).
4. Same process you already used once — no baking, no Play Mode requirement, just drag
   and test.

### 3.3 Real overtaking (currently opponents will queue, not pass)
This is a known gap, not something I could fully close this session:
`SimpleAIPathFollower` has a `Traffic` mode that brakes for a car ahead, but no logic to
actually change lanes onto `RaceCourse.alternatePaths` to get around it. With 4 AI cars
on one line, expect bunching/queueing rather than dynamic overtaking. To improve this
yourself (or ask me to build it in a follow-up session):
1. Author 1-2 alternate paths roughly parallel to `AIPath_Main` (same "Create AI Path
   Here" + drag-waypoints workflow, offset sideways at the straights).
2. Assign them to `RaceCourse_CitySprint.alternatePaths` — `RaceManager` already
   round-robins opponents across `mainPath` + `alternatePaths` at spawn time via
   `ChoosePathForOpponent`, so this alone gives each car a different line, though they
   still won't dynamically switch mid-race.
3. Actual mid-race lane-switching (detect blocked + steer toward the clear alternate)
   would need new logic in `SimpleAIPathFollower.FixedUpdate` — flagged in
   `TESTING_AND_ROADMAP.md` §4.3 as the next real AI improvement.

### 3.4 Hover time / trigger size feel
`RaceTrigger_CitySprint`'s `Hover Seconds To Prompt` (1.5s) and its `BoxCollider` size
(14x4x14) are reasonable defaults, not tuned against actual play feel. Adjust directly
on the component if the prompt feels too eager/sluggish, or the trigger volume feels too
small/large to drive into naturally.

### 3.5 Popup visual polish
The Start/Cancel popup and results panel were built programmatically (plain
`Image`/`TextMeshProUGUI`, solid colors, no custom art) to get the flow working
end-to-end. If you want them to match a particular visual style, that's straightforward
Inspector/Scene-view editing on `_GameController/Canvas/RacePromptPanel` and
`.../RaceHUD/ResultsPanel` — swap in real background sprites, adjust fonts/colors,
reposition — no code changes needed for pure visual changes.

## 4. Reference: what got renamed/moved this session

- `AI_Opponent_1` (a standalone test car) was deleted — it conflicted with the real
  `BeginRace()` spawn flow (same grid slot, ignored race state).
- `RaceDevTrigger.cs` was deleted — superseded by `RaceTrigger_CitySprint`.
- If you had anything referencing the old grid slot positions (before the 5-slot
  repositioning), re-check it — the whole grid moved to align with the corrected path.
