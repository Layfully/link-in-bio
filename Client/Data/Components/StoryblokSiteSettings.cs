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

    [JsonPropertyName("canonical_url")]
    public string? CanonicalUrl { get; set; }

    [JsonPropertyName("analytics_measurement_id")]
    public string? AnalyticsMeasurementId { get; set; }

    [JsonPropertyName("analytics_hosts")]
    public string? AnalyticsHosts { get; set; }

    [JsonPropertyName("app_name")]
    public string? AppName { get; set; }

    [JsonPropertyName("app_icon")]
    public StoryblokAsset? AppIcon { get; set; }

    [JsonPropertyName("app_maskable_icon")]
    public StoryblokAsset? AppMaskableIcon { get; set; }

    public string? CanonicalOrigin => Site.ParseOrigin(CanonicalUrl);

    public string? PageTitle => (Name, JobTitle) switch
    {
        ({ } name, { } title) => $"{name} - {title}",
        var (name, title) => name ?? title,
    };

    public string? Name => Clean(DisplayName);

    public string? JobTitle => Clean(ProfessionalTitle);

    public string? Description => Clean(SeoDescription);

    public string? InstallName => Clean(AppName);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
