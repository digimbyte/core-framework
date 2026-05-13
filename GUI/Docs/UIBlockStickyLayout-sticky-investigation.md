# UIBlock sticky layout: investigation notes

This document records work around **`UIBlockStickyLayout`**, debug logging, and “snapping / teleporting” reports. It is meant as a handoff for anyone touching Nova sticky layout or similar diagnostics—not as user-facing product docs.

---

## What we were trying to do

1. **Symptom:** UI felt like it **snapped or teleported** when layout targets changed, despite sticky smoothing (`Damped` / `Timed`) and `MaxSpeed`.
2. **Ask:** Richer **debug output**: target vs current, tangents, per-frame deltas, velocity, and later **target spec** (position + size) with **before / after** on start, stop, and target changes.
3. **Ongoing concern:** Logs showed `TELEPORT_CANDIDATE` when comparing motion to `MaxSpeed`, and a belief that the **layout buffer was not driving** the visible transform.

---

## What we changed in code (summary)

### `UIBlockStickyLayout.cs` (public component)

- Extended debug capture: **ideal position**, **buffer** (`TransformLocalPositions`), **`transform.localPosition`**, **sticky velocity**, **timed state**, plus **CalculatedSize** (via layout store) vs **`LayoutSize`** on the block.
- **Events** in the log stream: `CAPTURE_START`, `TARGET_SPEC_CHANGE` (before, tangents, after), `CAPTURE_STOP`.
- **Heuristics** (flags) to spot suspicious motion, including:
  - step larger than a `MaxSpeed`-based cap,
  - large actual motion while the **goal** barely moved.
- **Compiler fix (CS8173):** `LayoutDataStore.Instance.AccessCalc(...)` returns `Nova.Internal.Layouts.CalculatedLayout`, not `Nova.CalculatedLayout`. A `ref` local to the wrong type failed; the fix was to **read fields without a mismatched `ref` local** (e.g. read `.Size.Value` inline).

### Debug logic correction (important)

Early versions compared `|dACTUAL|` to `maxSpeed * Time.deltaTime` **on the same `LateUpdate` frame** as the sample. That turned out to be **the wrong time basis** for the motion between two `LateUpdate` samples (see “What we learned” below). Later logic stores **`min(Time.deltaTime, 0.333)` at the end of each `LateUpdate`** and uses that on the **next** `LateUpdate` as **`stickyBracketDt`** when checking the MaxSpeed teleport heuristic, and logs both “this frame job dt” and “bracket dt” for clarity.

### Core layout / smoothing (reference only)

- **`TransformSync.Write`** (`TransformsWrite.cs`): reads `transform.localPosition`, applies sticky rules, writes **`transform.localPosition`** and **`TransformPositions`** (buffer) together for the sticky path.
- **`LayoutEngine.WriteToPhysicalTransforms`**: sets job `DeltaTime` from `Time.deltaTime`, clamped to `0.333f`.
- **`StickyLayoutSmoothing.SmoothDamp`**: implements damping and enforces **`maxSpeed * deltaTime`** on the position delta for damped follow.

No change to that core smoothing math was required to explain the main **false alarm** in the debug cap (wrong `deltaTime` frame).

---

## What we learned (framework behavior)

### 1. Nova runs after `LateUpdate`, not inside it

The Nova engine update is injected into Unity’s **PostLateUpdate**, **after** normal **`LateUpdate`** callbacks, and is positioned **before** `PlayerUpdateCanvases` (see `EngineManager` comments and insertion logic).

**Implication for any `LateUpdate` debug that reads `transform.localPosition`:**

- On frame **N**, your `LateUpdate` sees the pose **before** Nova’s sticky write for frame **N**.
- Nova then runs and applies **one** sticky step using **`Time.deltaTime` for frame N**.
- On frame **N+1**, your `LateUpdate` sees the pose **after** that write (unless something else moves the transform later in the frame).

So the vector **`actual(N+1) - actual(N)`** is **one** sticky integration step, and its time step is **frame N’s** `Time.deltaTime` (capped the same way as `WriteToPhysicalTransforms`), **not** frame N+1’s `Time.deltaTime`.

Misusing **current frame** `Time.deltaTime` when judging that delta makes **`MaxSpeed * dt` look too small** and produces **bogus “exceeds cap”** conclusions even when the job behaved correctly.

### 2. Buffer vs display position

In the sticky path, the job writes **both** the internal **`TransformLocalPositions`** entry and **`transform.localPosition`**. In captures we saw **buffer and transform aligned** when read after the same pipeline stage. Reported “buffer not applied” was not supported by those readings; the confusion came from **when** in the frame we sampled and **which `dt`** we used in the heuristic.

### 3. Position vs size

Sticky smoothing in this pipeline is about **position** toward **`StickyLayoutIdealPositions`**. **Size** (`CalculatedSize` / `LayoutSize`) can still **jump in one frame** with large layout changes. That can **look** like a teleport even when **position** moves smoothly under `MaxSpeed`.

### 3b. Timed mode: `SegmentProgress == 1` vs “still wrong on screen”

`TransformsWrite` Timed path advances **`SegmentProgress`** toward **1** over **`TimedDuration`**, lerping from **`SegmentOriginDisplay`** to the current **`localIdeal`** (layout ideal in the same space as `transform.localPosition`). When progress crosses completion, the job forces **`next = localIdeal`** and sets **`SegmentProgress = 1`** (see `TransformsWrite.cs`).

So a capture like:

- **`TIMED SegmentProgress=1.0000`**
- **`err GOAL-ACTUAL: (0,0,0)`** and **buffer** (`TransformLocalPositions`) matches **`transform.localPosition`**
- **`TARGET SPEC`** equals **`ACTUAL DISPLAY`**

means **sticky layout has finished its job**: the block is exactly where **`StickyLayoutIdealPositions`** says it should be for that frame. It does **not** by itself prove that your **game/feature “end state”** (e.g. a NEWS flow) fired, or that the **design-intent** screen position is correct—only that **Nova’s layout target and the transform match**.

If something still looks “stuck in limbo” for a long time:

1. **Sparse debug** (`debugLogEveryFrame=false`) only prints when values change past epsilon, so a **single** sample at toggle-off does **not** show 15 minutes of history—only **that moment**.
2. **Wrong ideal upstream**: If **`StickyLayoutIdealPositions`** is numerically stable but **not** where you expect, the bug is in **layout / hierarchy / constraints**, not in “failing to reach” the ideal (you already reached the number the engine published).
3. **Different spaces**: Parent motion, canvas scaler, safe area, and world-space expectations can make **correct local** layout look “wrong” in **screen** space.
4. **Rename confusion**: “CAPTURE START **NEWS**” in logs is the **block label** in your scene, not a guarantee that a **NEWS** state machine entered an “end” state.

### 4. “Goal quiet but actual moved” flags

A large **`dACTUAL`** with **`dGOAL ≈ 0`** can happen when the **target stopped** but the block is **still catching up** from an earlier jump. That pattern alone does not prove an external script moved the transform; it needs interpretation together with sticky state and frame timing.

**Important nuance:** During **normal `Damped` follow**, the layout ideal often stays **fixed for many frames** while the transform **keeps moving** toward it. In that situation **`dGOAL` is legitimately ~0** and **`dACTUAL` is non-zero every frame** until the error closes. The debug flag `TELEPORT_CANDIDATE large |dACTUAL| while GOAL barely moved` therefore **fires routinely during ordinary smoothing**, not only on teleports. Treat it as a weak signal unless paired with something inconsistent (e.g. motion **away** from the goal, or a **`step exceeds MaxSpeed cap for stickyBracketDt`** line in the same capture).

### 5. Namespace / layout types

There are **two** `CalculatedLayout` concepts in play (public vs internal). Debug or tools must use the **actual return type** of `AccessCalc` or avoid `ref` locals that force the wrong struct type.

### 6. Auto-layout / stack: ideal slot vs smoothed render (why logs can “lie” to product intent)

This is the mental model behind **“expected location vs rendered location should be detached”** in a **stack / auto-layout** setup.

**Where the numbers come from**

- `LayoutCore.ConvertToTransforms` computes the **current** layout-local position from calculated layout (stack rules, alignment, padding, etc.) and writes the **same** `localPosition` to both:
  - `TransformLocalPositions` (the shared layout buffer), and
  - `StickyLayoutIdealPositions` (the sticky target).
- See `LayoutsConvertToTransforms.cs`: both assignments happen together when the layout pass converts dirty nodes.

**Order within a layout frame** (`LayoutEngine.PerformLayoutUpdate`)

1. `ConvertToTransforms` → buffer + ideal = **instantaneous slot** from this frame’s layout solve.
2. Hierarchy bounds, spatial partition, **content bounds** → these jobs read `TransformLocalPositions` **before** the sticky transform write.
3. `WriteToPhysicalTransforms` → `TransformSync.Write` moves **`transform.localPosition`** toward the ideal and writes the **smoothed** result back into `TransformLocalPositions` for sticky elements.

So in one frame, **internal geometry used for bounds/partitioning** is tied to the **layout-computed ideal**, while **on-screen local position** is allowed to **lag** behind that ideal until sticky catches up. The public `UIBlockStickyLayout` remark already notes that **hit regions can lead visuals** during catch-up; the same split applies to how you should read debug: **layout truth** (stacking / bounds) and **pixel truth** (smoothed transform) are not the same thing mid-tween.

**What sticky does *not* do**

- Sticky **does not** keep a separate, stable “semantic end position” across stack reflows. It only smooths toward **`StickyLayoutIdealPositions`**, which is **whatever layout just published**.
- When auto-layout **reflows** (sibling sizes, spacing, content changes), the **ideal can jump** in discrete steps. The smoother then chases a **moving target**. You get a **tween between successive ideals**, not a single continuous curve from “old stack state” to “final stack state” in world space.
- When the ideal **stops changing**, sticky can settle with **zero error** in logs—but that only means the **transform matched the last published ideal**, not that the **ideal’s path** matched a designer’s expectation of how the stack should have moved over time.

**Implication for your expectation (“free float to expected location”)**

- Today, **“expected” in code = last layout solve**, not a higher-level animated layout goal.
- True **free float to a stable expected slot** in a live stack usually needs **extra product/engine work**, for example: smoothing or predicting layout targets, driving a presenter layer off smoothed poses, running a second pass after sticky, or constraining when/how parents reflow—not only tuning `MaxSpeed` / `TimedDuration` on `UIBlockStickyLayout`.

### 7. Ways to get “free float” across reflows (design sketch)

This section answers: **if sticky is doing its job but the ideal jumps, what do we actually build?** None of this is implemented as a single toggle today; pick based on who owns “truth” for bounds and hits.

**Shared problem:** `ConvertToTransforms` writes the **instant** stack slot into `StickyLayoutIdealPositions` (see §6). `UIBlockStickyLayout` only eases **transform → that number**. To tween **meaningfully** when siblings or spacers change, something must either **smooth the ideal**, **smooth a separate visual**, or **slow down when layout is allowed to publish a new ideal**.

| Approach | Idea | Where it lives | Pros | Cons |
|----------|------|----------------|------|------|
| **A. Smoothed ideal buffer (engine)** | Keep the raw layout slot in one place; maintain a **second buffer** (per sticky index) that moves toward the raw slot each frame (damp, max speed, or timed). `TransformsWrite` reads the **smoothed ideal**, not the raw ideal. Bounds jobs that must match **stack truth** keep reading the **instant** buffer from layout. | New or extended job between `ConvertToTransforms` and `WriteToPhysicalTransforms`; data beside `StickyLayoutIdealPositions` in `LayoutDataStore`. | One place fixes all sticky elements; matches “global component” wording; one integration step still drives the transform. | You must decide **which systems see which buffer** (§6: bounds already see ideal before sticky—this may widen or narrow the gap on purpose). Large jumps may need **snap thresholds** so menus don’t lag a second behind data. |
| **B. Visual child / presenter** | Layout drives an **invisible** or **non-sticky** node at the true slot; a child `RectTransform` / `UIBlock` **only for drawing** tweens in local space toward the parent’s pose (or toward a cached “last good” pose). | Feature or small helper component; no Nova fork if you can express it with an extra node. | Full control over what the player sees; layout math stays honest for siblings. | Hit targets and focus need a clear rule (follow visual vs follow layout). Extra hierarchy and bookkeeping. |
| **C. Defer or stage reflow** | While a panel is “animating layout,” **do not** apply the full sibling reflow (or apply it to a hidden tree), then **commit** when the tween finishes. | Game/feature code that controls dirty flags, visibility, or duplicate layout trees. | Strongest guarantee of a single continuous motion in **screen** space. | Easy to get wrong (stale layout, double layout cost, race with content streaming). |
| **D. Animate size and spacing explicitly** | Many “teleports” are **size** or **padding** steps, not position smoothing. Drive **CalculatedLength** / style props through your own tweens, or accept instant size and only ease position. | Feature code or custom layout props. | Addresses the yellow/pink spacer case if the pop is mostly **height** changes. | Does not replace A/B if the issue is **pure stack re-order** of position. |
| **E. Higher-level “semantic target”** | Your flow computes an **expected** end pose (or delta) and blends **that** over time; layout still runs, but you **override** local position after Nova or blend toward a stored goal. | Rare; last resort; fights the engine order in §6. | Can match exact design curves. | Fragile (execution order, double writes, fights sticky). Prefer A or B first. |

**Practical recommendation for the “cavity behind the red panel” symptom**

1. Confirm whether the hole is **layout bounds** (siblings laid out as if the panel already moved) vs **only pixels** (transform lag). §6 explains that **bounds can lead** the smoothed transform—that is exactly an empty gap if siblings use the **instant** slot.
2. If the product wants **siblings and the panel to move together visually**, you need either **B** (visual-only child) or **A** with a clear rule: either **also** feed smoothed positions into whatever positions siblings use for stacking (hard; changes layout meaning), or **accept** that the stack solves in ideal space and **mask** the gap (e.g. background on parent, clip, or don’t reflow visible siblings until the tween completes—**C**).
3. If the jump is mostly **spacer height**, add **D** so the **ideal** itself changes smoothly, not only the sticky chase.

**If you implement A (smoothed ideal buffer)**

- Store **raw ideal** from `ConvertToTransforms` (today the same list is both buffer and ideal for that pass; you may split into `StickyLayoutRawIdeal` vs `StickyLayoutSmoothedIdeal` for sticky-eligible indices only).
- Run a cheap parallel pass: `smoothed += SmoothDelta(smoothed, raw, dt)` with the same `MaxSpeed` / smooth time policy you already trust on the transform.
- Point `TransformsWrite` at **smoothed** ideal; keep **instant** positions in `TransformPositions` for jobs that must match layout solve **before** sticky (audit `LayoutEngine.PerformLayoutUpdate` order when you change this—some code may need the post-smooth value for consistency).

---

## Process lessons (what went wrong in the investigation)

1. **Assumed** a simple invariant (“every step ≤ `MaxSpeed * dt`”) without pinning **`dt` to the same integration interval** as the measured **`Δposition`**. In this stack, that interval is tied to **Nova’s schedule relative to `LateUpdate`**, not “whatever `Time.deltaTime` is when I log.”
2. **Underweighted** how **large upstream layout deltas** (ideal position + size) dominate perceived pops, especially **instant size**.
3. **Debug flags** read like engine defects; without the timing note above, they **over-interpret** normal behavior.

---

## Files touched (for blame / review)

| Area | File |
|------|------|
| Sticky component + debug | `Scripts/Public/Components/UIBlockStickyLayout.cs` |
| Transform write + smoothing application | `Scripts/Internal/Layouts/TransformsWrite.cs` |
| Smoothing helpers | `Scripts/Internal/Layouts/StickyLayoutRuntime.cs` |
| Job `DeltaTime` | `Scripts/Internal/Layouts/LayoutEngine.cs` (`WriteToPhysicalTransforms`) |
| Engine phase / player loop | `Scripts/Internal/EngineManager.cs` |

---

## Suggested follow-ups (if symptoms remain)

1. Treat **size** explicitly in product design (animate size, staged layout, or accept instant bounds) if the pop is visual, not positional.
2. Any code that **reads world/local pose for gameplay or UI hit tests** in **`LateUpdate`** may be **one Nova pass behind** the rendered pose for that frame; consider reading after Nova or using layout/bounds data from the store if that matters.
3. Keep debug **stickyBracketDt** (or equivalent) in any future telemetry so MaxSpeed checks stay comparable to real integration steps.
4. If the issue is **stack reflow** vs **pixel tween**, treat **§6** as the root read: logs that show `err=0` are about **catching the current ideal**, not about **continuity of the ideal** across reflows.
5. For **product-level** smooth motion when spacers or siblings change, pick an approach from **§7** (smoothed ideal buffer, presenter child, deferred reflow, or explicit size animation)—`UIBlockStickyLayout` alone cannot tween a stable “expected” slot if the engine replaces that slot every dirty pass.

---

## Post-fix log check (same scenario, `stickyBracketDt` in logs)

After wiring **`stickyBracketDt`** = prior frame’s `min(Time.deltaTime, 0.333)` into the MaxSpeed check (`lenDA > maxSpeed * stickyBracketDt * 1.01f`), a capture matching the earlier “NEWS opens / layout jumps” pattern behaved as follows:

| Sample | `stickyBracketDt` | `\|dACTUAL\|` | `maxStepCap(maxSpeed*stickyBracketDt)` | MaxSpeed teleport flag |
|--------|-------------------|----------------|----------------------------------------|-------------------------|
| #2 | 0.02000 | 0.20000 | 0.20000 | No (at cap) |
| #3 | 0.02000 | 0.20000 | 0.20000 | No |
| #4 | 0.03510 | 0.35103 | 0.35102 | No |
| #5 | 0.01063 | 0.10628 | 0.10629 | No |

Previously, comparing **`|dACTUAL|`** to **`maxSpeed * dtLate`** (or this frame’s `jobDt` when it didn’t match the integration step) produced **`step exceeds MaxSpeed cap`** on frames like #4 even though the step matched **`maxSpeed *`** the **prior** frame’s `jobDt`. The corrected bracket removes that **false positive**.

The **`GOAL barely moved`** flag can still appear on samples #3–#5 because **`dGOAL ≈ 0`** while the block is **still translating toward a stable ideal**—see §4.

---

## Document history

- **2026-04-24:** Written after sticky debug work, `TELEPORT_CANDIDATE` timing correction, and CS8173 fix on `AccessCalc` usage.
- **2026-04-24:** Added post-fix log validation table and clarified that the “goal quiet” flag often reflects **normal** chase-to-target, not teleport.
- **2026-04-24:** Added §3b on **Timed** mode: `SegmentProgress=1` + zero error means layout target and transform agree; long “limbo” usually needs **upstream ideal**, **feature state**, or **space** debugging—not more smoothing.
- **2026-04-24:** Added §6 on **auto-layout / stack**: `ConvertToTransforms` vs sticky write order, **ideal vs smoothed buffer**, and why **chasing `StickyLayoutIdealPositions`** is not the same as tweening to a stable “expected” slot across reflows.
- **2026-04-24:** Added §7 **design sketch**: options (smoothed ideal buffer, presenter child, deferred reflow, size animation, semantic override) for **free float across reflows**, with a short decision table and notes on bounds vs pixels.
