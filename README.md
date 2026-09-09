# T's Most Wanted

An arcade street-racing game built in Unity on top of **BxB Studio's MVC (Multiversal
Vehicle Controller)** — Community tier. Free-roam an open city, drive into world
triggers to start races, level up through a 10-track Career mode, and buy/swap cars and
parts at the Garage and Shop.

This README documents the project **as it currently stands**. Three older docs
(`PLANNING.md`, `TESTING_AND_ROADMAP.md`, `StepsToSetupRace.md`) capture the *first*
build session (a single test race, `TestScene`) and are now historical — most of what
they describe has since been superseded (renamed scene, 10 races instead of 1, full
progression system, redesigned UI). They're still useful for the deep "why" behind the
custom AI system and the MVC Pro-vs-Community story; this file is the up-to-date map of
everything.

---

## 1. Tech stack

| | |
|---|---|
| Engine | Unity **6000.5.5f1** (Unity 6), URP (Universal Render Pipeline) |
| Vehicle physics | **MVC (Multiversal Vehicle Controller) — Community edition** (`Packages/dev.bxbstudio.mvc.community`) |
| Input | Unity's new **Input System** (`Keyboard.current`, etc.) |
| UI text | **TextMeshPro**, custom **Archivo** font (Regular/SemiBold/ExtraBold), generated as TMP Font Assets |
| Unused-but-present | **Vehicle Physics Pro** — its own vehicle/demo content is unused, but its `Sample Assets/Art/Models/Pixelactive City` folder **is** the entire drivable city map in `Drive_Scene`. Don't delete that subfolder. |

### The MVC Pro-vs-Community landmine (read this before touching AI/vehicle code)

MVC's real AI system (`MVC.AI.VehicleAIPath` / `VehicleAIPathFollower`) and direct
throttle/brake/steering control (`Vehicle.Inputs`) are **Pro-only**, gated at the
compiled-assembly level — not just hidden in the inspector. This project only has
Community. Two custom systems exist specifically to route around that:

- **`Racing.AI.SimpleAIPath` / `SimpleAIPathFollower`** (`Assets/Scripts/Racing/AI/`) —
  a from-scratch waypoint path + follower that drives AI cars via direct `Rigidbody`
  force/torque, not MVC's engine/wheel simulation. Field names deliberately mirror MVC's
  Pro API (`path`, `followDirection`, `targetSpeedMultiplier`, `trafficPolicy`,
  `followTimeGap`, `followMinimumGap`) so swapping back to real MVC Pro AI later is a
  scoped, mechanical change (see `PLANNING.md`'s Phase 0 note for the exact 3 files).
- **`Vehicle.Engine` is not per-instance.** Every clone of a given vehicle prefab shares
  the *same* `VehicleEngine` object. Writing to `power`/`torque`/etc. at runtime (e.g. to
  apply upgrade-part stat deltas) mutates the **source prefab asset itself**, stacking
  without bound across every scene load until MVC's own curve-mismatch validator
  disables the vehicle. `RaceGarage.ApplyOwnedPartsToVehicle()` and the opponent-preset
  engine-override path in `RaceManager.ApplyEnginePreset()` are both deliberately
  **no-ops** because of this — don't resurrect them without solving the per-instance
  problem first (e.g. cloning the engine asset per vehicle instance).

---

## 2. Scenes

| Scene | Purpose |
|---|---|
| `MainMenu_Scene` | Boot screen — title "T'S MOST WANTED", buttons: Career / Quick Race / Multiplayer / Options / Exit. Build index 0. |
| `Drive_Scene` | The open city (from Vehicle Physics Pro's Pixelactive City assets), free-roam driving, all race/garage/shop world triggers, the HUD. Build index 1. |
| `Garage_Scene` | Swap which car you drive (`CarGarageController`). Build index 2. |
| `Shop_Scene` | Buy upgrade parts (`RaceGarageController`/`ShopSceneController`). Build index 3. |

All scene transitions go through `SceneFlow` (`Assets/Scripts/Racing/SceneFlow.cs`),
which wraps every load in `LoadingScreen` so nothing calls `SceneManager` directly.

---

## 3. Game modes

Set once on the Main Menu, read everywhere else via the static `GameMode` class
(`Assets/Scripts/Racing/GameMode.cs`, session-only — not saved):

| Mode | Level gate | Rewards | Notes |
|---|---|---|---|
| **Career** | Yes — `PlayerProgress.Level` vs each race's `requiredLevel` | Yes — XP + coins, first-clear tracking | The "real" progression mode |
| **Quick Race** | No — every race unlocked regardless of level | **No** — `RaceManager.GrantRewardIfEarned()` early-returns | Same map, same `RaceDefinition` assets as Career, just no stakes. Shows `RaceSelectUI`, a race-picker panel, since the world-trigger level gate is bypassed |
| **Multiplayer** | No (same as Quick Race) | No (same as Quick Race) | **Not actually implemented** — behaves identically to Quick Race. Placeholder for future networking |

`GameMode.BypassesLevelGate` is the single flag both `RaceTrigger` and `RaceSelectUI`
check. Defaults to `Career` so entering Play mode directly on `Drive_Scene` (skipping the
menu, e.g. while testing in the Editor) behaves like normal career progression.

---

## 4. Career progression

**Leveling**: formula-based via `LevelCurve` (a ScriptableObject,
`Assets/Racing/LevelCurve.asset`):

```
xpToNextLevel(level) = baseXP * growthRate^(level - 1)
```

Currently tuned **flat**: `baseXP = 100`, `growthRate = 1.0` → every level requires
exactly 100 XP, and a first-clear race reward is exactly 100 XP → **winning any one race
= exactly one level**. Levels 1 through 10 map onto the 10-race roster below, one race
unlocking per level.

**Rewards** (`RaceDefinition.GetReward(firstClear)`): every race currently pays
`baseXPReward = 100`, `baseCoinReward = 1000` on a genuine first clear, and 50% of that
(`repeatRewardMultiplier = 0.5`) on every replay after.

**Persistence** — all via `PlayerPrefs`, keys prefixed `RaceGarage.*`:

| Key | Written by | Meaning |
|---|---|---|
| `RaceGarage.XP` | `PlayerProgress` | Total accumulated XP (level is *derived* from this via `LevelCurve.LevelForXP`, never stored directly — re-tuning the curve later re-derives everyone's level without a migration) |
| `RaceGarage.CompletedRaces` | `PlayerProgress` | `;`-joined set of `RaceDefinition.raceId`s ever cleared — drives first-clear vs. repeat reward, and the race-info billboard's completed/not-completed color |
| `RaceGarage.Currency` | `PlayerCurrency` | Coin balance (starts at 1000 if no save exists) |
| `RaceGarage.OwnedParts` | `RaceGarageController` | `;`-joined set of owned `RacePart.partId`s |
| `RaceGarage.SelectedCarIndex` | `CarSelection` | Which roster car is currently driven, shared between `Garage_Scene` and `Drive_Scene` |

**Debug reset** — `Ctrl+Shift+R` in Play mode (`DebugProgressReset.cs`, compiled out of
release builds via `#if UNITY_EDITOR || DEVELOPMENT_BUILD`) wipes XP/level, coins, *and*
completed-race tracking back to a fresh start, so the full level 1→10 chain can be
replayed without hand-editing `PlayerPrefs`. Deliberately **not** named `Reset()` on the
`MonoBehaviour` — that's a reserved Unity Editor message auto-invoked on `AddComponent`/
the Inspector's "Reset" option, which would otherwise wipe save data the instant the
script is attached.

---

## 5. The 10-race roster

All 10 live as `RaceCourse` GameObjects in `Drive_Scene`, each with its own
`RaceDefinition` asset in `Assets/Racing/Races/`. 5 sprints (point-to-point, 1 lap) + 5
circuits (looped, 2–3 laps), one race unlocking per Career level:

| Lv | Race | Type | Laps | Opponents | Reward (first clear) |
|---|---|---|---|---|---|
| 1 | **South Sprint** | Sprint | 1 | 4 | 100 XP · 1000 coins |
| 2 | **Harbor Sprint** | Sprint | 1 | 4 | 100 XP · 1000 coins |
| 3 | **Uptown Sprint** | Sprint | 1 | 4 | 100 XP · 1000 coins |
| 4 | **Old Town Sprint** | Sprint | 1 | 4 | 100 XP · 1000 coins |
| 5 | **Riverside Sprint** | Sprint | 1 | 4 | 100 XP · 1000 coins |
| 6 | **Eastside Circuit** | Circuit | 3 | 4 | 100 XP · 1000 coins |
| 7 | **Westside Circuit** | Circuit | 3 | 4 | 100 XP · 1000 coins |
| 8 | **North Loop** | Circuit | 2 | 4 | 100 XP · 1000 coins |
| 9 | **South Loop** | Circuit | 2 | 4 | 100 XP · 1000 coins |
| 10 | **Grand Circuit** | Circuit | 3 | 5 | 100 XP · 1000 coins |

Plus 3 pre-existing prototype/test races (`CitySprintTest`/"City Sprint",
`CitySprint2`/"City Circuit", `CityLapTest1`) left untouched, all `requiredLevel = 1`.
`CitySprint2` and `CityLapTest1` are **orphaned data** — the `RaceDefinition` assets
exist but have no `RaceCourse` GameObject placed in the scene, so they're not actually
playable; only `CitySprintTest` (`RaceCourse_CitySprint`) has scene geometry.

**How the 10 were laid out**: the city is a uniform, repeating road grid (verified via
top-down orthographic screenshots across all 4 quadrants). The 4 new sprints beyond
South Sprint were generated by applying rigid transforms (90°/270° rotation about the
map center, plus translations by clean multiples of the grid pitch) to the original,
already-tested `RaceCourse_CitySprint` path — guaranteeing every new corridor lands on
real road without hand-placing coordinates. The 5 circuits are rectangular loops built
from scratch (`RaceCourse.mainPath.loopedPath = true`), each corner's grid slots/finish
line auto-computed from the course's own waypoints via a shared `RacingTheme`-style
builder script, not placed by hand.

---

## 6. Core architecture

### Race data (ScriptableObjects — portable, no scene refs)
- **`RaceDefinition`** — name, description, `requiredLevel`, `baseXPReward`,
  `baseCoinReward`, `repeatRewardMultiplier`, `opponentCount`, `opponentPresets[]`,
  `countdownSeconds`, `laps`. `raceId` is the stable save-data key (falls back to the
  asset name if blank) — renaming the `.asset` file in the Project window won't lose a
  player's completion record.
- **`RaceOpponentPreset`** — vehicle prefab, optional engine-stat override (currently
  inert — see the Engine landmine above), AI tuning (`trafficPolicy`,
  `targetSpeedMultiplier`, `followTimeGap`, `followMinimumGap`).
- **`RacePart`** — shop upgrade: id, cost, additive engine stat deltas. Applying them is
  currently a no-op (same Engine landmine).
- **`LevelCurve`** — the XP/level formula (§4).

### Race scene rig (`RaceCourse`, a `MonoBehaviour` — can't live in a portable SO)
`gridSlots[]` (slot 0 = player), `finishLine` + `finishLineRadius`, `mainPath` +
`alternatePaths[]` (both `SimpleAIPath`), `wrongWayDotThreshold` (declared, never
actually read/used by `RaceManager` — a scoped-but-unbuilt feature).

### Race flow (`RaceManager`, singleton on `_GameController`)
State machine: `Idle → Countdown → Racing → Finished`. Key behavior:
- **Object pooling for AI opponents** — `RentOpponent()`/`ReturnOpponent()` keep
  deactivated-but-alive instances keyed by source prefab, reused across races instead of
  destroy+instantiate (a full car with mesh/materials/physics is expensive to rebuild).
- **`TeleportVehicle()`** — moves *both* `Transform` and `Rigidbody.position/rotation`,
  then calls `Physics.SyncTransforms()`. Setting `transform.position` alone is not
  enough for a `Rigidbody` with interpolation enabled (the default for a smooth-looking
  vehicle) — Unity renders interpolated bodies from the physics engine's own buffered
  position, so the visible car can keep showing its *old* spot even though
  `transform.position` already reads correctly. This bit hard once already (car
  "stuck" at the trigger instead of the grid slot) — always use this helper, never a
  bare `transform.SetPositionAndRotation` on a race participant.
- **Countdown freeze**: every car, player included, is forced kinematic + snapped back
  to its exact grid pose *every frame* during Countdown (belt-and-suspenders — even if
  something else nudges the transform mid-frame, the next frame corrects it).
- **Lap counting** (`CheckFinishes()`): a lap only counts if the participant has driven
  at least 30% of the course's total length since they last left the finish zone
  (`distanceSinceZoneExit`, accumulated from real position deltas every frame). This
  exists because on a circuit the grid start sits *inside* `finishLineRadius` (a
  handful of units away, well under the 8-unit default) — a stationary or physics-
  jittering car can flicker across that boundary on its own, and both a naive
  "zone occupancy" check and an earlier "closest-waypoint-index" attempt both let that
  register as a free lap. Distance-driven is what actually holds up.
- **Position ordering**: finished-first, then `lapsCompleted`, then `progressIndex`
  (nearest-waypoint index along `mainPath`), then finish time.

### Custom AI (`Assets/Scripts/Racing/AI/`)
- **`SimpleAIPath`** — ordered `Transform[] waypoints` + `loopedPath` bool. Mirrors
  `VehicleAIPath`'s API surface (`TotalLength`, `IsValid`, `ClosestPointIndex`,
  `NextIndex`) so it's a drop-in-shaped replacement if this project ever gets MVC Pro.
- **`SimpleAIPathFollower`** — drives via direct `Rigidbody.AddForce` (propulsion) +
  damped yaw-rate (steering), with a forward sphere-cast for traffic (`Racing` policy:
  overtake if there's room; `Traffic` policy: just brake) and a stuck/flip-recovery
  backstop (rights and repositions a car that's been stuck or upside-down for >2.5s).

### World triggers (hover-then-confirm popup pattern)
`WorldPromptTrigger` (abstract base, implements `IWorldPromptTrigger`) handles the
shared hover timer + `RacePromptUI` show/hide + "no interaction mid-race" gate
(`CanInteract()`). Three concrete triggers:
- **`RaceTrigger`** — auto-wires to its parent `RaceCourse` via
  `GetComponentInParent`. `isEnabled` (the base class's master on/off switch) is driven
  by `RefreshUnlockState()`: `unlocked (level gate, or bypassed by GameMode) AND NOT
  race-in-progress`. Also toggles the trigger's particle marker and
  `RaceInfoBillboard` visibility to match — a locked or mid-race trigger is invisible in
  the world, not just non-interactive.
- **`GarageTrigger`** / **`ShopTrigger`** — send the player to `Garage_Scene`/
  `Shop_Scene`. Always enabled (no level gate).

### World-space race info card (`RaceInfoBillboard`)
A small card floating ~4.5 units above every race trigger, billboarding to face the
camera every frame (yaw-only — no tilt from camera pitch). Shows race name, type
(`SPRINT` or `CIRCUIT · N LAPS`, derived from `RaceDefinition.laps`), and reward. Colors
itself green (`RacingTheme.Success`) once `PlayerProgress.HasCompleted()` that race,
red/ink otherwise — refreshed on level-up, race start/reset, and reward-granted events.

### UI (`Assets/Scripts/Racing/UI/`)
- **`RacingTheme`** — the shared "Modernist" design-system helper (ink `#201e1d` /
  off-white panel `#f3f2f2` / accent red `#ec3013` / success green `#2e7d32`, Archivo
  type, sharp 0-radius 2px-bordered panels). Every screen builder (HUD, Garage, Shop,
  trigger prompts, loading, main menu, race select, the world-space info card) goes
  through these helpers so the look only needs tuning in one place.
- **`RaceUIController`** — the in-race HUD (corner-anchored: speed/gear bottom-left,
  position top-left, level top-right, course/progress bottom-right above the minimap,
  countdown, results). The progress card shows meters-remaining for a sprint, or
  `LAP x/y` for a circuit (swapped via `RaceEvents.LapChanged`) — a circuit's "distance
  remaining" only ever reflected the *current* lap, which read as broken on a
  multi-lap course, so it's lap-counter-only there instead.
- **`MinimapController`** — elevated chase-cam minimap (not straight-down), rendered
  every 3rd frame at reduced resolution for performance, with a green route line drawn
  from the active course's `mainPath` waypoints to the finish.
- **`RacePromptUI`** — the shared hover-popup panel every `WorldPromptTrigger` shows.
- **`MainMenuController`**, **`RaceSelectUI`**, **`GarageSceneController`**,
  **`ShopSceneController`**, **`LoadingScreen`** — one controller per screen, same
  "build hierarchy once via an Editor-only `[ContextMenu]`, wire references at runtime"
  pattern throughout.

Every one of these screen builders has a **"Build UI (Editor Only)"** context-menu
method — right-click the component in the Inspector to regenerate its whole child
hierarchy from scratch (destructive to that hierarchy, not the rest of the scene). This
is how visual changes get applied without hand-editing prefab hierarchies.

### Persistence (`PlayerProgress`, `PlayerCurrency`, `CarSelection`, `RaceGarageController`)
All simple `PlayerPrefs` wrappers — see the key table in §4.

---

## 7. Performance work already done

- **Occlusion culling** baked for `Drive_Scene` (447 renderers under the city root
  marked `Occluder|OccludeeStatic`, all exceed the 3m size threshold).
- **Texture compression pass**: VFX and brake-part textures were uncompressed/oversized
  (up to 4096px, `isReadable=true`); capped and compressed, cutting ~332MB→~176MB of
  Texture2D memory.
- **Mobile URP quality tier fix**: WebGL (the shipping target) uses the `Mobile` quality
  tier, which is `Forward` rendering (CPU per-object light culling) rather than the PC
  tier's `Forward+` (GPU-clustered). With Drive_Scene's 23 realtime lights this made
  Mobile-tier *slower* than PC-tier despite being the "cheaper" preset — fixed by
  dropping `Mobile_RPAsset.m_AdditionalLightsPerObjectLimit` from 4 to 2.
  **Editor Play-mode testing runs the PC tier — it does not reflect actual WebGL runtime
  performance.** Switch the active quality level to Mobile before trusting a profiling
  session for the shipping target.
- **Minimap render cost**: the minimap camera was rendering full-scene every frame;
  fixed via frame-skip (render every 3rd frame) + reduced RT resolution + reduced far
  clip + disabled shadows/post-processing on that camera specifically.
- **AI opponent object pooling** (§6, `RaceManager`) — avoids repeated
  destroy/instantiate of full vehicle prefabs.

Combined effect measured this session: ~38fps → ~90fps in-race under the Mobile quality
tier (occlusion culling + light-count fix together).

---

## 8. Known gotchas / non-obvious things to remember

- **New `RaceCourse` built from scratch (not duplicated from an existing one) needs its
  `RaceTrigger.promptUI` wired manually.** Duplicating an existing course inherits the
  reference automatically; building fresh via script/menu does not — this exact miss
  silently broke all 5 circuits (no popup ever appeared, `isEnabled`/collider/position
  all checked out fine, the field was just null).
- **Never write to `Vehicle.Engine` fields at runtime for anything but the single
  player-driven instance** — it mutates the shared prefab asset (§1).
- **Unity magic method names**: don't name a component method `Reset()` unless you mean
  the Editor's auto-invoked-on-`AddComponent` callback. Bit once already
  (`DebugProgressReset`).
- **Rigidbody + interpolation**: always reposition a race participant through
  `RaceManager.TeleportVehicle()`, never a bare `transform.SetPositionAndRotation`.
- **Editor Play Mode in this environment stalls when the window isn't focused** —
  `Time.frameCount` freezes entirely. Not specific to any racing code; keep the window
  active during manual testing.
- **`FindFirstObjectByType`/`FindObjectsByType(FindObjectsSortMode)` are obsolete** in
  this Unity version (prefer `FindAnyObjectByType`/`FindObjectsByType<T>()`) — present
  throughout as warnings, not errors; harmless but worth cleaning up eventually.
- Vehicle Physics Pro's `Sample Assets/Art/Models/Pixelactive City/*` is the actual
  drivable map — everything else under `Assets/Vehicle Physics Pro/` (its own demo
  vehicles, scenes, SDK) is confirmed unused and safe to remove if reclaiming ~80MB
  matters, but hasn't been deleted yet (pending explicit approval for the bulk delete).

---

## 9. Adding a new race (current workflow)

1. **Data**: create a `RaceDefinition` asset (`Racing/Race Definition` menu, or
   `Racing → Race Definitions` window) — name, `requiredLevel`, `laps` (1 = sprint,
   2–3 = circuit), reward, `opponentPresets[]`.
2. **Geometry**:
   - *Point-to-point sprint*: easiest to duplicate an existing, already-tested
     `RaceCourse_*` GameObject and reposition every child (`GridSlot_*`, `FinishLine`,
     `AIPath_Main/Waypoint_*`, `TriggerArea`) by the same rigid transform, so the new
     corridor is guaranteed to land on real road (§5's technique) rather than hand-
     placing coordinates and hoping.
   - *Circuit*: place 4+ waypoints forming a loop, set `mainPath.loopedPath = true`,
     `finishLine` at (or very near) the first waypoint, grid slots computed from the
     first waypoint's position/rotation.
3. **Wire the `RaceCourse` component**: `definition`, `gridSlots[]`, `finishLine` +
   `finishLineRadius`, `mainPath`.
4. **`RaceTrigger`**: `BoxCollider` (`isTrigger = true`) + `RaceTrigger` component on a
   child (typically `TriggerArea`) — **remember `promptUI`** (§8's gotcha). Use
   `RaceCourseEditor`'s custom inspector buttons (Add/Auto-Arrange grid, Create Finish
   Line, Create AI Path Here) if authoring by hand in the Editor rather than by script.
5. **`RaceInfoBillboard`**: add the world-space card (see any existing trigger's
   `InfoBillboard` child for the exact hierarchy — `RacingTheme.CreateFramedPanel` +
   3 labels + a `RaceInfoBillboard` component, `Bind()`-ed to the frame image and the
   3 `TMP_Text`s).
6. Refresh assets, check the Console for compile errors, save the scene.

---

## 10. Build target

**WebGL** (Unity Play) is the shipping target. `PlayerSettings.defaultWebScreenWidth/
Height` = 1920×1080, `runInBackground = true`. Build order in
`EditorBuildSettings`: `MainMenu_Scene → Drive_Scene → Garage_Scene → Shop_Scene`.
Remember §7's Mobile-quality-tier note when profiling for this target specifically.
