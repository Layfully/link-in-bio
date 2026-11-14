using Client.Data.Components;
using System.Text.Json;

namespace LinkInBio.Tests.Client;

/// <summary>
/// How a Storyblok link field becomes the address a link on the site points to. Read through <see cref="StoryblokLink"/>,
/// which is where the app uses the converter.
/// </summary>
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
    public void Links_become_the_address_to_link_to(string field, string expected)
    {
        Assert.Equal(expected, Read(field));
    }

    [Theory]
    [InlineData("""{ "linktype": "url", "url": "javascript:alert(1)" }""")]
    [InlineData("""{ "linktype": "url", "url": " JavaScript:alert(1)" }""")]
    [InlineData("""{ "linktype": "url", "url": "data:text/html,<script>alert(1)</script>" }""")]
    [InlineData("""{ "linktype": "url", "url": "vbscript:x" }""")]
    [InlineData("""{ "linktype": "url", "url": "file:///etc/passwd" }""")]
    [InlineData("\"javascript:alert(1)\"")]
    public void Addresses_that_could_run_code_are_dropped(string field)
    {
        Assert.Null(Read(field));
    }

    [Theory]
    [InlineData("""{ "linktype": "url", "url": "//evil.example" }""")]
    [InlineData("""{ "linktype": "url", "url": "/\\evil.example" }""")]
    [InlineData("\"//evil.example\"")]
    public void Addresses_that_only_look_site_relative_are_dropped(string field)
    {
        Assert.Null(Read(field));
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
    public void Empty_or_unexpected_fields_have_no_address(string field)
    {
        Assert.Null(Read(field));
    }

    [Fact]
    public void An_unexpected_link_field_doesnt_fail_the_rest_of_the_entry()
    {
        StoryblokLink? link = JsonSerializer.Deserialize<StoryblokLink>("""{ "label": "GitHub", "url": { "nested": { "deep": [1, 2] } }, "css_class": "fab" }""");

        Assert.Equal("GitHub", link?.Label);
        Assert.Null(link?.Url);
        Assert.Equal("fab", link?.CssClass);
    }

    private static string? Read(string field) =>
        JsonSerializer.Deserialize<StoryblokLink>($$"""{ "label": "Link", "url": {{field}} }""")?.Url;
}
