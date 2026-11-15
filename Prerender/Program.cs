using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prerender;
using Storyblok;

// Renders the home page into a published site: dotnet run --project Prerender -- <publish folder>/wwwroot
// The deploy runs it after publishing production. It never fails the deploy: whatever goes wrong, the site is left as
// published, which works exactly as before, only without content in the first HTML.

if (args is not [string wwwroot])
{
    Console.Error.WriteLine("Usage: dotnet run --project Prerender -- <published wwwroot folder>");
    return 1;
}

try
{
    IConfigurationSection storyblok = new ConfigurationBuilder()
        .AddJsonFile(Path.GetFullPath(Path.Combine(wwwroot, "appsettings.json")))
        .Build()
        .GetSection("Storyblok");

    if (string.IsNullOrWhiteSpace(storyblok["ApiKeyPublic"]))
    {
        return Skip("the published appsettings.json has no Storyblok public token");
    }

    // The editor build shows drafts; content baked in at deploy time would be out of date there by design.
    if (!string.IsNullOrWhiteSpace(storyblok["ApiKeyPreview"]))
    {
        return Skip("this is the editor build, which shows drafts");
    }

    await using ServiceProvider services = new ServiceCollection().AddPrerendering(storyblok).BuildServiceProvider();

    RenderedHome home = await HomeRenderer.RenderAsync(services);
    new PublishedSite(wwwroot).WriteHome(home.Html, services.GetRequiredService<PrerenderedStories>().ToJson(), home.Profile);
    Console.WriteLine($"Prerendered the home page into index.html; every other route now falls back to {PublishedSite.ShellFile}.");
}
catch (Exception exception)
{
    return Skip(ExceptionText.Describe(exception), exception);
}

// Only after a prerender: the icons of the links come from the rendered page. An icon added in Storyblok after this
// deploy stays blank until the next one (a publish in Storyblok starts one, see the README's Deployment section).
try
{
    string page = File.ReadAllText(Path.Combine(wwwroot, "index.html"));
    HashSet<string> icons = [.. FontAwesomeSubset.ComponentIcons, .. FontAwesomeSubset.IconsIn(page)];

    Console.WriteLine(new FontAwesomeSubset().Apply(wwwroot, icons)
        ? $"Cut Font Awesome down to {icons.Count} icons."
        : "::warning::Font Awesome wasn't cut down: pyftsubset (fonttools) isn't installed. The site ships it whole.");
    return 0;
}
catch (Exception exception)
{
    Console.WriteLine($"::warning::Font Awesome wasn't cut down: {ExceptionText.Describe(exception)}. The site ships it whole.");
    Details(exception);
    return 0;
}

// A GitHub Actions warning: visible on the run, but the deploy goes on. An exception's stack trace follows in a
// collapsed group of the log.
static int Skip(string reason, Exception? exception = null)
{
    Console.WriteLine($"::warning::The home page wasn't prerendered: {reason.TrimEnd('.')}. The site deploys without it.");
    if (exception is not null)
    {
        Details(exception);
    }

    return 0;
}

static void Details(Exception exception)
{
    Console.WriteLine("::group::Details");
    Console.WriteLine(exception);
    Console.WriteLine("::endgroup::");
}
