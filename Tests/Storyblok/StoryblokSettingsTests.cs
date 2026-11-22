using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Storyblok;
using Storyblok.Extensions;

namespace LinkInBio.Tests.Storyblok;

/// <summary>Tests Storyblok configuration, defaults, and validation.</summary>
public sealed class StoryblokSettingsTests
{
    [Fact]
    public void Settings_are_read_from_configuration()
    {
        StoryblokOptions options = Read(new()
        {
            ["ApiKeyPublic"] = "public-token",
            ["ApiKeyPreview"] = "preview-token",
            ["BaseUrl"] = "https://api-us.storyblok.com/v2/cdn",
            ["CacheDurationSeconds"] = "5",
        });

        Assert.Equal("public-token", options.ApiKeyPublic);
        Assert.Equal("preview-token", options.ApiKeyPreview);
        Assert.Equal("https://api-us.storyblok.com/v2/cdn", options.BaseUrl);
        Assert.Equal(5, options.CacheDurationSeconds);
    }

    [Fact]
    public void Settings_left_out_keep_their_defaults()
    {
        StoryblokOptions options = Read(new() { ["ApiKeyPublic"] = "public-token" });

        Assert.Null(options.ApiKeyPreview);
        Assert.Equal("https://api.storyblok.com/v2/cdn", options.BaseUrl);
        Assert.Equal(900, options.CacheDurationSeconds);
    }

    [Fact]
    public void A_non_numeric_cache_duration_is_rejected() =>
        Assert.ThrowsAny<InvalidOperationException>(() => Read(new() { ["ApiKeyPublic"] = "public-token", ["CacheDurationSeconds"] = "soon" }));

    private static StoryblokOptions Read(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using ServiceProvider services = new ServiceCollection().AddStoryblok(configuration).BuildServiceProvider();
        return services.GetRequiredService<IOptions<StoryblokOptions>>().Value;
    }
}
