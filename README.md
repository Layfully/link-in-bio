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

## Running it locally

You need the .NET 11 SDK.

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

## Styles

`Client/Styles/app.css` is the Tailwind CSS 4 source; `Client/wwwroot/css/app.css` is the built file and is committed.
After changing classes in a `.razor` file or the source stylesheet, rebuild it from the `Client` folder, using either
the [standalone Tailwind CLI](https://tailwindcss.com/docs/installation/tailwind-cli) or npm:

```bash
npx @tailwindcss/cli@4.3.5 --input Styles/app.css --output wwwroot/css/app.css --minify
```

CI rebuilds it the same way and warns when the committed file differs.

## Third-party libraries

Font Awesome, tsParticles and typed.js are served from `Client/wwwroot/lib` rather than a CDN. That way no visitor's
address goes to a third party before they've agreed to anything, and the Content Security Policy only allows the site's
own origin.

| Folder | Package | Files taken from it |
| --- | --- | --- |
| `fontawesome-7.3.1` | `@fortawesome/fontawesome-free@7.3.1` | `css/all.min.css`, `webfonts/*.woff2`, `LICENSE.txt` |
| `tsparticles-4.5.0` | `@tsparticles/slim@4.5.0` | `tsparticles.slim.bundle.min.js`, `LICENSE` |
| `typed.js-2.1.0` | `typed.js@2.1.0` | `dist/typed.umd.js` and its `.map`, `LICENSE.txt` |

The version is part of each folder's name, so a file's address changes whenever its content does. That is what lets
browsers keep these files for a year without asking again (see [Caching](#caching)).

Dependabot doesn't see these files. To update one:

1. Download the new version's tarball (`npm pack <package>@<version>`) and check it against the registry's
   `dist.integrity` (`npm view <package>@<version> dist.integrity`).
2. Copy the files listed above into a **new** folder named after the new version, and delete the old folder. Never
   change files inside an existing folder: browsers that cached them would keep the old copies for a year.
3. Update the paths in `Client/wwwroot/index.html` and this table, then check the icons, the particle background and
   the typed taglines.

## Caching

`Client/wwwroot/staticwebapp.config.json` sets how long browsers keep files:

- `_framework/*` and `lib/*` are cached for a year as `immutable`: their names change whenever their content does.
- Everything else, `index.html` included, is `no-cache`. Browsers keep a copy but check it on every visit, so a deploy
  is never answered with a stale page that points at `_framework` files that no longer exist.

The site's own `css/app.css` and `js/*.js` could be cached for a year too if their names carried a fingerprint.
Blazor's `#[.{fingerprint}]` placeholder in `index.html` is documented for JavaScript modules only, so check a real
`dotnet publish` before relying on it: a placeholder that isn't replaced would break the link.

## Deployment

`.github/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml` builds every push and pull request, then
deploys to Azure Static Web Apps:

- **Production** (pushes to `main`) and **pull request staging environments** are built with the public token only.
- **The `editor` environment** (pushes to `main`) is built with the preview token as well. It's the address Storyblok's
  Visual Editor previews, `https://<app>-editor.<region>.azurestaticapps.net`, so drafts appear there and nowhere else.
  Set it as the preview URL in Storyblok's space settings.

Everything in a WebAssembly app's `appsettings.json` can be downloaded by any visitor, which is why the preview token,
which reads drafts, is never part of the production build.

Dependabot's pull requests and pull requests from forks get no secrets, so they're built but not deployed.

The workflow needs these repository secrets:

| Secret | Used for |
| --- | --- |
| `STORYBLOK_API_KEY_PUBLIC` | Published content, in every build. |
| `STORYBLOK_API_KEY_PREVIEW` | Drafts, in the `editor` build only. |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_GRAY_TREE_0C2E88D03` | Deploying to Azure Static Web Apps. |

### Content Security Policy

`Client/wwwroot/staticwebapp.config.json` sends a strict Content Security Policy. The import map that Blazor writes into
`index.html` is an inline script whose content changes with every build, so the policy allows it by hash:
`Client/Csp.targets` computes that hash after each `dotnet publish` and writes it into the published config, in place
of the `sha256-IMPORTMAP-HASH-SET-AT-PUBLISH` placeholder.

## License

MIT. See [LICENSE](LICENSE). The libraries in `Client/wwwroot/lib` keep their own licenses, next to their files.
