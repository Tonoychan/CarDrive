# Racing System — Testing Guide & Roadmap

Companion to `PLANNING.md`. That file tracks phase-by-phase status; this one is a
practical guide: how to test what's built, how to use the custom editor tools (with a
worked example), and a prioritized list of what's left — including everything that was
out of scope for the initial build.

Last updated: 2026-08-29.

---

## 1. Before you start: the one thing to know

**MVC's real AI system (`VehicleAIPath`/`VehicleAIPathFollower`) is Pro-only and this
project has Community.** Everything AI-related here runs on a custom replacement —
`Racing.AI.SimpleAIPath` / `SimpleAIPathFollower` (`Assets/Scripts/Racing/AI/`) — built to
mirror MVC's naming so it's easy to read and easy to swap out later. It drives cars via
direct Rigidbody physics, not MVC's engine simulation. See `PLANNING.md`'s "MVC Pro vs
Community" section for the full story and the swap-back path if you ever buy Pro.

---

## 2. How to test what's already built

### 2.1 Quick smoke test (2 minutes) — confirm the AI actually drives

This is the fastest way to confirm the whole foundation works.

1. Open `Assets/Scenes/TestScene.unity`.
2. In the Hierarchy, find `AI_Opponent_1` — it's already placed at the start of
   `RaceCourse_CitySprint`'s path with a working `SimpleAIPathFollower`.
3. Press **Play**.
4. **Keep the Editor window focused/active.** Unity's Play Mode loop in this project
   stalls when the Editor isn't focused — `Time.frameCount` stops advancing entirely.
   Click into the Game or Scene view periodically if the car looks frozen.
5. Watch `AI_Opponent_1` drive itself down the road, following the waypoints under
   `AIPath_Main`. It should smoothly turn at each waypoint and end up near
   `Waypoint_4` after ~10-15 seconds.

If it doesn't move: check the Console for errors, and confirm `AI_Opponent_1` has a
`Simple AI Path Follower` component with `Path` assigned to `AIPath_Main`.

### 2.2 Full race flow test

1. With the scene open (not yet in Play mode), select `_GameController` and confirm it
   has a `Race Manager` component.
2. Enter Play mode.
3. Open the C# Console / use a temporary test script, or (once Phase 3 is wired) drive
   into a `RaceTrigger` volume and confirm the race:
   ```csharp
   var course = GameObject.Find("RaceCourse_CitySprint").GetComponent<Racing.RaceCourse>();
   Racing.RaceManager.Instance.BeginRace(course);
   ```
4. Confirm:
   - Player teleports to `GridSlot_Player`.
   - A second BMW spawns at `GridSlot_1` (opponent, from `BmwRival.asset`).
   - `RaceManager.Instance.State` reads `Countdown`, then `Racing` after ~3 seconds.
   - Drive the player car to the `FinishLine` object — confirm `State` becomes
     `Finished` and `RaceEvents.RaceFinished` fires (add a `Debug.Log` subscriber if you
     want to see it directly, or wire up `RaceUIController` first — see §4.1).

### 2.3 Editor tools test

1. Select `RaceCourse_CitySprint` in the Hierarchy.
2. In the Inspector, below the default fields, you'll see **Race Setup Tools** — buttons
   for `Add Grid Slot`, `Auto-Arrange Grid`, `Create Finish Line`, `Create AI Path Here`.
   Yellow warning boxes tell you what's missing (wrong grid slot count, no path, no
   finish line assigned).
3. With `RaceCourse_CitySprint` selected, the Scene view shows draggable position/
   rotation handles on each grid slot and the finish line — try nudging one and confirm
   it updates live.
4. Open **Racing → Race Definitions** from the menu bar. Confirms/lists existing
   `RaceDefinition` and `RaceOpponentPreset` assets, with `New Race Definition` /
   `New Opponent Preset` buttons.

### 2.4 What you can't test yet
- **Phase 3 UI (trigger prompt)**: `RaceTrigger`/`RacePromptUI` exist but aren't placed
  in the scene or wired to Canvas elements. See §5.1.
- **Phase 5 HUD**: `RaceUIController` exists but has no `TMP_Text`/panel references
  assigned anywhere, so nothing renders yet. See §4.1.
- **Phase 7 garage**: `RaceGarageController` works in isolation (you can call
  `Purchase`/`ApplyOwnedPartsToVehicle` from a test script) but has no UI.

---

## 3. Worked example: authoring a race from scratch

This walks through building a brand-new race the same way `RaceCourse_CitySprint` was
built, using the actual tools — so you can make a second track, or redo this one better.

**Goal:** a new point-to-point sprint with 2 opponents.

### Step 1 — Create the data assets
1. Menu **Racing → Race Definitions**.
2. Click **New Opponent Preset** twice (creates `NewOpponentPreset.asset`,
   `NewOpponentPreset 1.asset` in `Assets/Racing/OpponentPresets/`). Rename them, set
   `vehiclePrefab` to `Assets/BxB Studio/MVC Getting Started/Prefabs/Vehicles/Cars/
   2005 BMW M3 GTR E46 - AI.prefab` (or any other MVC vehicle prefab with a `Vehicle`
   component and a `Rigidbody`), tweak `targetSpeedMultiplier` per car so they don't
   move identically.
3. Click **New Race Definition**. Rename it, set `opponentCount = 2`, drag both presets
   into `opponentPresets`, set `countdownSeconds` (default 3 is fine).

### Step 2 — Build the scene rig
1. In the Hierarchy: right-click → **Create Empty**, name it e.g. `RaceCourse_Loop2`,
   reset its position to somewhere sensible on your road.
2. Add component: `Race Course`.
3. Drag your new `RaceDefinition` asset into the `Definition` field.
4. Click **Auto-Arrange Grid** — this creates `opponentCount + 1` (here: 3) grid slots
   arranged in a 2-column staggered formation behind the object's position, and names
   slot 0 `GridSlot_Player`.
5. Click **Create Finish Line** — creates a `FinishLine` child 50m ahead (Z+50 local).
   Drag it (via the Scene view handle or Inspector) to where you actually want the race
   to end.
6. Click **Create AI Path Here** — creates `AIPath_Main` with 2 seed waypoints
   (`Waypoint_0` at the course's position, `Waypoint_1` 50m ahead). This auto-assigns
   itself to `Course.mainPath`.

### Step 3 — Author the path (the part that actually matters)
This is where you replace MVC Pro's Bezier tool with plain Transform editing:
1. Select `AIPath_Main` in the Hierarchy, expand it — you'll see `Waypoint_0`,
   `Waypoint_1`.
2. Select each waypoint and drag it in the Scene view (standard Move gizmo) to trace
   your actual road. To add more waypoints: duplicate an existing `Waypoint_N` child
   (Ctrl+D), rename it sequentially, reposition it, then select `AIPath_Main` and drag
   the new Transform into the `Waypoints` array in the Inspector at the correct index
   (or just append it — order matters, the follower drives them in array order).
3. Select `AIPath_Main`'s `Simple AI Path` component in the Inspector — `Is Valid` and
   `Total Length` (if you add a debug button, or just check via a test script) confirm
   the path is usable as soon as it has 2+ waypoints. No baking step, no Play Mode
   requirement — this is the main practical advantage over MVC Pro's system for casual
   iteration.
4. **Test drive-ability before finalizing**: place a spare AI car (see §2.1's
   `AI_Opponent_1` as a template — duplicate it, reassign its `Simple AI Path Follower.
   Path` to your new `AIPath_Main`, move it to your new start), press Play, watch it
   drive the path, adjust waypoints for any building/wall collisions (this is exactly
   what happened building `RaceCourse_CitySprint` — the first straight-line path clipped
   a building and had to be nudged sideways).

### Step 4 — Wire opponents & test
1. Back on `RaceCourse_Loop2`, confirm `Main Path` points at your new `AIPath_Main`.
2. Call `RaceManager.Instance.BeginRace(course)` (or wait for Phase 3's trigger once
   wired) and confirm both opponents spawn at their grid slots and drive.

---

## 4. Improvements & things left out of scope

Ranked roughly by effort-to-value. Each entry says what exists, what's missing, and a
concrete starting point.

### 4.1 HUD wiring — DONE, needs one visual confirmation pass
`RaceHUD` now exists under `_GameController/Canvas` (`CountdownPanel`/`CountdownText`,
`HUDPanel`/`PositionText`, `ResultsPanel`/`ResultsText` + a translucent background),
matching the existing SpeedMeter's TMP font. `RaceUIController` on `_GameController` has
all references wired. Confirmed programmatically that `BeginRace()` correctly flips
`CountdownPanel` active and updates its text via `RaceEvents`. **What's left**: a human
Play-mode pass, watching the Game view the whole time, to eyeball that the countdown/
position/results actually look right (font size, layout, readability against the 3D
scene) — automated verification kept getting interrupted by this session's Play Mode
instability (see §5). If it doesn't look right, the pieces to adjust are plain
`RectTransform`/`TextMeshProUGUI` settings on `RaceHUD`'s children — no code changes.

### 4.2 Place a real trigger + wire the prompt (small, closes Phase 3)
1. Create a trigger volume GameObject (Box Collider, `Is Trigger` on) somewhere in the
   free-roam area, add `Race Trigger`, assign its `Course`.
2. Build a small prompt Canvas panel (a background image + `TMP_Text` + a Button for
   touch), add `Race Prompt UI` to it, wire `Panel`/`Label`/`Confirm Button`.
3. Drive into the trigger, confirm the prompt shows, press E (or tap), confirm
   `BeginRace` fires.

### 4.3 Multi-lane overtaking (the actual "6+ opponents, real traffic" gap)
`RaceCourse.alternatePaths` exists and `RaceManager.ChoosePathForOpponent` already
assigns opponents to different paths round-robin — but `SimpleAIPathFollower` has no
logic to *change lanes* mid-race to pass a slower car ahead of it on the same path; it
just slows down (`TrafficPolicy.Traffic`) or ignores traffic entirely (`TrafficPolicy.
Racing`). For real overtaking:
- Author 1-2 alternate `SimpleAIPath`s roughly parallel to the main line (offset
  sideways at corners/straights).
- Add lane-switching logic to `SimpleAIPathFollower`: when `IsVehicleAhead` triggers and
  `trafficPolicy == Racing`, check if an alternate path's nearest point is clear, and if
  so steer toward it instead of just braking.
- This is a genuinely nontrivial AI behavior — budget real iteration time, same as the
  original plan's Phase 6 called out ("highest-effort tuning phase").

### 4.4 Garage UI (Phase 7 completion)
`RaceGarageController` (`Assets/Scripts/Racing/RaceGarage.cs`) has working
purchase/ownership/apply logic and zero UI. A minimal version: a Canvas panel listing
`availableParts`, a button per part showing owned/locked state, calling `Purchase()` and
then `ApplyOwnedPartsToVehicle(playerVehicle)`. No currency/economy exists yet — parts
are free to "purchase" (just unlocks). Adding a currency requires deciding how players
earn it (race winnings? Not designed yet — a real product decision, not just code).

### 4.5 Wrong-way detection
`RaceCourse.wrongWayDotThreshold` is declared but never read anywhere — it was scoped in
Phase 1's data model but the check was never wired into `RaceManager`. To add: in
`UpdatePositions()`, compare each vehicle's forward direction against the path's local
tangent near its `progressIndex`; if the dot product is below the threshold, the vehicle
is facing backward — flag it (UI warning, or just exclude from position ordering until
it turns around).

### 4.6 Ground surface tuning (Phase 6 adjacent)
Noted in `PLANNING.md`'s Phase 0 section: `TestScene`'s `Plane`/road meshes have no
`VehicleGroundMapper` component, so all surfaces use MVC's default grip/audio. Not
blocking, but worth doing before final tuning — different surfaces (road vs. dirt
shoulders vs. sidewalks) should probably grip differently.

### 4.7 If you get MVC Pro
Revert the "keep it aligned" swap: in `RaceCourse.cs`, `RaceManager.cs`, and
`RaceOpponentPreset.cs`, change `Racing.AI.SimpleAIPath`/`SimpleAIPathFollower` back to
`MVC.AI.VehicleAIPath`/`VehicleAIPathFollower`, then author the real path via MVC's
official "AI Path Bezier" tool (Tools → Multiversal Vehicle Controller → per MVC's
Documentation.html, section "AI paths and path followers"). You'd gain: real
Ackermann-accurate steering, MVC's engine simulation on AI cars (matching player-car
feel), obstacle sensors, and `AI Zones` (Brake/Handbrake/NOS zones placed along the
path) — none of which the Community fallback replicates.

---

## 5. Known limitations (by design, not bugs)

- AI cars don't get MVC's engine sound/RPM/torque curves (arcade Rigidbody physics
  instead) — see §1.
- `SimpleAIPathFollower` steering is a damped yaw-rate, not true Ackermann wheel
  steering — cornering looks slightly different from the player's MVC-simulated car.
- No lane-changing/overtaking yet (§4.3).
- Editor Play Mode in this environment stalls when unfocused — keep the window active
  during testing, this isn't specific to the racing code.
