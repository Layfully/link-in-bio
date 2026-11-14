using System.Text.Json.Serialization;

namespace Client.Data.Components;

/// <summary>
/// Source-generated System.Text.Json metadata for the app's Storyblok components, so stories are read without reflection,
/// which is slow in WebAssembly's interpreter. Every class marked [StoryblokComponent] needs a line here; the app refuses
/// to start without it (StoryblokComponentTypes), and a test says so first.
/// </summary>
[JsonSerializable(typeof(StoryblokPage))]
[JsonSerializable(typeof(StoryblokHeader))]
[JsonSerializable(typeof(StoryblokList))]
[JsonSerializable(typeof(StoryblokLink))]
[JsonSerializable(typeof(StoryblokText))]
[JsonSerializable(typeof(StoryblokCertificate))]
public sealed partial class ComponentJson : JsonSerializerContext;
