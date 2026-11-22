using Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prerender;
using Storyblok;

if (args is not [string wwwroot])
{
    Console.Error.WriteLine("Usage: dotnet run --project Prerender -- <published wwwroot folder>");
    return 1;
}

try
{
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.GetFullPath(Path.Combine(wwwroot, "appsettings.json")))
        .Build();
    IConfigurationSection storyblok = configuration.GetSection("Storyblok");

    if (string.IsNullOrWhiteSpace(storyblok["ApiKeyPublic"]))
    {
        return Skip("the published appsettings.json has no Storyblok public token");
    }

    if (!string.IsNullOrWhiteSpace(storyblok["ApiKeyPreview"]))
    {
        return Skip("this is the editor build, which shows drafts");
    }

    Site site = Site.FromConfiguration(configuration);
    await using ServiceProvider services = new ServiceCollection().AddPrerendering(storyblok, site).BuildServiceProvider();

    RenderedHome home = await HomeRenderer.RenderAsync(services);
    string stories = services.GetRequiredService<PrerenderedStories>().ToJson();
    new PublishedSite(wwwroot, site).WriteHome(home.Html, stories, home.Profile);
    Console.WriteLine($"Prerendered the home page into index.html; every other route now falls back to {PublishedSite.ShellFile}.");

    foreach (string label in home.LinksWithoutAddress)
    {
        Warn($"The link \"{label}\" has no address the site can use, so it shows as not available. In Storyblok, give it a web address with https://, an email address or another story.");
    }
}
catch (Exception exception)
{
    return Skip(ExceptionText.Describe(exception), exception);
}

// The rendered HTML includes the icons selected in Storyblok.
try
{
    string page = File.ReadAllText(Path.Combine(wwwroot, "index.html"));
    HashSet<string> icons = [.. FontAwesomeSubset.ComponentIcons, .. FontAwesomeSubset.IconsIn(page)];

    if (new FontAwesomeSubset().Apply(wwwroot, icons))
    {
        Console.WriteLine($"Cut Font Awesome down to {icons.Count} icons.");
    }
    else
    {
        Warn("Font Awesome wasn't cut down: pyftsubset (fonttools) isn't installed. The site ships it whole.");
    }
}
catch (Exception exception)
{
    Warn($"Font Awesome wasn't cut down: {ExceptionText.Describe(exception)}. The site ships it whole.", exception);
}

return 0;

static int Skip(string reason, Exception? exception = null)
{
    Warn($"The home page wasn't prerendered: {reason}. The site deploys without it.", exception);
    return 0;
}

static void Warn(string message, Exception? exception = null)
{
    Console.WriteLine($"::warning::{message}");
    if (exception is not null)
    {
        Console.WriteLine("::group::Details");
        Console.WriteLine(exception);
        Console.WriteLine("::endgroup::");
    }
}
