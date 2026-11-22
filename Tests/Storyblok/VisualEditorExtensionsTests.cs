using Storyblok;
using Storyblok.Extensions;

namespace LinkInBio.Tests.Storyblok;

/// <summary>How a block's <c>_editable</c> value becomes the attributes the Visual Editor's bridge looks for.</summary>
public sealed class VisualEditorExtensionsTests
{
    [Theory]
    [InlineData("""{"name":"link","space":"1","uid":"abc","id":"42"}""")]
    [InlineData("""{"uid":"abc","id":42}""")]
    public void String_and_numeric_ids_produce_the_bridges_attributes(string options)
    {
        IReadOnlyDictionary<string, object>? attributes = Block($"<!--#storyblok#{options}-->").EditorAttributes();

        Assert.NotNull(attributes);
        Assert.Equal(options, attributes["data-blok-c"]);
        Assert.Equal("42-abc", attributes["data-blok-uid"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<!--#storyblok#{ broken-->")]
    [InlineData("""{"uid":"abc","id":"42"}""")]
    [InlineData("""<!--#storyblok#{"uid":"abc"}-->""")]
    [InlineData("""<!--#storyblok#["abc"]-->""")]
    public void A_missing_or_malformed_value_gives_no_attributes(string? editable) => Assert.Null(Block(editable).EditorAttributes());

    [Fact]
    public void No_block_gives_no_attributes() => Assert.Null(((StoryblokComponent?)null).EditorAttributes());

    private static StoryblokComponent Block(string? editable) => new() { Editable = editable };
}
