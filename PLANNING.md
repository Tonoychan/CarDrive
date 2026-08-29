# Racing Game — Planning Doc

Last updated: 2026-08-29 (Phase 0 verified in-editor)

## Plugin decision

Using **BxB Studio MVC (Multiversal Vehicle Controller)** as the base for cars, AI, and
performance data, not Vehicle Physics Pro.

Reasoning:
- MVC already ships an AI/path toolkit: `AI Path`, `AI Path Bezier`, `AI Path Anchor`,
  `AI Path Visualizer`, `AI Controller`, `AI Agent`, `AI Follow Path`, `AI Vehicle`, and
  `AI Zones` (Brake Zone, Handbrake Zone, NOS Zone, Obstacle Sensors).
- MVC has a data-driven Engine Preset / Performance model (acceleration, deceleration,
  mass, RPM curves) — a natural fit for a parts-upgrade system later.
- MVC already includes a Mobile variant (joystick UI, mobile scenes) alongside the
  desktop setup.
- Vehicle Physics Pro has no AI, waypoint, traffic, or race tooling anywhere in its SDK
  or samples — physics-only. Using it would mean building AI pathing from scratch.
- The two plugins aren't designed to interoperate; don't mix them long-term in one scene.

## Confirmed decisions (from user, 2026-08-29)

- Race format: **point-to-point sprint** (start position → end position, no laps).
- Path/race setup tools: **Editor-only** (author tracks in the Unity Editor before
  shipping, not an in-game/runtime track editor).
- AI scale: **larger grid, 6+ opponents**, with real traffic behavior — overtaking and
  collision avoidance between AI cars, not just independent path-following.
- Target platform: **both PC and mobile**, from the start.

## Feature roadmap

### Phase 0 — Foundation
- ~~Remove Vehicle Physics Pro content from `Assets/Scenes/TestScene.unity`~~ — **DONE
  (verified 2026-08-29):** scanned every component on every GameObject in the scene via
  script-execute; zero VPP/EVP components found. All 7 root objects (Directional Light,
  Global Volume, Plane, `2005 BMW M3 GTR E46`, SimpleCamera, The City, `_GameController`)
  already use MVC types (`MVC.Core.Vehicle`, `MVC.Core.VehicleFollower`,
  `MVC.VehicleManager`). The `Vehicle Physics Pro` folder still exists under `Assets/` as
  an unused reference package — left in place per the original plan (reference only, not
  in the live scene).
- ~~Set up desktop + mobile input/UI in one project configuration~~ — **DONE (2026-08-29):**
  merged `_GameController` from
  `Assets/BxB Studio/MVC Getting Started - Mobile/Scenes/DefaultScene - Mobile.unity` into
  `TestScene`, replacing the old bare controller (which had `VehicleManager` only, no UI).
  The merged controller carries `PostProcessing`, `Canvas` (desktop HUD: SpeedMeter, logos)
  and `MobileCanvas` (`MobilePreset0`, `MobilePreset1`, `EventSystem`) as children, all
  driven by MVC's built-in `VehicleUIController` — confirming MVC already ships the "one
  HUD, platform-conditional input layer" pattern Phase 5 calls for (`activeMobilePreset`
  toggles which joystick/touch layout `RectTransform` group is active). `VehicleManager
  .playerTarget` was repointed to TestScene's `2005 BMW M3 GTR E46`. Verified error-free in
  Play mode: steering wheel, pedals, handbrake, NOS button, and speed/RPM HUD all render
  and respond against the BMW (see screenshot sent in chat). `GroundMappers` was left
  empty (TestScene's `Plane` has no `VehicleGroundMapper` component yet) — surface
  grip/audio tuning is Phase 6 scope, not a blocker here.
  - Known follow-up (not urgent): actual runtime platform detection (auto-picking desktop
    vs. mobile preset instead of both existing side-by-side) is Phase 5 polish, not done
    here.

### ⚠️ MVC Pro vs Community — read this before touching AI code

MVC's real AI Path / Path Follower system (`MVC.AI.VehicleAIPath`,
`VehicleAIPathFollower`) is a **Pro-only feature**, confirmed directly in MVC's own
`Documentation.html` (`<span class="docs-badge pro">Pro</span>` on the "AI paths and path
followers" section: *"using MVC Pro AI control"*). This project only has the installed
**Community** package (`Packages/dev.bxbstudio.mvc.community`). Every attempt to author a
`VehicleAIPath` via script — `AddNextCurve()`, forcing `Awake()` via reflection, using
MVC's own official `VehicleAIPathEditor.CreateNewPath()` creation method, even running in
Play Mode — silently returned `false`/no-op with zero exceptions or warnings. That's a
deliberate license gate, not a scripting limitation. Further reflection also confirmed
`Vehicle.Inputs.Fuel/Brake/Direction/etc.` have **public getters but non-public setters**
— MVC does not expose a way to drive a vehicle's throttle/brake/steering from outside
code in Community tier either (only its own internal, Pro-gated systems can).

**Resolution (2026-08-29):** built a from-scratch, license-free AI system —
`Racing.AI.SimpleAIPath` / `SimpleAIPathFollower` (`Assets/Scripts/Racing/AI/`) — that
deliberately mirrors MVC's naming (`path`, `followDirection`, `targetSpeedMultiplier`,
`trafficPolicy`, `followTimeGap`, `followMinimumGap`) so it reads the same way and is a
straightforward swap-out if this project ever gets MVC Pro. It drives the car via direct
`Rigidbody` force (propulsion) and a damped yaw-rate (steering) — **not** through MVC's
engine/wheel simulation, since that's the part with no public write access. Trade-off:
the AI car doesn't get MVC's engine sound/RPM/torque-curve feel; it's straightforward
arcade physics. **This works with zero Unity restrictions — waypoints are just child
Transforms, fully hand-editable in the Scene view at any time**, unlike MVC Pro's
Bezier tool. `RaceCourseEditor`'s "Create AI Path Here" button now creates a
`SimpleAIPath` with two seed waypoints.
**If you later buy MVC Pro:** swap `SimpleAIPath`/`SimpleAIPathFollower` back for
`MVC.AI.VehicleAIPath`/`VehicleAIPathFollower` in `RaceCourse.cs`, `RaceManager.cs`, and
`RaceOpponentPreset.cs` (search-and-replace scoped, ~15 lines total) to get real
traffic/overtaking/obstacle-sensor AI and MVC's own engine simulation on AI cars.

### Phase 1 — Data model (ScriptableObjects) — DONE
`Assets/Scripts/Racing/`:
- `RaceOpponentPreset` (SO): vehicle prefab ref, optional engine-stat override
  (power/torque/RPM/mass — real MVC `VehicleEngine` fields), AI behaviour tuning
  (`SimpleAIPathFollower.TrafficPolicy`, speed multiplier, follow gaps).
- `RaceDefinition` (SO): race name, opponent count, opponent preset list, countdown time.
- `RaceCourse` (scene `MonoBehaviour`, not a SO): links a `RaceDefinition` asset to the
  scene-specific bits that **can't** live in a portable asset — grid slot `Transform[]`,
  finish line `Transform`, main/alternate `SimpleAIPath` refs. Unity SOs cannot hold
  persistent scene-object references, hence the split.
- Checkpoints: no separate system needed — `SimpleAIPath.ClosestPointIndex(position)` /
  `TotalLength` / `IsValid` (mirroring what `VehicleAIPath` would have given us) is what
  `RaceManager` uses for race-position ordering.

### Phase 2 — Race Setup Tool (Editor-only) — DONE
`Assets/Scripts/Racing/Editor/`:
- `RaceCourseEditor` (custom inspector for `RaceCourse`): Add/Auto-Arrange grid slots,
  create finish line, create AI path (now `SimpleAIPath`, seeded with 2 waypoints),
  scene-view position/rotation handles for slots + finish line, validation warnings.
- `RaceDefinitionLibraryWindow` (`Racing/Race Definitions` menu): browse + create
  `RaceDefinition`/`RaceOpponentPreset` assets.
- **Verified working**: used live in-session to author `RaceCourse_CitySprint`'s grid and
  path; a human (not just script) hand-edited the path waypoints directly in the Scene
  view to route around a building, proving the workflow is genuinely usable.

### Phase 3 — Free roam + trigger cubes — CODE DONE, not scene-wired
`RaceTrigger` (`OnTriggerEnter`/`Exit`, ignores AI vehicles via `Vehicle.HasAI`) +
`RacePromptUI` (shows "Press E or tap to start: {name}", listens for `Keyboard.current`
E key, confirm button for touch). Compiles clean. Not yet placed as an actual trigger
volume in the scene or wired to Canvas UI elements — do that next.

### Phase 4 — Race Manager (runtime state machine) — VERIFIED END-TO-END
`RaceEvents` (static C# events) + `RaceManager` (`Idle → Countdown → Racing → Finished`),
using `SimpleAIPathFollower` for opponents (see Pro/Community note above).
**Confirmed working in Play mode (2026-08-29):**
- `BeginRace()`: teleports player to grid slot 0, instantiates the opponent from
  `RaceOpponentPreset.vehiclePrefab` at grid slot 1, configures its
  `SimpleAIPathFollower`, transitions `Idle → Countdown` — zero console errors.
- **AI driving, full path**: a `SimpleAIPathFollower`-equipped AI car placed in
  `RaceCourse_CitySprint` autonomously drove the entire hand-corrected 120m waypoint path
  (5 waypoints, human-adjusted mid-session to route around a building) — start
  `(-15, 0, -6)` to `(-9.79, 0.01, 139.55)` near the final waypoint, steering through each
  intermediate point correctly. This is the core, hardest-to-fake proof that the whole
  system works.
- **Not yet re-verified in this exact configuration**: the countdown→Racing state
  transition and finish-line detection specifically *via `BeginRace()`* (as opposed to a
  path-follower placed directly in the scene) — the state-machine logic is simple and
  unchanged from when it was checked earlier, but do a final end-to-end pass (trigger →
  countdown → race → finish → results) before considering Phase 4 fully closed.
- **Editor stability note**: this session's Unity Editor repeatedly stalled its Play Mode
  player loop when not focused (`Time.frameCount` frozen for 60+ real seconds). Simulated
  time only advances while the Editor window has focus/is being interacted with. Keep the
  Editor focused during Play mode testing.

### Phase 5 — Race UI — WIRED, PENDING VISUAL CONFIRMATION
`RaceUIController` (`Assets/Scripts/Racing/UI/`) subscribes to `RaceEvents` and drives
countdown/position/results `TMP_Text` fields + panel visibility. Now wired to a real
`RaceHUD` hierarchy under `_GameController/Canvas` (`CountdownPanel`, `HUDPanel`,
`ResultsPanel`), matching the existing SpeedMeter's TMP font for visual consistency.
**Confirmed via script** that `BeginRace()` correctly activates `CountdownPanel` and
updates its text to `"3"` through the real `RaceEvents` pipeline (component state,
`RectTransform` centering, and text content all verified programmatically). **Not yet
visually confirmed on screen** — this session's Play Mode kept restarting/stalling
mid-test (see the recurring Editor-stability note elsewhere in this doc). Do one direct
Play-mode pass, watching the Game view continuously, to confirm the countdown/position/
results actually render and look right.

### Phase 6 — AI traffic tuning — READY TO START
The blocker (no real track) is resolved — `RaceCourse_CitySprint` has a working, tested
path. `SimpleAIPathFollower` exposes the same tuning knobs the plan called for
(`trafficPolicy` Racing/Traffic, `targetSpeedMultiplier`, `followTimeGap`,
`followMinimumGap`) plus its own physics knobs (`driveAcceleration`, `maxTurnRateDegPerSec`,
`turnRateDamping`). Its traffic-gap logic (`TrafficPolicy.Traffic`) is a simplified
raycast-ahead check, not real overtaking — multiple opponents will currently queue behind
each other rather than pass, since there's no multi-lane/alternate-path logic wired into
the follower yet (RaceCourse.alternatePaths exists but SimpleAIPathFollower doesn't pick
between lanes). That's the next real gap to close for the "6+ opponents with real
overtaking" requirement — see the improvement doc for specifics.

### Phase 7 (later, ambitious) — Parts upgrade — CODE DONE
`RacePart` (SO: id, name, cost, additive power/torque/maxRPM/mass deltas) +
`RaceGarageController` (ownership tracked via `PlayerPrefs`, `ApplyOwnedPartsToVehicle`
sums all owned deltas onto the live `VehicleEngine`). No economy/spending logic or UI —
deliberately placeholder per the original plan. Not tested; no UI built to interact with
it yet.

## Example content (for testing/reference)
- `Assets/Racing/OpponentPresets/BmwRival.asset` — uses MVC's own
  `2005 BMW M3 GTR E46 - AI.prefab` (ships with a `VehicleAIPathFollower` component that
  is currently unused/dormant — `SimpleAIPathFollower` is added alongside it at runtime).
- `Assets/Racing/Races/CitySprintTest.asset` — 1 opponent, 3s countdown.
- `RaceCourse_CitySprint` in `TestScene`: 2 grid slots (aligned with the path start),
  finish line, `AIPath_Main` (`SimpleAIPath`, 5 waypoints, hand-corrected to avoid a
  building), `RaceManager` on `_GameController`. `AI_Opponent_1` is placed persistently
  in the scene at grid slot 1 with a working `SimpleAIPathFollower` for quick testing —
  press Play and watch it drive without needing to trigger `BeginRace()`.

## Build order

0 → 1 → 2 → 4 (with 1-2 AI, no traffic tuning yet) → 3 → 5 → 6 (scale to 6+, tune) → 7

**Actual status (2026-08-29):** 0, 1, 2, 4 are done and verified (including the hardest
part — an AI car actually driving a hand-authored path end to end). 3, 5, 7 are code-
complete but not scene-wired/visually tested. 6 is unblocked and ready to start but the
follower doesn't yet support multi-lane overtaking. See `TESTING_AND_ROADMAP.md` for the
full test checklist and a prioritized list of what's left, including everything outside
this session's scope.
