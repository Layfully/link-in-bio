using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace LinkInBio.Tests.Storyblok;

/// <summary>How component classes are registered, and the mistakes that stop the app at startup instead of misreading content.</summary>
public sealed class StoryblokComponentTypesTests
{
    [Fact]
    public void Classes_are_found_by_their_component_name()
    {
        StoryblokComponentTypes types = new([typeof(Teaser), typeof(Unmarked)]);

        Assert.True(types.TryGetType("teaser", out Type? type));
        Assert.Equal(typeof(Teaser), type);
        Assert.False(types.TryGetType("Teaser", out _)); // names are case-sensitive, like Storyblok's
        Assert.False(types.TryGetType("unmarked", out _));
    }

    [Fact]
    public void Two_classes_for_one_component_are_refused()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new StoryblokComponentTypes([typeof(Teaser), typeof(SecondTeaser)]));

        Assert.Contains("\"teaser\"", error.Message);
    }

    [Theory]
    [InlineData(typeof(NotAComponent))]
    [InlineData(typeof(AbstractComponent))]
    public void A_marked_class_that_cant_hold_a_component_is_refused(Type type)
    {
        Assert.Throws<InvalidOperationException>(() => new StoryblokComponentTypes([type]));
    }

    [Fact]
    public void The_same_class_named_twice_is_registered_once()
    {
        StoryblokComponentTypes types = new([typeof(Teaser), typeof(Teaser)]);

        Assert.True(types.TryGetType("teaser", out _));
    }

    [Fact]
    public void A_class_the_json_metadata_doesnt_know_is_refused()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new StoryblokComponentTypes([typeof(JsonKnownComponent), typeof(JsonUnknownComponent)], OnlyKnownComponentJson.Default));

        Assert.Contains(nameof(JsonUnknownComponent), error.Message);
    }

    [StoryblokComponent("teaser")]
    private sealed class Teaser : StoryblokComponent;

    [StoryblokComponent("teaser")]
    private sealed class SecondTeaser : StoryblokComponent;

    private sealed class Unmarked : StoryblokComponent;

    [StoryblokComponent("not_a_component")]
    private sealed class NotAComponent;

    [StoryblokComponent("abstract")]
    private abstract class AbstractComponent : StoryblokComponent;
}

// Source-generated JSON metadata must reach the types it describes, so these can't be private nested classes.
[StoryblokComponent("json_known")]
public sealed class JsonKnownComponent : StoryblokComponent;

[StoryblokComponent("json_unknown")]
public sealed class JsonUnknownComponent : StoryblokComponent;

[JsonSerializable(typeof(JsonKnownComponent))]
internal sealed partial class OnlyKnownComponentJson : JsonSerializerContext;
