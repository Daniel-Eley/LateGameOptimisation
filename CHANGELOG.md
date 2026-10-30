# Changelog

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
