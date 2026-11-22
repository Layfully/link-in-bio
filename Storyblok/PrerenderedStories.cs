using Storyblok.Client;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storyblok;

/// <summary>Records Storyblok responses during prerendering and loads them back at startup.</summary>
/// <remarks>
/// Each embedded response serves the first load. Refreshes can still compare against it later.
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

    /// <summary>Loads embedded response objects by story path, ignoring malformed data.</summary>
    public void AddAll(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement is not { ValueKind: JsonValueKind.Object } responses)
            {
                return;
            }

            foreach (JsonProperty response in responses.EnumerateObject())
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

    /// <summary>Serializes recorded responses for embedding in a script element using the default JSON encoder.</summary>
    public string ToJson()
    {
        JsonObject responses = new(_recorded.Select(entry => KeyValuePair.Create(entry.Key, JsonNode.Parse(entry.Value))));
        return responses.ToJsonString();
    }

    /// <summary>Checks whether the slug has an unused embedded response.</summary>
    public bool Contains(string slug) =>
        StoryblokStoryClient.ToStoryPath(slug) is { } path && _responses.ContainsKey(path) && !_used.Contains(path);

    /// <summary>The response for the first load of the story; false for every later one.</summary>
    internal bool TryTake(string path, out string response) =>
        TryGetResponse(path, out response) && _used.Add(path);

    internal bool TryGetResponse(string path, out string response) => _responses.TryGetValue(path, out response!);

    internal void Record(string path, string response)
    {
        if (IsRecording)
        {
            _recorded[path] = response;
        }
    }
}
