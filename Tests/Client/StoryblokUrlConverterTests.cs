using Client.Data.Components;
using System.Text.Json;

namespace LinkInBio.Tests.Client;

/// <summary>Tests URL conversion through the app's <see cref="StoryblokLink"/> model.</summary>
public sealed class StoryblokUrlConverterTests
{
    [Theory]
    [InlineData("""{ "linktype": "url", "url": "https://github.com/x" }""", "https://github.com/x")]
    [InlineData("""{ "linktype": "url", "url": "  http://example.com/a  " }""", "http://example.com/a")]
    [InlineData("""{ "linktype": "url", "url": "https://example.com", "anchor": "top" }""", "https://example.com#top")]
    [InlineData("""{ "linktype": "url", "url": "https://example.com", "anchor": "#top" }""", "https://example.com#top")]
    [InlineData("""{ "linktype": "story", "cached_url": "home" }""", "/")]
    [InlineData("""{ "linktype": "story", "cached_url": "/home/" }""", "/")]
    [InlineData("""{ "linktype": "story", "cached_url": "certificates/aws-architect" }""", "/Certificate/aws-architect")]
    [InlineData("""{ "linktype": "story", "cached_url": "about", "anchor": "contact" }""", "/about#contact")]
    [InlineData("""{ "linktype": "email", "email": "me@example.com" }""", "mailto:me@example.com")]
    [InlineData("""{ "linktype": "email", "email": "mailto:me@example.com", "anchor": "x" }""", "mailto:me@example.com")]
    [InlineData("""{ "linktype": "asset", "url": "https://a.storyblok.com/f/1/cv.pdf" }""", "https://a.storyblok.com/f/1/cv.pdf")]
    [InlineData("""{ "linktype": "something_new", "url": "https://example.com" }""", "https://example.com")]
    [InlineData("\"https://example.com/plain\"", "https://example.com/plain")]
    [InlineData("\"/Certificate/x\"", "/Certificate/x")]
    [InlineData("\"#section\"", "#section")]
    [InlineData("""{ "linktype": "url", "url": "https://example.com/a\n/b" }""", "https://example.com/a/b")]
    public void Url_ShouldBeTheExpectedAddress_WhenTheLinkFieldIsConverted(string field, string expected)
    {
        // Act
        string? result = Read(field);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("""{ "linktype": "url", "url": "javascript:alert(1)" }""")]
    [InlineData("""{ "linktype": "url", "url": " JavaScript:alert(1)" }""")]
    [InlineData("""{ "linktype": "url", "url": "data:text/html,<script>alert(1)</script>" }""")]
    [InlineData("""{ "linktype": "url", "url": "vbscript:x" }""")]
    [InlineData("""{ "linktype": "url", "url": "file:///etc/passwd" }""")]
    [InlineData("\"javascript:alert(1)\"")]
    public void Url_ShouldBeNull_WhenTheSchemeIsUnsupported(string field)
    {
        // Act
        string? result = Read(field);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{ "linktype": "url", "url": "//evil.example" }""")]
    [InlineData("""{ "linktype": "url", "url": "/\\evil.example" }""")]
    [InlineData("\"//evil.example\"")]
    [InlineData("""{ "linktype": "url", "url": "/\t/evil.example" }""")]
    [InlineData("""{ "linktype": "url", "url": "/\n/evil.example" }""")]
    [InlineData("""{ "linktype": "url", "url": "/\r\n\\evil.example" }""")]
    [InlineData("\"/\\t/evil.example\"")]
    public void Url_ShouldBeNull_WhenTheAddressOnlyLooksSiteRelative(string field)
    {
        // Act
        string? result = Read(field);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{ "linktype": "url", "url": "" }""")]
    [InlineData("""{ "linktype": "url", "url": "   " }""")]
    [InlineData("""{ "linktype": "story" }""")]
    [InlineData("""{ "linktype": "email", "email": "" }""")]
    [InlineData("""{ "linktype": "url", "url": 42 }""")]
    [InlineData("\"\"")]
    [InlineData("42")]
    [InlineData("[]")]
    public void Url_ShouldBeNull_WhenTheFieldIsEmptyOrUnexpected(string field)
    {
        // Act
        string? result = Read(field);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ShouldKeepTheRestOfTheEntry_WhenTheLinkFieldIsUnexpected()
    {
        // Arrange
        const string Json = """{ "label": "GitHub", "url": { "nested": { "deep": [1, 2] } }, "css_class": "fab" }""";

        // Act
        StoryblokLink? sut = JsonSerializer.Deserialize<StoryblokLink>(Json);

        // Assert
        sut.ShouldNotBeNull();
        sut.Label.ShouldBe("GitHub");
        sut.Url.ShouldBeNull();
        sut.CssClass.ShouldBe("fab");
    }

    private static string? Read(string field) =>
        JsonSerializer.Deserialize<StoryblokLink>($$"""{ "label": "Link", "url": {{field}} }""")?.Url;
}
