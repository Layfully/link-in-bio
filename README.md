# link-in-bio

A link-in-bio page: a header with a photo and typed taglines, a list of profile links, and certificate pages. It is a
Blazor WebAssembly app on .NET 11, gets its content from the [Storyblok](https://www.storyblok.com) headless CMS, and is
hosted on Azure Static Web Apps.

![image](https://github.com/Layfully/link-in-bio/assets/15947565/e5cf6622-88b9-4b5c-8234-a9a93ce92160)

## Projects

| Folder | What it is |
| --- | --- |
| `Client` | The Blazor WebAssembly app: pages, components, styles and the static files in `wwwroot`. |
| `Storyblok` | A small client for Storyblok's Content Delivery API v2 that reads stories into the app's component classes. |
| `Api` | The site's API, an Azure Function deployed with it: passes Storyblok's webhook on to GitHub, so a publish starts a deploy (see [Deploying on a publish in Storyblok](#deploying-on-a-publish-in-storyblok)). |
| `Prerender` | A console app the deploy runs after publishing: it puts the home page's content into the HTML (see [Prerendering](#prerendering)). |
| `Tests` | Unit tests for the Storyblok client, the link converter, the pages, the prerendering and the API's webhook. |

## Running it locally

You need the .NET SDK that `global.json` names (11.0.100, or a later patch of it).

1. Put the Storyblok **public** token in `Client/wwwroot/appsettings.Development.json`, which git ignores:

   ```json
   {
     "Storyblok": {
       "ApiKeyPublic": "your public token"
     }
   }
   ```

   To try the Visual Editor locally, add `"ApiKeyPreview"` as well and open the page with `?_storyblok` in the address.
   Without a preview token the app ignores `?_storyblok` and shows published content.

2. Run `dotnet run --project Client` and open the address it prints.

CI also installs the WebAssembly build tools (`dotnet workload install wasm-tools`), which make `dotnet publish` relink
the .NET runtime into a smaller file. A local publish works without them, only with a larger download.

## Tests

`Tests` holds the unit tests (xunit v3): the Storyblok client against a fake Storyblok (how each answer becomes a result,
how Storyblok's values are read, how slugs are escaped, what is cached), the retries and timeouts of its requests, the
component registry, the image addresses, the link converter's safety rules, the Visual Editor check, the JavaScript
interop (a typing animation that finishes starting too late is stopped), and with
[bUnit](https://bunit.dev) the pages (what each answer from Storyblok shows), the loading hints and the layout's error
message, and the prerendering (the rendered page, the profile it takes from Storyblok, how it is written into the
published site, which Font Awesome icons stay, what a failure says), and the webhook that starts a deploy (which calls
it refuses, what it sends GitHub, which failures it tries again). Run them with:

```bash
dotnet test --solution link-in-bio.slnx
```

The particle background's movement rules and drawing (`particle-field.js`, `particle-scene.js`), when Blazor starts
(`blazor-start.js`) and the analytics consent (`analytics-consent.js`: nothing of Google before a yes), all in
`Client/wwwroot/js`, and the deploy's Content Security Policy check (`.github/scripts/check-published-csp.mjs`) have
tests of their own in `Tests/js`, run with Node's built-in test runner, no packages needed:

```bash
node --test "Tests/js/*.test.mjs"
```

`global.json` switches `dotnet test` to Microsoft Testing Platform, which xunit v3 needs on .NET 10 and later. It also pins
the SDK: CI installs the same one, and Dependabot proposes newer ones as pull requests. CI runs the tests in Release on
every push and pull request, before anything is deployed.

## Code style

`.editorconfig` describes the code style (explicit types rather than `var`, braces everywhere, `_camelCase` private
fields, no unused usings), and the build checks it together with the .NET analyzers' recommended rules. Release builds,
which CI makes, treat every warning as an error.

## Styles

`Client/Styles/app.css` is the Tailwind CSS 4 source; `Client/wwwroot/css/app.css` is the built file and is committed.
Publishing puts it inline into `index.html` (see [Content Security Policy](#content-security-policy)).
After changing classes in a `.razor` file or the source stylesheet, rebuild it from the `Client` folder, using either
the [standalone Tailwind CLI](https://tailwindcss.com/docs/installation/tailwind-cli) or npm:

```bash
npx @tailwindcss/cli@4.3.5 --input Styles/app.css --output wwwroot/css/app.css --minify
```

CI rebuilds it the same way and warns when the committed file differs. The deploy always ships the file it builds
itself (`.github/scripts/build-app-css.sh`), so a forgotten rebuild never reaches visitors; the committed copy is for the
dev server.

## Third-party libraries

Font Awesome and typed.js are served from `Client/wwwroot/lib` rather than a CDN. That way no visitor's
address goes to a third party before they've agreed to anything, and the Content Security Policy only allows the site's
own origin.

| Folder | Package | Files taken from it |
| --- | --- | --- |
| `fontawesome-7.3.1` | `@fortawesome/fontawesome-free@7.3.1` | `css/all.min.css`, `webfonts/*.woff2`, `LICENSE.txt` |
| `typed.js-2.1.0` | `typed.js@2.1.0` | `dist/typed.umd.js` and its `.map`, `LICENSE.txt` |

The version is part of each folder's name, so a file's address changes whenever its content does. That is what lets
browsers keep these files for a year without asking again (see [Caching](#caching)).

Dependabot doesn't see these files. To update one:

1. Download the new version's tarball (`npm pack <package>@<version>`) and check it against the registry's
   `dist.integrity` (`npm view <package>@<version> dist.integrity`).
2. Copy the files listed above into a **new** folder named after the new version, and delete the old folder. Never
   change files inside an existing folder: browsers that cached them would keep the old copies for a year.
3. Update the paths in `Client/wwwroot/index.html` and this table, then check the icons and the typed taglines. The
   deploy's Font Awesome cut (see [Prerendering](#prerendering)) finds the folder through the link in `index.html`, so
   it needs no change.

For typed.js, also check that the taglines still take turns without the first one coming back half-deleted:
`Client/Components/TypedText.razor.js` resets `typed.strPos`, which is typed.js's internal state, not its documented
API, and a new version may rename or drop it.

The Tailwind CLI is pinned in the workflow, where Dependabot doesn't see it either (`TAILWIND_VERSION` and its
`TAILWIND_SHA256`, also named in [Styles](#styles)); update it by hand now and then. fonttools and brotli, for the Font
Awesome cut, are pinned with their hashes in `Prerender/requirements.txt`, which Dependabot keeps up to date.

The particle background used tsParticles until November 2026; it is now drawn by the site's own script
(`Client/wwwroot/js/particle-background.js`), which saves every visitor 160 KB and about a third of a second of
script.

## Caching

`Client/wwwroot/staticwebapp.config.json` sets how long browsers keep files:

- `_framework/*` and `lib/*` are cached for a year as `immutable`: their names change whenever their content does.
- The exception is `_framework/blazor.webassembly.js` (and its `.map`). A standalone WebAssembly app publishes it
  without a fingerprint, although `index.html` asks for one, so it is `no-cache`. Otherwise a returning visitor would
  keep an old loader next to a new runtime after a .NET update.
- Everything else, `index.html` included, is `no-cache`. Browsers keep a copy but check it on every visit, so a deploy
  is never answered with a stale page that points at `_framework` files that no longer exist. It is also
  `no-transform`, which keeps Cloudflare from changing the pages on the way (see [Cloudflare](#cloudflare)).

The site's own `css/app.css` and `js/*.js` could be cached for a year too if their names carried a fingerprint.
Blazor's `#[.{fingerprint}]` placeholder in `index.html` is documented for JavaScript modules only, so check a real
`dotnet publish` before relying on it: a placeholder that isn't replaced would break the link.

## Deployment

`.github/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml` builds every push and pull request, then
deploys to Azure Static Web Apps:

- **Production** (`main`) and **pull request staging environments** are built with the public token only.
- **The `editor` environment** (`main`) is built with the preview token as well. It's the address Storyblok's
  Visual Editor previews, `https://<app>-editor.<region>.azurestaticapps.net`, so drafts appear there and nowhere else.
  Set it as the preview URL in Storyblok's space settings. Its responses carry `X-Robots-Tag: noindex, nofollow`
  (added by `Client/Csp.targets`), so search engines leave it out.

Everything in a WebAssembly app's `appsettings.json` can be downloaded by any visitor, which is why the preview token,
which reads drafts, is never part of the production build.

The `editor` environment is public too, though: anyone who opens its address can download the preview token there and
read the space's drafts. That is accepted for this site, whose drafts contain nothing private. If that changes, protect
the environment with a password (Azure Static Web Apps Standard plan) or preview from `https://localhost:7077/` instead
and stop deploying the editor build.

Dependabot's pull requests and pull requests from forks get no secrets, so they're built but not deployed.

Besides pushes, `main` deploys when the workflow is started by hand from the Actions tab, and when content is published
in Storyblok, which starts it the same way (`workflow_dispatch`, see below). A run started by hand on another branch is
built but never deployed.

Before deploying, CI checks every published page against the site's Content Security Policy
(`.github/scripts/check-published-csp.mjs`): each inline script and style must match a hash in the policy, each
script and stylesheet the page loads must come from a host the policy names, and no element may carry a style or event
handler attribute, which the policy would block. It runs after each publish and
again after prerendering, which changes the page.

The workflow needs these repository secrets:

| Secret | Used for |
| --- | --- |
| `STORYBLOK_API_KEY_PUBLIC` | Published content, in every build. |
| `STORYBLOK_API_KEY_PREVIEW` | Drafts, in the `editor` build only. |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_GRAY_TREE_0C2E88D03` | Deploying to Azure Static Web Apps. |

### Deploying on a publish in Storyblok

The prerendered page, the cut-down Font Awesome and the page's metadata are made at deploy time (see
[Prerendering](#prerendering)), so a publish in Storyblok should start a deploy. Storyblok's webhooks can't send the
`Authorization` header GitHub's API needs, so the site's own API passes them on: `Api` is an Azure Function that the
Static Web App runs next to the site (managed functions, deployed with production only). At
`POST /api/storyblok-webhook?secret=<secret>` it checks the secret and starts this workflow on `main`
(`workflow_dispatch`, the same as "Run workflow" in the Actions tab). Storyblok never repeats a webhook call that failed,
so the API tries GitHub up to three times when it answers with a 5xx, 408 or 429, or can't be reached.

Set it up once:

1. Create a fine-grained GitHub token (Settings → Developer settings → Fine-grained tokens) for this repository only,
   with **Actions: read and write** and nothing else. That lets it start workflow runs, but not push to the repository,
   so a token that leaks can't change the site. Give it an expiry you'll remember: a token that has expired makes the
   webhook answer 502, and publishes stop starting deploys.
2. Make up a long random secret, for example with `openssl rand -hex 32`. It goes in the webhook's address because
   Storyblok's own webhook secrets, which sign each call in a `webhook-signature` header and would keep the secret out
   of the address, come only with its paid plans, and this space is on the free one. An address can end up in logs
   (Cloudflare's, Storyblok's webhook log), so if the secret leaks, make a new one and change it in both places. A
   leaked secret can only start extra deploys.
3. In the Azure portal, open the Static Web App → **Environment variables**, production environment, and add
   `DeployTrigger__GitHubToken` (the token) and `DeployTrigger__WebhookSecret` (the secret). Settings are kept
   between deploys; the build never sees them.
4. In Storyblok, open the space's **Settings → Webhooks** and add one on story **published**, **unpublished**,
   **deleted** and **moved**, with the address `https://adriangaborek.dev/api/storyblok-webhook?secret=<secret>`.
   Storyblok's log shows each call's answer: 202 started a deploy, 401 is a wrong secret, 500 means a setting is
   missing, 502 means GitHub refused (expired token, missing permission) or couldn't be reached.

Several publishes in a row end in one deploy: a newer run on `main` replaces one still in progress. Pull request
environments don't get the API: Static Web Apps copies production's settings to them, and a branch's unreviewed code
mustn't run with the token.

To run the API locally with Azure Functions Core Tools, put the two settings in `Api/local.settings.json` (under
`Values`, as `DeployTrigger__GitHubToken` and `DeployTrigger__WebhookSecret`); git ignores that file.

### Cloudflare

`adriangaborek.dev` is served through Cloudflare; the `azurestaticapps.net` addresses are not. Cloudflare changes the
responses on the way:

- It replaces `Referrer-Policy` (with `same-origin`) and `Strict-Transport-Security`, and adds `X-Frame-Options`, so
  visitors don't get exactly the headers `staticwebapp.config.json` sets. That is accepted. To see the site's own
  headers, request the `azurestaticapps.net` address.
- Bot Fight Mode would add an inline script (JavaScript detections, `/cdn-cgi/challenge-platform/…`) before `</body>`.
  Its text changes with every request, so the Content Security Policy can't allow it by hash; only a nonce made afresh
  for every response could, which a static site can't send. Browsers blocked it and logged a CSP error on every page,
  so the site sends `Cache-Control: no-transform`, and Cloudflare then leaves the pages alone. Bot Fight Mode's other
  protection stays on. CI's CSP check runs on the published folder and wouldn't see such a script.

If Storyblok's webhook log shows Cloudflare's 403 instead of the API's answers, point the webhook at the
`azurestaticapps.net` address instead.

### Prerendering

A Blazor WebAssembly page shows nothing of its own until .NET has started in the browser, which takes seconds. So the
production and pull request builds, after `dotnet publish`, run the `Prerender` project on the published site:

- It loads the home story with the app's own Storyblok client and renders the home page with the app's own components
  (`HtmlRenderer`), and writes the result into the published `index.html` in place of the loading indicator (between
  the `<!--prerender-->` markers). Visitors see the photo and the links in the first HTML.
- Storyblok's response goes into the same page, in `<script type="application/json" id="storyblok-prerendered">`. The
  app reads it on start (`Client/Program.cs`), so its first render shows the same content instead of a placeholder,
  without fading it in a second time. It then asks Storyblok whether the story has changed since the deploy and shows
  the newer one if so. An outage at that point keeps the content the page came with.
- The page as published is kept as `app-shell.html`, and the published config's navigation fallback points there, so
  certificate pages and unknown addresses get the plain page, never the home page's content.
- Both pages get the profile from the home story in their metadata: the photo, cut to 600 x 600, as the link preview
  (`og:image`) and the structured data's `image`, and every web address the page links to as its `sameAs`. The values
  written in `index.html` are the fallback, for builds without prerendering. The description stays as written.
- Font Awesome is cut down to the icons in the prerendered page and in the components (`FontAwesomeSubset.ComponentIcons`,
  which a test keeps complete), fonts included (`pyftsubset` from fonttools). The copy goes to
  `lib/fontawesome-7.3.1-<content hash>`. An icon added to a link in Storyblok shows once the deploy its publish
  starts has finished.

On the prerendered home page, .NET doesn't start right away: starting it takes about a second of the browser's main
thread, and the page has nothing it needs it for at first. `index.html` loads Blazor with `autostart="false"`, and
`Client/wwwroot/js/blazor-start.js` starts it 5 seconds after the page has loaded, at an idle moment: after the first
look, and after page-speed tests have finished measuring. Nothing visible waits for it:

- The particles don't belong to Blazor. `js/particle-background.js` puts them in `#particles`, outside `#app`, from
  the first paint on every page, and Blazor never replaces them. The drawing (`js/particle-scene.js`) runs in a worker
  (`js/particle-worker.js`) on a canvas handed over with `transferControlToOffscreen`, so the dots keep moving while
  .NET's start keeps the main thread busy, instead of freezing for a second. Browsers that can't do that draw on the
  page.
- The typed line shows its first tagline from the start (`data-first`, through CSS). When the typing starts, that
  tagline becomes the element's text, and typed.js carries on from it: it deletes it after the usual pause and types
  the next one, so nothing jumps.

- Blazor's first render replaces the prerendered content with its own copy. A link that has keyboard focus at that
  moment keeps it (`blazor-start.js` moves focus to the same link in the copy), and the app moves focus to a page's
  heading only after navigating inside the app, never on its first render (`Client/App.razor`).

Every other page, and every build without prerendering, starts Blazor at once.

A change published in Storyblok reaches the first HTML with the deploy the publish starts, a few minutes later; until
then visitors see it a moment after the page has loaded. The editor build is never prerendered (it shows drafts).
Whatever goes wrong while prerendering (no token, Storyblok down or refusing the token, no content, a render error)
shows as a warning on the run that says why, with the exception's details in a collapsed group of the log, and the site
deploys as published, which works as before, only without content in the first HTML.

A link in the list whose address the site can't use shows as plain text, "not available". That is meant for an entry
left without an address, but it also happens to a typo, such as `www.example.com` without `https://`, which the site
refuses (only web, email and site addresses are allowed). So the prerender step names each such link in a warning on
the run.

To try it locally, publish, put the public token into the published `appsettings.json`, and run
`dotnet run --project Prerender -- <publish folder>/wwwroot`. Without fonttools installed, Font Awesome stays whole.

### Content Security Policy

`Client/wwwroot/staticwebapp.config.json` sends a strict Content Security Policy. The import map that Blazor writes into
`index.html` is an inline script whose content changes with every build, so the policy allows it by hash:
`Client/Csp.targets` computes that hash after each `dotnet publish` and writes it into the published config, in place
of the `sha256-IMPORTMAP-HASH-SET-AT-PUBLISH` placeholder.

The same step puts `css/app.css` into the published `index.html` as a `<style>` element, in place of its `<link>`, and
allows it by its hash (the `sha256-APPCSS-HASH-SET-AT-PUBLISH` placeholder in `style-src`). The first paint then needs
no request after the page itself; the stylesheet was `no-cache`, so even returning visitors waited for it to be
checked. The dev server keeps the `<link>`: the policy applies only on Azure.

The same step removes `https://app.storyblok.com` from the policy unless the build has the preview token. Only the editor
build runs inside the Visual Editor, which frames the page and loads its bridge script from that origin; the app starts
the bridge only there (`Client/Components/StoryblokBridge.razor`).

A hash that doesn't match shows only in a visitor's browser, as a page that stays blank or unstyled, so CI checks the
published pages against the filled-in policy before every deploy (see [Deployment](#deployment)). To check a local
publish: `node .github/scripts/check-published-csp.mjs <publish folder>/wwwroot`.

## License

MIT. See [LICENSE](LICENSE). The libraries in `Client/wwwroot/lib` keep their own licenses, next to their files.
