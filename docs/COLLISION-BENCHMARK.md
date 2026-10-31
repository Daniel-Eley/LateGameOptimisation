# Collision breakdown benchmark: 0.7.0

This is an observational profiling experiment, not a speed improvement. It is
intended to choose the next investigation target without changing gameplay.

## First pilot

1. Install the new mod DLL while BTD6 is closed. Keep the same save and other mods.
2. In LateGameOptimisation settings, enable **Enable Collision Profiling** and
   disable **Enable Detailed Profiling**, then restart. Simulation Profiling may
   stay on; collision mode also enables it automatically. If installation already
   configured these settings while the game was closed, just launch the game.
3. Confirm **Profile: on**, **Detail: 0/8**, and **Collision breakdown: 3/3**.
   If collision status is unavailable/off, stop here and inspect the loader log.
   Hash mismatches deliberately disable this experiment on unreviewed binaries.
4. Load the unchanged Spa Pits round-240 save with ninja spam and extra Alchemist
   buffs, no Homeland, at built-in 3x. If the setup has changed, document it as a
   new benchmark and use that exact setup for all runs.
5. Press your configured benchmark key **before starting the round** (F7 in the
   owner's current configuration; source default is F8). Start the round, keep
   the game focused, avoid menus/inputs, and let round end stop and save recording.
6. Wait for **Saved**. The CSV is in Windows Documents / BTD6 Benchmarks. No game
   exit is needed to read or copy it. Report that the pilot is ready for inspection.

One pilot comes first. The 3/3 display only proves installation, not that the
native game called the intercepted methods. Check nonzero parent/loop/hit counts
for this workload before spending time on repeated runs. Hits are method calls,
including attempts rejected by validation; they are not successful damage events.

## Compare overhead, then repeat

After the pilot passes coverage checks:

- **Broad baseline:** Simulation Profiling on, Collision Profiling off, Detailed
  Profiling off; restart, confirm Profile on / Detail 0/8 / Collision off.
- **Collision experiment:** Collision Profiling on, Detailed Profiling off;
  restart, confirm Profile on / Detail 0/8 / Collision 3/3.
- Collect three comparable runs per mode from the same starting save. A valid
  pilot can count as one of the three experimental runs. Warm up consistently,
  keep other mod settings fixed, and alternate blocks if thermal drift is apparent.
- Compare round-start to round-end elapsed time, active-round mean frame interval,
  active-round aggregate FPS (sample count / elapsed seconds) and slow-frame
  percentiles. Keep transition intervals separate, and report all runs, not only
  the best result. FPS alone does not measure round completion speed.

If experimental timings materially worsen round duration or frame intervals
beyond baseline variability, do not treat the percentages as an accurate original
cost split. Stop adding hot probes and investigate a suitable native sampling
profiler. No sampling profiler is installed or configured by this mod.

## New CSV columns

| Column(s) | Meaning |
| --- | --- |
| collision_profile_enabled | 1 only when all new probes installed; 0 otherwise |
| collision_parent_ms / collision_parent_calls | Outermost CollisionChecker.Process inside recorded main-thread simulation |
| bloon_loop_ms / bloon_loop_calls | Outermost Projectile.CollideBloons(CollisionState<Bloon>) within that collision pass |
| bloon_hit_ms / bloon_hit_calls | Outermost Projectile.CollideBloon calls within that selected loop |
| bloon_loop_remainder_ms | Loop time minus its measured hit time, including searching, loop bookkeeping and hook overhead |

Times are elapsed wall milliseconds, not CPU time. Native search and effects
remain interleaved in their original order. The mod only times method boundaries;
it never enumerates a second time or modifies arguments, results or exceptions.
Calls outside these nesting boundaries, including other CollideBloons overloads,
are not represented in loop/hit measurements. Nested calls of the same category
are included in the outer timer, rather than added twice.

This residual is a deliberately weaker measurement than the initially proposed
direct MoveNext probe. The native iterator implementation is shared between generic
types. We have not proven safe bloon-only interception, so it is not hooked.
Harmony's [generic patching caveats](https://harmony.pardeike.net/v2/articles/patching-edgecases.html#generics)
provide general background; our actual decision also rests on local native analysis.

The residual includes unmeasured child-hook entry/exit costs. Hit timings themselves
also include some instrumentation overhead and any nested callbacks/other mods.
Candidate-yield counts are not captured; hit calls are not a substitute for exact
iterator-advance or yield counts. There is no approximation or guessed count.

For complete active intervals, check that loop time roughly equals hit time plus
remainder (allow numeric formatting roundoff), and that nested totals make sense
relative to the parent. A lifecycle-triggered drain may split parent completion
from completed loops across boundary rows; child totals are committed together
with their loop. Do not use an individual transition row as a containment proof.
Zero counts or inconsistent coverage must be investigated, not interpreted as a
free subsystem. No performance conclusion is valid from an unverified hook.

## What comes after the measurements

- If hit processing dominates reliably, inspect its validation/behavior callbacks.
- If the loop remainder dominates after overhead checks, continue examining
  traversal, geometry and bookkeeping; do not assume all residual time is searching.
- Only implement a replacement after its semantics and costs are established.
  Gameplay state, event order, floating-point behavior and RNG must remain identical.
  Multicore execution remains conditional on proving independent work.

## Validation completed before installation

Release compilation and standalone recorder/accounting checks passed. Tests cover
recursive timers, ignored calls outside selected scopes, exception-style unwinding,
draining while a scope is live, CSV column alignment and clearing between runs.
The build's NU1900 warning means online vulnerability data was unavailable, not
that vulnerability review passed. No new external dependencies were introduced.
Runtime hook interception and on/off overhead checks remain the purpose of the pilot.
