using Storyblok;
using Storyblok.Extensions;

namespace LinkInBio.Tests.Storyblok;

/// <summary>How a block's <c>_editable</c> value becomes the attributes the Visual Editor's bridge looks for.</summary>
public sealed class VisualEditorExtensionsTests
{
    [Fact]
    public void A_blocks_editable_value_becomes_the_bridges_attributes()
    {
        const string options = """{"name":"link","space":"1","uid":"abc","id":"42"}""";

        IReadOnlyDictionary<string, object>? attributes = Block($"<!--#storyblok#{options}-->").EditorAttributes();

        Assert.NotNull(attributes);
        Assert.Equal(options, attributes["data-blok-c"]);
        Assert.Equal("42-abc", attributes["data-blok-uid"]);
    }

    [Fact]
    public void A_numeric_id_works_like_a_string_one()
    {
        IReadOnlyDictionary<string, object>? attributes = Block("""<!--#storyblok#{"uid":"abc","id":42}-->""").EditorAttributes();

        Assert.Equal("42-abc", attributes?["data-blok-uid"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<!--#storyblok#{ broken-->")]
    [InlineData("""{"uid":"abc","id":"42"}""")]
    [InlineData("""<!--#storyblok#{"uid":"abc"}-->""")]
    [InlineData("""<!--#storyblok#["abc"]-->""")]
    public void A_missing_or_malformed_value_gives_no_attributes(string? editable)
    {
        Assert.Null(Block(editable).EditorAttributes());
    }

    [Fact]
    public void No_block_gives_no_attributes()
    {
        Assert.Null(((StoryblokComponent?)null).EditorAttributes());
    }

    private static StoryblokComponent Block(string? editable) => new() { Editable = editable };
}
