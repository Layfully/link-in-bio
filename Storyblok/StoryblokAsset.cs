using Storyblok.Extensions;
using System.Text.Json.Serialization;

namespace Storyblok;

/// <summary>An uploaded file from an asset field. Only the fields the site uses are read.</summary>
public sealed class StoryblokAsset : IImageService
{
    [JsonPropertyName("filename")]
    public string? Url { get; set; }

    [JsonPropertyName("alt")]
    public string? Alt { get; set; }
}
