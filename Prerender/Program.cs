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
    return 0;
}
catch (Exception exception)
{
    return Skip(exception.Message);
}

// A GitHub Actions warning: visible on the run, but the deploy goes on.
static int Skip(string reason)
{
    Console.WriteLine($"::warning::The home page wasn't prerendered: {reason}. The site deploys without it.");
    return 0;
}
