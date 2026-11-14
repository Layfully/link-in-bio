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

    if (await HomeRenderer.RenderAsync(services) is not { } html)
    {
        return Skip("the home story didn't load as a page with content");
    }

    new PublishedSite(wwwroot).WriteHome(html, services.GetRequiredService<PrerenderedStories>().ToJson());
    Console.WriteLine($"Prerendered the home page into index.html; every other route now falls back to {PublishedSite.ShellFile}.");
}
catch (Exception exception)
{
    return Skip(exception.Message);
}

// Only after a prerender: the icons of the links come from the rendered page. An icon added in Storyblok after this
// deploy stays blank until the next one.
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
    Console.WriteLine($"::warning::Font Awesome wasn't cut down: {exception.Message} The site ships it whole.");
    return 0;
}

// A GitHub Actions warning: visible on the run, but the deploy goes on.
static int Skip(string reason)
{
    Console.WriteLine($"::warning::The home page wasn't prerendered: {reason}. The site deploys without it.");
    return 0;
}
