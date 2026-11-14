# Prerendered Home Page (option B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The production home page arrives with its content in the HTML, Blazor takes over without a visible change, and
Font Awesome ships only the glyphs in use.

**Architecture:** A `Prerender` console app runs after the production `dotnet publish`: it loads `/home` through the
existing Storyblok client, renders `RouteView(Home, MainLayout)` with `HtmlRenderer`, and rewrites the published
`index.html` (content plus the raw Storyblok response). In the browser, `Program.cs` hands that response to a
`PrerenderedStories` singleton the Storyblok client reads before the network; `Home` renders it at once, without the
fade-in, then refreshes from Storyblok. The same tool cuts Font Awesome down with `pyftsubset`.

**Tech Stack:** .NET 11, Blazor WebAssembly, `Microsoft.AspNetCore.Components.Web.HtmlRenderer`, xunit v3 + bUnit,
fonttools (`pyftsubset`) in CI, GitHub Actions, Azure Static Web Apps.

**Spec:** `docs/superpowers/specs/2026-11-14-first-load-performance-design.md` (section B)

## Global Constraints

- CSP unchanged: no inline executable script, no style attributes. A `<script type="application/json">` data block is allowed.
- The editor build (preview token) and builds without a public token are never prerendered and keep the full Font Awesome.
- Files under `wwwroot/lib/<name-version>/` are never changed in place; a cut-down Font Awesome goes to a new folder whose name contains a content hash.
- A refresh that fails or returns another outcome never replaces prerendered content.
- Release builds stay warning-free; code style per `.editorconfig` (explicit types, braces, `_camelCase`).
- Prerendering failure never fails the deploy: the tool logs a GitHub `::warning::` and leaves the publish untouched.

## Review Focus

- Storyblok unavailable during the deploy: the site deploys unprerendered, exactly as today.
- Home story not found or unreadable at deploy time: no prerender (the static HTML must never show a notice or skeleton).
- A story whose text contains `</script>` or `<!--`: the embedded JSON must not end the data block early.
- Seeded data present but not parseable in the browser: the app ignores it and loads normally.
- Visiting `/Certificate/x` or an unknown path: served `app-shell.html`, never the home content.

---

### Task 1: Prerendered stories in the Storyblok client

**Files:**
- Create: `Storyblok/PrerenderedStories.cs`
- Modify: `Storyblok/Client/StoryblokStoryClient.cs`, `Storyblok/StoryblokStoryQuery.cs`, `Storyblok/Extensions/ServiceCollectionExtensions.cs` (register `PrerenderedStories` as singleton)
- Test: `Tests/Storyblok/StoryblokStoryClientTests.cs`

**Interfaces:**
- Produces: `public sealed class PrerenderedStories` with
  - `bool IsRecording { get; set; }` — when true, every Storyblok response body the client loads is kept;
  - `IReadOnlyDictionary<string, string> Recorded` — escaped story path (as `ToStoryPath` returns, e.g. `"home"`) → raw response JSON;
  - `void AddAll(string json)` — reads `{ "<path>": <response>, ... }` (the embedded block); invalid JSON is ignored (logged by the caller);
  - `string ToJson()` — `Recorded` as that same shape, with `<`, `>` and `&` escaped (default `JsonSerializer` encoder), safe inside `<script>`;
  - `bool Contains(string slug)` — slug as callers pass it (`"/home"`).
- Produces: `StoryblokStoryQuery.TryRefresh<T>(CancellationToken)` → `Task<StoryLoadResult<T>?>`: null when the story wasn't prerendered or Storyblok's `story` object is unchanged (raw JSON text equal); otherwise a fresh load bypassing every cache.
- Client reads the response as a string, then deserializes (needed for recording and comparing). A prerendered response is used before cache and network, once per path, and doesn't count as a network load.

- [ ] **Step 1: Failing tests** (names and assertions):
  - `A_prerendered_story_is_used_without_asking_Storyblok`: `AddAll("{\"home\": <response>}")`; `TryLoad<TestPage>` returns the story; `_storyblok.Requests` empty.
  - `A_refresh_of_an_unchanged_story_is_null`: prerendered response R; fake answers R; `TryRefresh` → null; one request.
  - `A_refresh_of_a_changed_story_is_the_new_story`: fake answers R with another name; `TryRefresh` → story with the new name.
  - `A_refresh_of_a_story_that_wasnt_prerendered_is_null_without_asking`: no requests.
  - `Recording_keeps_the_response_of_each_story`: `IsRecording = true`; load `/home`; `Recorded["home"]` equals the fake's body.
  - `Prerendered_json_cant_end_a_script_block`: record a body containing `</script><!--`; `ToJson()` contains neither `</script` nor `<!--`, and `AddAll(ToJson())` round-trips it.
  - `Unreadable_prerendered_json_is_ignored`: `AddAll("not json")` doesn't throw; `Contains("/home")` false.
- [ ] **Step 2:** Run `dotnet test --solution link-in-bio.slnx`. Expected: the new tests fail (type missing).
- [ ] **Step 3:** Implement as the Interfaces block says.
- [ ] **Step 4:** Run the suite. Expected: all pass.
- [ ] **Step 5:** Commit `let the Storyblok client start from prerendered stories and refresh them`.

### Task 2: Home renders prerendered content at once and refreshes it

**Files:**
- Modify: `Client/Program.cs`, `Client/Pages/Home.razor`, `Client/Components/Header.razor`, `Client/Components/LinkList.razor`
- Test: `Tests/Client/PageTests.cs`

**Interfaces:**
- Consumes: Task 1's `PrerenderedStories`, `TryRefresh<T>`.
- Produces: `Header` and `LinkList` get `[Parameter] public bool Reveal { get; set; } = true;` — the `reveal` class only when true.
- `Program.cs`: after `builder.Build()`, if `(IJSInProcessRuntime)js.Invoke<IJSInProcessObjectReference?>("document.getElementById", "storyblok-prerendered")` is not null, pass its `GetValue<string>("textContent")` to `PrerenderedStories.AddAll`; then `RunAsync`. Element id constant: `PrerenderedStories.ElementId = "storyblok-prerendered"`.
- `Home`: when `Contains("/home")` at init, the loaded state renders with `Reveal=false`, then `TryRefresh<StoryblokPage>`; only a `StoryblokStory<StoryblokPage>` result replaces the state.

- [ ] **Step 1: Failing tests:**
  - `A_prerendered_home_page_shows_its_links_in_the_first_render_without_fading_in`: seed, no fake answer queued before render; first markup contains the link labels; no element has class `reveal`.
  - `A_changed_home_page_replaces_the_prerendered_one`: refresh answer with another label; markup shows it.
  - `A_failed_refresh_keeps_the_prerendered_links`: refresh answers 503 three times; markup still shows the seeded labels, no notice.
- [ ] **Step 2:** Run; expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the suite; expected: all pass.
- [ ] **Step 5:** Commit `show the prerendered home page at once and refresh it from Storyblok`.

### Task 3: The Prerender tool writes the home page into the publish

**Files:**
- Create: `Prerender/Prerender.csproj` (console, references `Client` and `Storyblok`, package `Microsoft.AspNetCore.Components.Web`), `Prerender/Program.cs`, `Prerender/HomeRenderer.cs`, `Prerender/PublishedSite.cs`
- Modify: `link-in-bio.slnx`, `Tests/LinkInBio.Tests.csproj` (reference `Prerender`), `Client/wwwroot/index.html` (markers `<!--prerender-->` and `<!--/prerender-->` around the loader inside `#app`)
- Test: `Tests/Prerender/PublishedSiteTests.cs`, `Tests/Prerender/HomeRendererTests.cs`

**Interfaces:**
- `HomeRenderer.RenderAsync(IServiceProvider services)` → `Task<string?>`: HTML of `RouteView` for `Home` with `MainLayout`, or null unless `/home` loads as a story (not found, unavailable, unreadable → null). Services: `AddStoryblok`, `PrerenderedStories { IsRecording = true }`, `TimeProvider.System`, a `NavigationManager` at `https://adriangaborek.dev/`, an `IJSRuntime` that throws `NotSupportedException` (never called: no `OnAfterRender` in `HtmlRenderer`).
- `PublishedSite(string wwwroot)`: `void WriteHome(string html, string prerenderedJson)` — copies `index.html` to `app-shell.html`, replaces the text between the markers with `html`, inserts `<script type="application/json" id="storyblok-prerendered">{json}</script>` after `#app`, deletes `index.html.br`/`.gz`, sets `navigationFallback.rewrite` in `staticwebapp.config.json` to `/app-shell.html`.
- `Program.Main(args[0] = published wwwroot)`: reads `appsettings.json` from it; skips with `::warning::` when there is no public token, when there is a preview token, when rendering returns null, or on any exception; exit code 0 always.

- [ ] **Step 1: Failing tests:**
  - `The_home_page_is_written_between_the_markers_and_the_shell_is_kept` (temp dir with a minimal index.html): `index.html` has the HTML and the data block, no loader; `app-shell.html` equals the original.
  - `Every_other_route_falls_back_to_the_shell`: config's `navigationFallback.rewrite` is `/app-shell.html`, other config content unchanged (CSP string identical).
  - `Compressed_copies_of_the_old_page_are_removed`.
  - `The_rendered_home_page_shows_the_links_and_fades_in` (FakeStoryblok answering a home story): HTML contains link labels and `reveal`.
  - `A_home_page_that_isnt_published_isnt_rendered`: fake answers 404 → null.
- [ ] **Step 2:** Run; expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the suite; then `dotnet publish Client -c Release -o <tmp>` and `dotnet run --project Prerender -- <tmp>/wwwroot` with the local public token. Expected: `index.html` contains the real link labels; `app-shell.html` exists; the CSP hash in the config is unchanged.
- [ ] **Step 5:** Commit `prerender the home page into the published site`.

### Task 4: Font Awesome cut down to the icons in use

**Files:**
- Create: `Prerender/FontAwesomeSubset.cs`
- Modify: `Prerender/Program.cs` (runs after `WriteHome`, only when it prerendered)
- Test: `Tests/Prerender/FontAwesomeSubsetTests.cs`

**Interfaces:**
- `FontAwesomeSubset.ComponentIcons`: `IReadOnlySet<string>` of the icons the components use (`fa-arrow-right`, `fa-cloud-bolt`, `fa-file-circle-exclamation`, `fa-triangle-exclamation`, `fa-circle-question`, `fa-link-slash`).
- `static IReadOnlySet<string> IconsIn(string html)` — `fa-` classes in rendered HTML, minus style classes (`fa-solid`, `fa-brands`, `fa-regular`, `fa-fw` …).
- `static (string Css, IReadOnlySet<int> Codepoints) Filter(string allMinCss, IReadOnlySet<string> icons)` — keeps every rule except `.fa-…{--fa:"\…"}` icon rules none of whose selectors is used.
- `void Apply(string wwwroot, IReadOnlySet<string> icons)` — runs `pyftsubset <font> --unicodes=… --flavor=woff2 --output-file=…` for `fa-brands-400` and `fa-solid-900` (other webfonts copied as-is) into `lib/fontawesome-7.3.1-<first 8 hex of SHA-256 of css+fonts>/`, rewrites the stylesheet href in `index.html` and `app-shell.html`; when `pyftsubset` isn't on PATH, logs `::warning::` and changes nothing.

- [ ] **Step 1: Failing tests:**
  - `Every_icon_a_component_uses_is_kept`: scan `Client/**/*.razor` for `fa-[a-z0-9-]+` outside `@* *@` and `///` comments, minus style classes; each is in `ComponentIcons`.
  - `Icons_are_read_from_the_rendered_page`: `IconsIn("<i class=\"fab fa-github\"></i><i class=\"fa-solid fa-arrow-right\"></i>")` = {`fa-github`, `fa-arrow-right`}.
  - `Unused_icon_rules_are_dropped_and_shared_rules_kept`: on a snippet of the real `all.min.css` (base rule, `.fa-github`, `.fa-gitlab`, an alias group `.fa-a,.fa-b{--fa:"\f001"}` with `fa-b` used) → keeps base, `fa-github`, the alias group; drops `fa-gitlab`; codepoints {0xf09b, 0xf001}.
  - `Without_pyftsubset_nothing_changes` (PATH without it): `index.html` still links `lib/fontawesome-7.3.1/css/all.min.css`.
- [ ] **Step 2:** Run; expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the suite. Expected: all pass.
- [ ] **Step 5:** Commit `ship only the Font Awesome icons the site uses`.

### Task 5: Prerender in the production deploy

**Files:**
- Modify: `.github/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml`, `README.md` (Deployment, Caching, Third-party libraries)

- [ ] **Step 1:** In `build_and_deploy_job`, after "Publish the site with the public token": install `fonttools==4.55.0 brotli==1.1.0` with pip, then `dotnet run --project Prerender --configuration Release -- publish/site/wwwroot`. The editor publish is untouched.
- [ ] **Step 2:** README: what the deploy prerenders, that content changes in Storyblok show after Blazor's refresh and are baked in at the next deploy, that a new icon from Storyblok stays blank until then, the hashed Font Awesome folder.
- [ ] **Step 3:** Open the pull request; on its staging address check: page source of `/` has the links; `/Certificate/<x>` source is the shell; Lighthouse desktop CLS < 0.1 and LCP < 1.5 s; Font Awesome transfer below 30 KB. Expected: all hold.
- [ ] **Step 4:** Commit `prerender the home page in the production deploy`.
