using Storyblok;
using Storyblok.Extensions;

namespace LinkInBio.Tests.Storyblok;

/// <summary>How a block's <c>_editable</c> value becomes the attributes the Visual Editor's bridge looks for.</summary>
public sealed class VisualEditorExtensionsTests
{
    [Theory]
    [InlineData("""{"name":"link","space":"1","uid":"abc","id":"42"}""")]
    [InlineData("""{"uid":"abc","id":42}""")]
    public void EditorAttributes_ShouldProduceTheBridgesAttributes_WhenTheIdIsAStringOrANumber(string options)
    {
        // Arrange
        StoryblokComponent sut = Block($"<!--#storyblok#{options}-->");

        // Act
        IReadOnlyDictionary<string, object>? attributes = sut.EditorAttributes();

        // Assert
        attributes.ShouldNotBeNull();
        attributes["data-blok-c"].ShouldBe(options);
        attributes["data-blok-uid"].ShouldBe("42-abc");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<!--#storyblok#{ broken-->")]
    [InlineData("""{"uid":"abc","id":"42"}""")]
    [InlineData("""<!--#storyblok#{"uid":"abc"}-->""")]
    [InlineData("""<!--#storyblok#["abc"]-->""")]
    public void EditorAttributes_ShouldBeNull_WhenTheValueIsMissingOrMalformed(string? editable)
    {
        // Arrange
        StoryblokComponent sut = Block(editable);

        // Act
        IReadOnlyDictionary<string, object>? attributes = sut.EditorAttributes();

        // Assert
        attributes.ShouldBeNull();
    }

    [Fact]
    public void EditorAttributes_ShouldBeNull_WhenThereIsNoBlock()
    {
        // Arrange
        StoryblokComponent? sut = null;

        // Act
        IReadOnlyDictionary<string, object>? attributes = sut.EditorAttributes();

        // Assert
        attributes.ShouldBeNull();
    }

    private static StoryblokComponent Block(string? editable) => new() { Editable = editable };
}
