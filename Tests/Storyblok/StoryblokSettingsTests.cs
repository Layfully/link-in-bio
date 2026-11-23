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
    public void AddStoryblok_ShouldReadTheSettingsFromConfiguration_WhenTheyAreSet()
    {
        // Arrange
        Dictionary<string, string?> settings = new()
        {
            ["ApiKeyPublic"] = "public-token",
            ["ApiKeyPreview"] = "preview-token",
            ["BaseUrl"] = "https://api-us.storyblok.com/v2/cdn",
            ["CacheDurationSeconds"] = "5",
        };

        // Act
        StoryblokOptions sut = Read(settings);

        // Assert
        sut.ApiKeyPublic.ShouldBe("public-token");
        sut.ApiKeyPreview.ShouldBe("preview-token");
        sut.BaseUrl.ShouldBe("https://api-us.storyblok.com/v2/cdn");
        sut.CacheDurationSeconds.ShouldBe(5);
    }

    [Fact]
    public void AddStoryblok_ShouldKeepTheDefaults_WhenSettingsAreLeftOut()
    {
        // Arrange
        Dictionary<string, string?> settings = new() { ["ApiKeyPublic"] = "public-token" };

        // Act
        StoryblokOptions sut = Read(settings);

        // Assert
        sut.ApiKeyPreview.ShouldBeNull();
        sut.BaseUrl.ShouldBe("https://api.storyblok.com/v2/cdn");
        sut.CacheDurationSeconds.ShouldBe(900);
    }

    [Fact]
    public void AddStoryblok_ShouldReject_WhenTheCacheDurationIsNotNumeric()
    {
        // Arrange
        Dictionary<string, string?> settings = new() { ["ApiKeyPublic"] = "public-token", ["CacheDurationSeconds"] = "soon" };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Read(settings));
    }

    private static StoryblokOptions Read(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using ServiceProvider services = new ServiceCollection().AddStoryblok(configuration).BuildServiceProvider();
        return services.GetRequiredService<IOptions<StoryblokOptions>>().Value;
    }
}
