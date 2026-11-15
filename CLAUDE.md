# link-in-bio

README.md describes the projects, how to run and test them, and how the deploy works; read it before changing those.

## Checks before a change is done

- `dotnet test --solution link-in-bio.slnx --configuration Release`: Release treats warnings as errors, as CI does.
- `node --test "Tests/js/*.test.mjs"` after changing anything in `Client/wwwroot/js` or `.github/scripts`.
- After changing classes in a `.razor` file or `Client/Styles/app.css`, rebuild `Client/wwwroot/css/app.css` (README,
  Styles) and commit it with the change.

## Commit messages

One line, lowercase, no prefix or ticket number, saying what the change does in plain words, several changes joined
with commas: `deploy on a Storyblok publish, take the profile metadata from Storyblok, check the CSP before deploying`.
No body unless something needs explaining. Branches are named `fix/…`, `feat/…`, `perf/…` or `chore/…`.

## Style

- Comments and docs say why, in plain sentences; the README and code comments are written the same way.
- Every byte a visitor downloads counts: the Client avoids extra assemblies (no Polly, no IHttpClientFactory, no
  configuration binder), and the comments say so where a hand-written piece replaces a library.
- Files are UTF-8 without a BOM.
