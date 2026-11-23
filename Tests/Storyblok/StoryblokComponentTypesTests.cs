using Storyblok;
using Storyblok.Attributes;

namespace LinkInBio.Tests.Storyblok;

/// <summary>Tests component lookup and registration validation.</summary>
public sealed class StoryblokComponentTypesTests
{
    [Fact]
    public void TryGetType_ShouldFindTheClass_WhenTheComponentNameMatchesExactly()
    {
        // Arrange
        StoryblokComponentTypes sut = new([typeof(Teaser), typeof(Unmarked)]);

        // Act
        bool foundByName = sut.TryGetType("teaser", out Type? type);
        bool foundByOtherCase = sut.TryGetType("Teaser", out _);
        bool foundUnmarked = sut.TryGetType("unmarked", out _);

        // Assert
        foundByName.ShouldBeTrue();
        type.ShouldBe(typeof(Teaser));
        foundByOtherCase.ShouldBeFalse();
        foundUnmarked.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_ShouldRefuseTheClasses_WhenTwoOfThemShareOneComponent()
    {
        // Act
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => new StoryblokComponentTypes([typeof(Teaser), typeof(SecondTeaser)]));

        // Assert
        error.Message.ShouldContain("\"teaser\"", Case.Sensitive);
    }

    [Theory]
    [InlineData(typeof(NotAComponent))]
    [InlineData(typeof(AbstractComponent))]
    public void Constructor_ShouldThrow_WhenAMarkedClassIsNotAConcreteComponent(Type type)
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new StoryblokComponentTypes([type]));
    }

    [Fact]
    public void Constructor_ShouldRegisterTheClassOnce_WhenItIsNamedTwice()
    {
        // Act
        StoryblokComponentTypes sut = new([typeof(Teaser), typeof(Teaser)]);

        // Assert
        sut.TryGetType("teaser", out _).ShouldBeTrue();
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
