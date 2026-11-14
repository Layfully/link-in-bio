using Client;
using Client.Data.Components;
using Storyblok;

namespace LinkInBio.Tests.Client;

/// <summary>The app's component classes and their source-generated JSON metadata.</summary>
public sealed class ComponentJsonTests
{
    // A new [StoryblokComponent] class that isn't added to ComponentJson would stop the app at startup; this says so first.
    [Fact]
    public void Every_component_class_of_the_app_has_json_metadata()
    {
        StoryblokComponentTypes types = new([typeof(App).Assembly], ComponentJson.Default);

        Assert.True(types.TryGetType("page", out _));
    }
}
