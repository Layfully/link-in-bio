using Storyblok;
using Storyblok.Attributes;

namespace LinkInBio.Tests.Storyblok;

/// <summary>Tests component lookup and registration validation.</summary>
public sealed class StoryblokComponentTypesTests
{
    [Fact]
    public void Classes_are_found_by_their_component_name()
    {
        StoryblokComponentTypes types = new([typeof(Teaser), typeof(Unmarked)]);

        Assert.True(types.TryGetType("teaser", out Type? type));
        Assert.Equal(typeof(Teaser), type);
        Assert.False(types.TryGetType("Teaser", out _));
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
    public void A_marked_class_must_be_a_concrete_component(Type type) =>
        Assert.Throws<InvalidOperationException>(() => new StoryblokComponentTypes([type]));

    [Fact]
    public void The_same_class_named_twice_is_registered_once()
    {
        StoryblokComponentTypes types = new([typeof(Teaser), typeof(Teaser)]);

        Assert.True(types.TryGetType("teaser", out _));
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
