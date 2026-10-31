# Collision and homing optimisation feasibility

Follow-up: [native collision investigation](NATIVE-COLLISION.md) now traces the
iterator/callback boundary using the retained local native address map. This
document records the earlier interface-only assessment and its limitations.

Assessment date: 31 October 2026. Prepared with OpenAI Codex. This is a local
interface/interop feasibility investigation, not recovered native source or a
proof of gameplay equivalence. No gameplay code or installed DLL was changed.

## Decision

**No-go for moving CollisionChecker.Process or TrackTarget.Process directly onto
workers. Conditional go for a narrowly scoped, comparison-only geometry experiment.**
There is no validated speedup or safe drop-in replacement ready to ship.

Runtime interception is established by the benchmark call counts. Replacing a
method with equivalent logic is a separate problem. Inspection establishes that
the exposed C# bodies forward into native code and that the relevant objects
contain mutable simulation state. It does not establish complete read/write sets,
native execution order, or the original numerical algorithms.

The next deliverable should be an exact dependency and arithmetic specification
for a small homing geometry operation, followed by a bounded observational capture
of its inputs/outputs. If that specification cannot be established, do not replace
the operation. Full collision replacement remains a larger research effort.

## Evidence and scope

Read the installed Assembly-CSharp metadata and wrapper IL with Mono.Cecil, without
executing game types. Examined CollisionChecker, CollisionGrid and its nested
CollisionState, Projectile, TrackTarget, TrackTargetModel, TravelStrait, Target
and relevant entity/behavior relationships. Inspected wrapper method bodies to
distinguish native forwarding from actual algorithms. Reviewed the recorded v0.6.0
timings. No game binaries, raw native disassembly or save data are published here.

Reproduce the interface inspection locally:

```powershell
pwsh -File scripts/Inspect-Feasibility.ps1
# Optional alternate installation:
pwsh -File scripts/Inspect-Feasibility.ps1 -GameDirectory 'D:\Games\BloonsTD6'
```

The script prints signatures, properties, native-forwarding flags and hashes. It
uses the loader's locally installed Mono.Cecil.dll and does not distribute it.
Keep any extensive inspection output local. This report summarizes observations
rather than providing a decompiled game implementation.

Inspected build identity (SHA-256):

| File | Hash |
| --- | --- |
| GameAssembly.dll | CAF18ADDE5B26658F65A8D040413624886FF90BB15C789A29DED4736F6E6689C |
| Generated Assembly-CSharp.dll | B420556C3BD1EEE3CDBDBD1B5CA0F53FE0E327619614D60E3ADDE9838FDA825B |

The local loader log reports MelonLoader 0.7.2 Open-Beta and Unity 6000.3.23f1;
its game version field is UNKNOWN. Do not substitute an assumed game version for
these hashes. A game/interop update requires reassessment.

## What the collision interface establishes

- CollisionChecker.Process accepts an Int32 and returns void. The parameter's
  semantics must be verified; a signature alone is not an execution specification.
- CollisionChecker exposes a projectile LockList and separate BloonGrid,
  MapBlockerGrid and PropCollidableGrid properties. A LockList name is not a
  documented thread-safety guarantee for native simulation access.
- CollisionGrid already contains buckets, dimensions, collision margins, ordered
  neighbor offsets, a source collection and reusable collisionStates with a
  currentCollisionState index. The game already has a spatial grid; adding one is
  not by itself a new optimisation. Its efficiency has not been established.
- GetInRange takes five Single parameters and returns CollisionState<T>.
  GetInLine takes position/direction-length vectors and thickness and returns an
  enumerable. Fill(Int32) is exposed. Exact boundary and ordering rules are unknown.
- CollisionState holds a grid reference, query coordinates/radii, bucket indices,
  iteration indices and Current; it exposes MoveNext, Initialize and Reset. It
  is a mutable iterator, not a detached immutable candidate array.
- Projectile offers CollideBloons overloads accepting CollisionState<Bloon> or
  an enumerable plus additional arguments. It also exposes pierce, collision
  history, ordered behavior passes, filters, target state, crit rolls and destruction
  callbacks. These are observable dependencies, not a complete mutation inventory.

**Implication:** do not share grid queries/iterators or live projectiles between
workers. Calling GetInRange or enumerating an original iterator for a shadow
comparison may itself advance/reuse state; do not assume such reads are harmless.
Observe what the original call consumes without performing a second enumeration.

A frame-wide candidate snapshot may become stale as earlier collisions destroy
objects or spawn children. Rechecking saved candidates afterward does not prove
completeness: new valid candidates may be missing. Any parallel candidate builder
needs proof of a stable epoch, complete invalidation rules, original ordering and
boundary behavior, plus a compatible native result-delivery mechanism. None is
currently established. Replacing the whole Process method would also require
preserving blocker/prop checks and behavior side effects.

## What the homing interface establishes

- TrackTarget.Process(Int32), GetNewTarget(), CalculateDirection(Int32),
  IsBloonValid(Bloon), IsTargetInSeekAngleRange(Vector3) and
  GetAngleBetweenDirectionAndTarget(Vector3) exist.
- TrackTarget contains lastElapsed, lastTargetSearchElapsed, searchDelayFrames,
  turnRatePerFrame, a filter, a BloonFilter delegate and a TravelStrait reference.
  Those fields indicate existing timing/search machinery; they do not prove its
  exact caching behavior. Blindly reducing search frequency violates our constraints.
- TrackTargetModel includes seek angle, distance, turn rate, rotation override,
  reacquisition flags, lifetime-as-distance and expire-on-contact settings.
- ProjectileBehavior references the live projectile. Target contains live bloon,
  tower and path references plus current/previous positions and a bloon identifier.
  Passing a Target wrapper/value to a worker is not sufficient data isolation.
- CalculateDirection is a virtual native call. Replacing only one wrapper cannot
  be assumed to cover derived behavior. Even the angle-returning method accesses
  an instance; it is not proven pure merely because it returns a float.

**Candidate boundary:** an independently implemented direction/angle calculation
over copied primitive inputs, leaving target acquisition, validation, movement
application and effects on the original thread. This is a proposed boundary, not
an existing verified pure API. The complete state read by the original must first
be established. Preserve operation order, float precision, turn clamping, angle
wrapping, zero-vector behavior and exact tick inputs. Do not replace formulas with
approximations or treat a numerical tolerance as proof of unchanged gameplay.

## Native implementation limitation

The inspected Process, GetNewTarget, CalculateDirection and angle wrappers call
il2cpp_runtime_invoke using native method pointers. CalculateDirection additionally
resolves a virtual method; the angle wrapper unboxes a Single result. Their C# IL
does not contain the original collision or homing arithmetic.

GameAssembly.dll is present locally, but its native algorithm has not been
reconstructed or audited in this investigation. This is an explicit stopping
boundary, not evidence that reverse engineering is impossible. Local native
call-graph/disassembly work or an authoritative algorithm specification is needed
to resolve arithmetic, side effects, call ordering and snapshot consistency. The
generated caller counts are not reliable substitutes for a dynamic call graph.
Do not use a guessed formula to fill this gap.

## Performance implications

For the latest detailed run (filename ending 88035409b7844f21899a229676c8e290),
active samples contained 82.669 seconds, 65.979 seconds inside Simulate, 20.902
seconds inside collision processing and 7.644 seconds inside tracking. Movement
was called about 5.69 million times and tracking about 5.03 million times. This
setup was reported as round 240 with additional Alchemist buffs, without Homeland.

Collision represents about 25.3% of that instrumented wall time and tracking
about 9.25%. Even if ALL collision cost could scale perfectly over four workers,
the fixed-workload Amdahl estimate is only about 1.23x overall speedup; for ALL
tracking cost alone it is about 1.07x. Actual replaceable geometry is only an
unknown subset. These are illustrative ceilings under ideal assumptions, not
predictions: timings overlap, native-hook overhead is included, and the workload
can vary. They must not be added together to claim a combined benefit.

Millions of tiny per-object jobs would incur scheduling and interop costs. Any
parallel experiment must batch primitive data with bounded queues; copying,
validation, scheduling and main-thread application must be included in its cost.
Keeping a full vanilla calculation alongside a replacement measures correctness
but cannot establish the replacement's production speedup. No speedup is promised.

## Candidate decisions

| Approach | Decision now | Reason |
| --- | --- | --- |
| Task.Run/Parallel.For on original Process methods | Reject | Live mutable state and unspecified ordering/thread safety |
| New spatial grid as a blanket fix | Reject premise | A spatial grid already exists; no demonstrated redundant search |
| Cache whole target searches across ticks | Reject without invalidation proof | Existing delays, mutable targets and filters; stale results change gameplay |
| Copied-data collision candidate builder | Deferred | Lazy iterator semantics, ordering, mutation epochs and native handoff unknown |
| Copied-data homing geometry | Conditional research candidate | Smaller potential boundary, but formula/read set and useful cost unknown |
| Reuse mod-owned diagnostic buffers | Feasible, already used | Reduces our own overhead, not established as a game optimisation |

## Concrete next experiment and acceptance gates

1. Specify native homing geometry first: establish exactly what the angle or
   direction calculation reads/writes and which overrides execute. If not feasible,
   stop this candidate rather than modifying live simulation on speculation.
2. Add a separate default-off, bounded capture experiment at that narrow boundary.
   Copy verified primitive inputs at original call entry and observe original
   return/output at exit. Do not invoke the original twice or enumerate game data
   again. Tag samples with tick/object generation/model configuration. Never carry
   native references into a worker. Capture must preserve original exceptions.
3. Implement a serial pure-data reference from the established specification.
   Replay captured inputs offline; compare exact output bits, decisions and order.
   Include angle wrap, zero distance, target death/change, buffs, upgrades, save
   reload and relevant projectile variants. Log counterexamples, not just pass rates.
4. Measure this serial kernel and snapshot acquisition separately. If the kernel is
   too small to repay data copying and synchronization, reject multicore for it.
5. Only then prototype batching. Prove the snapshot epoch is valid across the batch,
   compare serial/parallel outputs across worker counts, and keep applying results
   in vanilla order. Late, stale or failed results fall back BEFORE any mutation.
6. Do not integrate until native behavior is understood, counterexamples resolved,
   deterministic per-tick game-state comparisons exist and measured net benefit
   exceeds repeated-run variation. Build success and a visually normal round do
   not satisfy these gates. Unvalidated versions/modes stay on vanilla behavior.

The current project has none of that replay/equivalence infrastructure yet. No
more repeated whole-round timing runs are necessary until there is a concrete
capture or replacement experiment to evaluate. Preserve the round-240 save.
