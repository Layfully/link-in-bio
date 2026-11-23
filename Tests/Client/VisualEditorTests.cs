using Client;

namespace LinkInBio.Tests.Client;

/// <summary>Tests which URLs activate Visual Editor mode.</summary>
public sealed class VisualEditorTests
{
    [Theory]
    [InlineData("https://site.example/?_storyblok", true)]
    [InlineData("https://site.example/?_storyblok=12345&_storyblok_c=page&_storyblok_tk[token]=x", true)]
    [InlineData("https://site.example/Certificate/aws?lang=en&_storyblok=1", true)]
    [InlineData("https://site.example/", false)]
    [InlineData("https://site.example/?storyblok=1", false)]
    [InlineData("https://site.example/?_storyblok_tk[token]=x", false)]
    [InlineData("https://site.example/?_storyblokx=1", false)]
    [InlineData("https://site.example/?q=_storyblok", false)]
    [InlineData("https://site.example/#_storyblok", false)]
    public void HasOpened_ShouldOnlyBeTrue_WhenTheExactEditorQueryParameterIsPresent(string address, bool expected)
    {
        // Arrange
        Uri uri = new(address);

        // Act
        bool result = VisualEditor.HasOpened(uri);

        // Assert
        result.ShouldBe(expected);
    }
}
