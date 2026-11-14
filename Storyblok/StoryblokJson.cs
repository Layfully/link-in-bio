using System.Text.Json.Serialization;

namespace Storyblok;

/// <summary>
/// Source-generated System.Text.Json metadata for the library's own types. Reading a story with it skips the reflection
/// System.Text.Json would otherwise do the first time, which is slow in WebAssembly's interpreter. The app's component
/// classes have their own (see <see cref="StoryblokComponentTypes.Json"/>).
/// </summary>
[JsonSerializable(typeof(StoryblokStoryContainer))]
[JsonSerializable(typeof(StoryblokAsset))]
internal sealed partial class StoryblokJson : JsonSerializerContext;
