# First-load performance

## Why

Lighthouse (desktop, 2026-11-14) scores the live site 32: TBT 2,180 ms, LCP 3.3 s, CLS 0.244. Almost all of it is the
.NET WebAssembly runtime starting (about 3 s of script) and the profile photo waiting for that start, the Storyblok
call and the first render (5.2 s resource load delay). The goal is a fast first view while the site stays a Blazor app.

## Approaches

- **A. Tune the WebAssembly app.** Smaller and fewer downloads, less script, no layout shift. Can't remove the runtime's
  start, so TBT stays high.
- **B. Prerender the home page at deploy.** Its HTML and story data go into the page; Blazor shows the same content on its
  first render and refreshes it from Storyblok afterwards. Fixes LCP and CLS on `/`; TBT unchanged.
- **C. Static HTML in production from the same Razor components.** Fixes everything, but content goes live only through
  a rebuild. Not chosen.

Chosen: A, then B. Expected result: roughly 60-75, TBT about 2 s.

## A (this round)

- CI installs the `wasm-tools` workload, so publish relinks `dotnet.native.wasm`.
- tsParticles (160 KB, about 350 ms of script) is replaced by a small canvas script that keeps the current look and
  behaviour: 40 pale purple dots per 1000 x 800 px with faint links, repulse on hover, push on click, standing still
  with reduced motion. Loading the library at idle was considered and dropped: its long task would still count.
- The layout shift is fixed once a trace names the moved elements; it isn't guessed from the code.

## B (next round)

- A `Prerender` console project renders `Home` with `HtmlRenderer` after the production publish, writes `home.html` (shell
  plus HTML in `#app` plus the story JSON in a `<script type="application/json">`) and adds a route from `/` to it. Other
  routes keep the plain shell.
- `Program.cs` seeds the Storyblok client's cache from the embedded JSON, so the first render matches the static HTML,
  without repeating the fade-in. Blazor then loads the live story and re-renders only if it differs.
- Editor and token-less builds skip it; a Storyblok outage during deploy only warns.
- Font Awesome is cut down to the glyphs the published stories and components use (the prerender step knows them);
  the editor keeps the full set. A new icon added in Storyblok shows only after the next deploy.

## Non-goals

Prerendering certificate pages; a Storyblok webhook that rebuilds on publish.
