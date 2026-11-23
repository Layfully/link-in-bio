# link-in-bio

README.md describes the projects, how to run and test them, and how the deploy works; read it before changing those.

## Checks before a change is done

- `dotnet test --solution link-in-bio.slnx --configuration Release`: Release is what CI builds, and every build treats warnings as errors.
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

## Code conventions

The build enforces the first group (`.editorconfig`, warnings as errors), so a violation fails the build; write it that way from the start.

- C#: file-scoped namespaces, explicit types instead of `var`, braces on every `if`/`for`/`foreach`/`while`, usings outside the namespace and sorted with `System` among the rest, no unused usings. Private fields are `_camelCase`; constants and `static readonly` fields are PascalCase. Four spaces; two in json, yml, csproj, props, targets and slnx. Shell scripts use LF.
- Silencing an analyzer is a line in `.editorconfig` with a comment saying why it can't apply (like `CA1848` or `BL0016` there), never a `#pragma` or a project-wide `NoWarn`.
- Package versions live only in `Directory.Packages.props`; a `.csproj` never carries a `Version`. The SDK and target framework come from `global.json` and `Directory.Build.props`.

How the code reads, which the build doesn't check:

- Types are `internal sealed` unless something else needs them; primary constructors and `record`s for small data; target-typed `new(...)`. A public or internal type gets a `<summary>` saying what it does; the comment on a decision says why.
- Anything that depends on the clock takes a `TimeProvider`, so a test can use `FakeTimeProvider`.
- Tests are xUnit v3 with bUnit for components, run on Microsoft.Testing.Platform, asserting with Shouldly (not `Assert.*`). Names and layout follow the common community convention: `Method_ShouldResult_WhenCondition` (`SendAsync_ShouldRetry_WhenTheAnswerIsTransient`), the object under test is called `sut`, and the body is split into `// Arrange`, `// Act` and `// Assert` sections (merged into `// Act & Assert` when the act is the assertion, as in `Should.Throw`). The class summary says which behavior it covers. Tests use the hand-written fakes (`FakeStoryblok`, `FakeJs`), not a mocking library.
- Browser code in `Client/wwwroot/js` is plain ES modules or one IIFE, no bundler and no dependency; its tests are `node:test` files in `Tests/js`. Third-party files in `wwwroot/lib` are pinned with the version in the folder name (README, Development).
