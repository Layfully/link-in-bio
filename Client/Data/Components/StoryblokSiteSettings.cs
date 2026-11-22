using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("site_settings")]
public sealed class StoryblokSiteSettings : StoryblokComponent
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("professional_title")]
    public string? ProfessionalTitle { get; set; }

    [JsonPropertyName("seo_description")]
    public string? SeoDescription { get; set; }

    [JsonPropertyName("social_image")]
    public StoryblokAsset? SocialImage { get; set; }

    public string? PageTitle => (Name, JobTitle) switch
    {
        ({ } name, { } title) => $"{name} - {title}",
        var (name, title) => name ?? title,
    };

    public string? Name => Clean(DisplayName);

    public string? JobTitle => Clean(ProfessionalTitle);

    public string? Description => Clean(SeoDescription);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
