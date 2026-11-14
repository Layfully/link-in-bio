using Storyblok.Client;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storyblok;

/// <summary>
/// Storyblok's responses for stories that were loaded when the site was built, so a page can show them before the app
/// has asked Storyblok anything. The deploy records them while it prerenders a page (<see cref="IsRecording"/>, then
/// <see cref="ToJson"/>) and embeds them in that page; the app reads them back with <see cref="AddAll"/> on start.
/// </summary>
/// <remarks>
/// Each response is used for the first load of its story only. Later loads ask Storyblok (or the cache) as usual, and
/// <see cref="StoryblokStoryQuery.TryRefresh{T}"/> tells whether the story has changed since it was recorded.
/// Keys are story paths as the client sends them (<c>home</c>, <c>certificates/aws</c>).
/// </remarks>
public sealed class PrerenderedStories
{
    /// <summary>The id of the <c>&lt;script type="application/json"&gt;</c> element the responses are embedded in.</summary>
    public const string ElementId = "storyblok-prerendered";

    private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _recorded = new(StringComparer.Ordinal);

    /// <summary>While true, the client keeps every response it receives from Storyblok in <see cref="Recorded"/>.</summary>
    public bool IsRecording { get; set; }

    public IReadOnlyDictionary<string, string> Recorded => _recorded;

    /// <summary>
    /// Reads responses embedded by the deploy: <c>{ "home": { "story": … }, … }</c>. Anything else is ignored, so a
    /// damaged page loads its content from Storyblok like any other.
    /// </summary>
    public void AddAll(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty response in document.RootElement.EnumerateObject())
            {
                if (response.Value.ValueKind == JsonValueKind.Object)
                {
                    _responses[response.Name] = response.Value.GetRawText();
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    /// <summary>
    /// The recorded responses in the shape <see cref="AddAll"/> reads. Safe inside a <c>&lt;script&gt;</c> element: the
    /// default encoder writes &lt;, &gt; and &amp; as \u escapes, so no text from Storyblok can close the element or open
    /// an HTML comment.
    /// </summary>
    public string ToJson()
    {
        JsonObject responses = [];
        foreach ((string path, string response) in _recorded)
        {
            responses[path] = JsonNode.Parse(response);
        }

        return responses.ToJsonString();
    }

    /// <summary>True when the page came with this story's response (<c>"/home"</c>, as callers pass slugs).</summary>
    public bool Contains(string slug) => StoryblokStoryClient.ToStoryPath(slug) is { } path && _responses.ContainsKey(path);

    /// <summary>The response for the first load of the story; false for every later one.</summary>
    internal bool TryTake(string path, out string response) =>
        _responses.TryGetValue(path, out response!) && _used.Add(path);

    internal bool TryGetResponse(string path, out string response) => _responses.TryGetValue(path, out response!);

    internal void Record(string path, string response)
    {
        if (IsRecording)
        {
            _recorded[path] = response;
        }
    }
}
