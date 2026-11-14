using Client;

namespace LinkInBio.Tests.Client;

/// <summary>
/// Which addresses count as opened by the Visual Editor. It decides whether the editor build loads drafts, so a near miss
/// must not count.
/// </summary>
public sealed class VisualEditorTests
{
    [Theory]
    [InlineData("https://site.example/?_storyblok")]
    [InlineData("https://site.example/?_storyblok=12345&_storyblok_c=page&_storyblok_tk[token]=x")]
    [InlineData("https://site.example/Certificate/aws?lang=en&_storyblok=1")]
    public void The_editors_parameter_counts(string address)
    {
        Assert.True(VisualEditor.HasOpened(new Uri(address)));
    }

    [Theory]
    [InlineData("https://site.example/")]
    [InlineData("https://site.example/?storyblok=1")]
    [InlineData("https://site.example/?_storyblok_tk[token]=x")]
    [InlineData("https://site.example/?_storyblokx=1")]
    [InlineData("https://site.example/?q=_storyblok")]
    [InlineData("https://site.example/#_storyblok")]
    public void Anything_else_doesnt(string address)
    {
        Assert.False(VisualEditor.HasOpened(new Uri(address)));
    }
}
