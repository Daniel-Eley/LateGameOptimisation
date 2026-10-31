# Native collision investigation: first pass

31 October 2026. AI-assisted analysis using local binaries; no runtime patch or
game installation changes. This advances the earlier [interface feasibility
assessment](FEASIBILITY.md), which had not inspected native implementations.

## Result

**There is a concrete boundary worth investigating: collision-grid iterator
traversal versus collision validation/effects.** The current collision timer
combines both. Native inspection confirms that they are interleaved rather than
two independent batch phases.

The next targeted measurement should distinguish time in the bloon grid's
CollisionState.MoveNext from time in Projectile.CollideBloon. Do not implement a
parallel collision loop yet. A lower-overhead serial iterator optimisation is a
plausible research candidate, but no redundant operation or safe replacement has
been proved, and no performance change is implemented by this investigation.

## Local evidence and validation

The loader retained Cpp2IL output with method RVA, file-offset and length metadata.
That let us map the generated wrapper token for CollisionChecker.Process to the
native entry point. Its token is 0x06004156, agreeing with the wrapper's method
lookup. GameAssembly SHA-256 is
`CAF18ADDE5B26658F65A8D040413624886FF90BB15C789A29DED4736F6E6689C`.
The binary's SHA-512 also matches the loader's generation configuration.

Used the loader's local Mono.Cecil and Iced libraries to inspect metadata and
decode x64 instructions. The reproducible script checks the reviewed binary hash
and selected RVA/file-offset ranges against executable PE sections. Native game
code was not loaded or called by the inspection script. Disassembly is retained
only in ignored `.local-build/native/`; no proprietary binaries or disassembly
are committed.

Selected mapped entries in this exact build:

| Entry | RVA | Mapped size (bytes) |
| --- | --- | ---: |
| CollisionChecker.Process(Int32) | 0xF31590 | 1187 |
| Projectile.CollideBloons(CollisionState<Bloon>) | 0xC8BCE0 | 132 |
| Projectile.CollideBloon(Bloon, Boolean) | 0xC8B970 | 875 |
| Projectile.IsCollisionValid(Bloon) | 0xC8D840 | 366 |
| CollisionGrid<T>.GetInRange | 0x24B34E0 | 150 |
| CollisionGrid<T>.Fill | 0x24B2EF0 | 812 |
| CollisionState<T>.Initialize | 0x24B3ED0 | 568 |
| CollisionState<T>.MoveNext | 0x24B4110 | 1249 |

Addresses are version-specific evidence, not proposed patch offsets. Native
generic bodies can be shared by multiple types, and different managed methods can
share machine-code addresses. The script reports all matching aliases instead of
pretending every address uniquely identifies a method. Indirect virtual/interface
calls remain incompletely resolved. Generic field offsets of zero in the Cpp2IL
metadata are placeholders and were not treated as actual object layouts.

## Confirmed execution structure

### CollisionChecker.Process

The traced routine acquires/enumerates projectiles, checks destroyed/model flags,
then conditionally processes bloon collisions, prop collisions and map blockers.
It rechecks the projectile's destroyed flag after the earlier collision stages.
The bloon path calls HasValidBloonCollision, queries the bloon grid using
GetInRange and immediately passes that result to CollideBloons. The prop path uses
its own grid and collision callback. The blocker path enumerates candidates,
performs additional checks and can call CollideMap and Destroy.

There is no direct call to CollisionGrid.Fill in the inspected Process routine.
Fill has its own clear/enumerate/add implementation; its registration/caller path
has not yet been traced. Do not infer that our CollisionChecker timer separately
measures grid rebuilding, or that rebuilding cannot occur in some nested callback.

### GetInRange initializes a query; it does not return all candidates

The native routine selects an existing collision-state object, advances a rotating
index (wrapping at the pool length), clamps a query value, calls Initialize and
returns that object. The inspected direct path does not allocate a fresh result
list or walk all candidate objects.

This makes GetInRange alone a poor proxy for candidate-search cost. It also means
that retaining pooled states or calling the query speculatively can change pool
state and eventually overwrite live query objects. No safe retained lifetime has
been established for workers.

### MoveNext contains traversal AND geometric filtering

The iterator maintains bucket and within-bucket positions, follows neighbor
offsets, performs bounds/empty checks, and tests candidate geometry before updating
Current and its cursor and returning. The observed paths include squared-distance
comparisons and a secondary per-object collection of offset/radius geometry.
Some bucket conditions can bypass the first distance test. The exact meaning of
all generic/interface accesses, constants and geometry variants is not yet fully
resolved; this is not simply a universal distance test over a flat list.

It already uses squared distances on the observed path, so replacing a supposed
square-root test with squared distance is not a supported optimisation proposal.
The original arithmetic/branch order and floating-point edge behavior still need
to be specified before any equivalent geometry kernel can be written.

### Candidate generation and effects alternate

The CollisionState overload of CollideBloons does this structurally:

1. Advance the original iterator.
2. Pass its current bloon to CollideBloon.
3. Stop if that call says not to continue; otherwise advance again.

This is a control-flow summary, not recovered source. It confirms that later
enumeration occurs AFTER earlier collision callbacks. Precomputing a whole list
before applying effects could observe a different world, perform work the original
would never request, or mishandle object lifetime. Validating only the candidates
already in a snapshot does not prove that new candidates are absent.

### CollideBloon is not a pure geometric predicate

The traced method includes live-state/pierce checks, IsCollisionValid, collision
history updates, ordered behavior iteration with interface calls, pierce updates,
exhaustion handling and additional virtual callbacks. IsCollisionValid includes
FilterBloon and a branch calling Map.IsTargetLineClear. Non-generic field offsets
agree with projectile collision history, behavior-pass list and pierce fields.
Some virtual callbacks occur before later guards, so moving guards or deleting
apparently redundant calls can change side effects.

Not every callback has been resolved, and this report does not claim a complete
damage/spawn/RNG mutation graph. Those unresolved callbacks are an explicit blocker
to parallel application of collisions.

## What this changes about optimisation choices

| Proposal | Assessment |
| --- | --- |
| Time GetInRange and call it candidate-search time | Misleading: traversal is deferred into MoveNext |
| Cache or share CollisionState between worker threads | Reject: pooled mutable cursor and live backing data |
| Parallelise CollideBloon calls | Reject for now: history, pierce, behavior callbacks and ordering dependencies |
| Replace square roots with squared distances | Not supported by this path: squared-distance checks already exist |
| Optimise MoveNext serially while preserving lazy traversal | Plausible investigation boundary; semantics and cost must be established |
| Batch geometric tests over copied data | Conditional future experiment; requires snapshot consistency and identical lazy-result behavior |
| Optimise collision callbacks rather than searching | Potentially important; first determine their share of measured time |

A serial iterator replacement would preserve the original callback order and
observe live state at the same logical points, making it more contained than a
frame-wide parallel batch. It could still be slower: native-to-managed transitions,
many wrapper property reads and generic dispatch may cost more than any algorithmic
saving. No replacement should proceed without an exact specification and benchmark.

## Next experiment specification

Do not add dozens of hot-method probes. Introduce a separate, default-off,
restart-required collision-breakdown experiment only after verifying the hook
scope for shared native generic code:

- Keep CollisionChecker.Process as the parent timer.
- During that parent call, distinguish bloon-grid MoveNext and CollideBloon
  time/call counts; label other or unresolved paths separately. Do not label a
  shared MoveNext body as bloon-only unless instance/type discrimination is proven.
- Record candidate yields versus iterator advances where observable without
  advancing the iterator twice or retaining Current beyond original use.
- Keep broad-only baseline runs. Instrumentation itself may significantly affect
  the millions-of-calls workload; use a native sampling trace instead if precise
  probes distort it excessively. No claim about an installed sampling tool is made.
- If traversal dominates, fully specify its bucket order, radius overrides,
  secondary shapes, branch boundaries, pooled lifecycle and mutation handling.
  Build a serial comparison-only implementation using recorded primitive inputs.
- If callbacks dominate, stop treating faster candidate searches as the primary
  solution and inspect the expensive callback path instead.

Dynamic test results are needed to choose between these branches; static code
length does not establish runtime cost. No new recordings are required with the
unchanged profiler, and this investigation has not installed new hooks.

## Reproduction

```powershell
New-Item -ItemType Directory -Force .local-build/native | Out-Null
pwsh -File scripts/Inspect-NativeCollision.ps1 > .local-build/native/collision.txt
pwsh -File scripts/Inspect-NativeCollision.ps1 `
  -TypePattern '^Assets.Scripts.Simulation.Physics.CollisionGrid`1(/CollisionState)?$|^Assets.Scripts.Simulation.Towers.Projectiles.Projectile$' `
  -MethodPattern '^(GetInRange|Initialize|MoveNext|Fill|CollideBloons|CollideBloon|IsCollisionValid)$' `
  > .local-build/native/collision-path.txt
```

Requires the loader's retained Cpp2IL output and installed Cecil/Iced assemblies.
Output is native disassembly and must remain local. Script execution and PE-range
checks were verified for CollisionChecker.Process and the broader selection
shown above. Interpretation is manual and incomplete, not
a formal decompilation or proof. No recovered game source is offered for reuse.
