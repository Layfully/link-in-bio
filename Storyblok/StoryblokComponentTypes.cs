using Storyblok.Attributes;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Storyblok;

/// <summary>
/// The classes Storyblok components are read into, by technical component name (<c>[StoryblokComponent("page")]</c>).
/// Built once from the assemblies the app names, rather than by scanning every loaded assembly, framework included.
/// </summary>
public sealed class StoryblokComponentTypes
{
    private readonly FrozenDictionary<string, Type> _types;

    /// <param name="assemblies">Where the component classes are.</param>
    /// <param name="json">
    /// Source-generated System.Text.Json metadata for those classes (a JsonSerializerContext with [JsonSerializable] for each).
    /// With it, stories are read without reflection, which is slow in WebAssembly's interpreter. Null reads by reflection.
    /// </param>
    public StoryblokComponentTypes(IEnumerable<Assembly> assemblies, IJsonTypeInfoResolver? json = null)
        : this(assemblies.Distinct().SelectMany(assembly => assembly.GetTypes()), json)
    {
    }

    /// <summary>Only the given classes, for tests: an assembly holds every test's classes, deliberately broken ones included.</summary>
    internal StoryblokComponentTypes(IEnumerable<Type> candidates, IJsonTypeInfoResolver? json = null)
    {
        Json = json;
        JsonSerializerOptions options = new();
        Dictionary<string, Type> types = new(StringComparer.Ordinal);

        foreach (Type type in candidates.Distinct())
        {
            if (type.GetCustomAttribute<StoryblokComponentAttribute>(inherit: false) is not { } attribute)
            {
                continue;
            }

            if (type.IsAbstract || !type.IsAssignableTo(typeof(StoryblokComponent)))
            {
                throw new InvalidOperationException($"{type} is marked [StoryblokComponent] but isn't a concrete {nameof(StoryblokComponent)}.");
            }

            if (!types.TryAdd(attribute.Name, type))
            {
                throw new InvalidOperationException($"The Storyblok component \"{attribute.Name}\" is mapped to both {types[attribute.Name]} and {type}.");
            }

            // Checked here, at startup, rather than when a story first contains the component.
            if (json is not null && json.GetTypeInfo(type, options) is null)
            {
                throw new InvalidOperationException($"{type} is marked [StoryblokComponent] but its JSON metadata is missing: add [JsonSerializable(typeof({type.Name}))] to the app's JsonSerializerContext.");
            }
        }

        _types = types.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The source-generated JSON metadata for the component classes, or null to read them by reflection.</summary>
    public IJsonTypeInfoResolver? Json { get; }

    public bool TryGetType(string componentName, [NotNullWhen(true)] out Type? type) => _types.TryGetValue(componentName, out type);
}
