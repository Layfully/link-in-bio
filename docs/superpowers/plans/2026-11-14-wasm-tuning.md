# WebAssembly Tuning (option A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lower TBT and download size of the live site without changing what visitors see.

**Architecture:** CI relinks the .NET runtime with `wasm-tools`; the particle background becomes a dependency-free canvas
module with the same contract as today (`startParticles(container)` returns `{ stop() }` or `null`); the layout shift
is fixed from a trace.

**Tech Stack:** .NET 11 Blazor WebAssembly, GitHub Actions, plain ES module JavaScript, Canvas 2D.

**Spec:** `docs/superpowers/specs/2026-11-14-first-load-performance-design.md`

## Global Constraints

- Content Security Policy unchanged: scripts only from `'self'`, no inline script or style attributes.
- No new runtime dependency; nothing loaded from a third-party origin.
- The C# interop contract stays: module `./Components/ParticleBackground.razor.js`, export `startParticles`, handle method `stop`.
- Reduced motion: dots drawn once, no movement, no pointer reactions.
- Code comments and README in the repo's existing explanatory style.

## Review Focus

- Leaving the page while the background starts: `stop()` must leave no animation frame, listener or canvas behind.
- Window resized (or phone rotated): canvas matches the new size, density stays 40 per 1000 x 800 px, no stretched dots.
- High-DPI screens: canvas backing store scaled by `devicePixelRatio`, dots stay crisp and the same visual size.
- Background tab: no drawing while hidden (requestAnimationFrame pauses), no burst of movement on return (clamp the time step).
- Clicks keep adding dots: cap the total so a long session can't slow the page down (suggested cap: 2 x the density count).

---

### Task 1: Relink the runtime in CI

**Files:**
- Modify: `.github/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml` (both jobs that publish)
- Modify: `README.md` (Running it locally: optional `dotnet workload install wasm-tools`; Deployment: CI installs it)

- [ ] **Step 1:** After "Setup .NET" in `build_job` and `build_and_deploy_job`, add a step "Install the WebAssembly build tools" running `dotnet workload install wasm-tools`.
- [ ] **Step 2:** Push and open the CI log of "Check that the site publishes". Expected: no "Publishing without optimizations" line; `dotnet.native.*.wasm.br` smaller than 1,003,700 bytes (today's local publish). Note the new size in the commit message.
- [ ] **Step 3:** Commit: `relink the .NET runtime when publishing`.

### Task 2: Replace tsParticles with a canvas module

**Files:**
- Rewrite: `Client/Components/ParticleBackground.razor.js`
- Modify: `Client/wwwroot/index.html` (remove the tsParticles `<script defer>`)
- Delete: `Client/wwwroot/lib/tsparticles-4.5.0/`
- Modify: `README.md` (third-party table and caching text no longer mention tsParticles)
- Unchanged, must still pass: `Tests/Client/InteropTests.cs`

**Interfaces:**
- Produces: `export async function startParticles(container: HTMLElement): { stop(): void } | null` — null when `container` is not connected.

- [ ] **Step 1: Implement the module.** Values copied from today's options: 40 dots per 1000 x 800 px of viewport, colour `#e9d5ff`, dot opacity 0.25, radius 0.5-2 px, links between dots closer than 150 px in `#e9d5ff` at opacity 0.12 x (1 - distance / 150), line width 1; constant speed in a random direction, starting from tsParticles' speed 4 and tuned to look the same; dots leaving one edge re-enter at the opposite one; at most 60 frames a second; hover within 100 px pushes dots away (0.4 s ease), click adds 4 dots at the pointer; pointer events detected on the canvas as today. Append one `<canvas>` to `container`, sized to it. Read `prefers-reduced-motion` once at start. `stop()` cancels the frame, removes the listeners (canvas pointer, window resize) and the canvas.
- [ ] **Step 2:** Remove the script tag and the library folder; drop the `whenLoaded('tsparticles', …)` call (keep `deferred-scripts.js`, typed.js still uses it).
- [ ] **Step 3:** Run `dotnet test --solution link-in-bio.slnx --configuration Release`. Expected: all tests pass (the interop tests use a fake JS runtime and only check the contract).
- [ ] **Step 4:** Run `dotnet run --project Client` and compare with https://adriangaborek.dev side by side: density, speed, link brightness, hover and click; then with reduced motion turned on in the OS (still dots, no reaction); then resize the window. Expected: indistinguishable look; DevTools shows no tsParticles request and no long task from the background.
- [ ] **Step 5:** Commit: `draw the particle background with a small canvas script instead of tsParticles`.

### Task 3: Fix the layout shift

Blocked on a trace: DevTools Performance recording of a reload of https://adriangaborek.dev, Layout shifts track, the
elements each shift moved.

- [ ] **Step 1:** From the trace, name the moved elements and what moved them (write it in the commit message).
- [ ] **Step 2:** Fix the cause in the component or stylesheet responsible; rebuild `app.css` if classes change (README recipe).
- [ ] **Step 3:** Verify with Lighthouse desktop on the pull request's staging address. Expected: CLS below 0.1.
- [ ] **Step 4:** Commit: `<what moved> no longer shifts the page while it loads`.
