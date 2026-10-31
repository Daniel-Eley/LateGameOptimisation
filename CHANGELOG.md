# Changelog

## 0.7.0 - Unreleased

- Add separate opt-in, restart-required collision breakdown profiling, guarded by reviewed binary hashes.
- Time the parent collision pass, bloon-grid collision loop and its nested hit-processing calls.
- Label the loop-minus-hit residual explicitly as including bookkeeping and probe overhead, not pure search time.
- Avoid shared generic iterator hooks; collision mode takes priority over eight-probe detailed mode.
- Append eight CSV columns, mod-version/status events and overlay status; preserve existing columns.
- Add nested/recursive/unwind timing and CSV tests; local checks pass, native interception needs an in-game pilot.
- No gameplay optimisation or measured speedup yet.

## 0.6.0 - Unreleased

- Expand opt-in detailed profiling with projectile travel, tracking, lifetime and process-list entry points.
- Append four timing/count pairs and show an actual installed-probe count (8/8).
- Document new-setup baseline procedure and preserve old CSV meanings.
- Build and CSV tests pass; added native probes need in-game validation.

## 0.5.0 - Unreleased

- Add opt-in detailed timing for collision, attack targeting, weapon and bloon entry points.
- Record per-frame inclusive timings, observed call counts and an installed-probe bitmask.
- Keep original execution intact; document overlap, partial coverage and profiling overhead.
- Build and extended CSV tests pass; new hooks require in-game verification.

## 0.4.0 - Unreleased

- Add lifecycle/state event rows and stop recordings at round end, match end or restart.
- Add opt-in, restart-required main-thread Simulation.Simulate elapsed timing.
- Label transition/unknown/unfocused intervals and retain explicit stop reasons.
- Extend CSV checks; native interception and lifecycle hooks need in-game validation.

## 0.3.0 - Unreleased

- Add F8 start/stop recording and Mod Helper settings buttons.
- Save closed, uniquely named numerical CSVs under Documents/BTD6 Benchmarks.
- Bound recordings and preserve unsaved data for retry after write failures.
- Add recorder tests; build and automated checks passed, in-game validation pending.

## 0.2.0 - Unreleased

- Add a default-on, non-interactive in-game diagnostics overlay with AI disclosure.
- Keep log output separately opt-in and refresh displayed values twice per second.
- Build and standalone statistics checks pass; in-game visual validation remains pending.

## 0.1.0 - Unreleased

- Disclose substantial AI involvement in the README, mod description and distribution template.
- Make gameplay preservation a strict requirement with explicit equivalence and fallback criteria.

- Add opt-in frame interval diagnostics, with no gameplay changes.
- Add standalone statistics checks and a beginner development guide.
- Replace generated publishing workflow with read-only diagnostics checks.
- Use explicit local build references and avoid automatic mod installation.
- No multicore simulation or verified speed improvement yet.
