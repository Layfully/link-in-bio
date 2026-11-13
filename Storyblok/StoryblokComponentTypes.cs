using Storyblok.Attributes;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Storyblok;

/// <summary>
/// The classes Storyblok components are read into, by technical component name (<c>[StoryblokComponent("page")]</c>).
/// Built once from the assemblies the app names, rather than by scanning every loaded assembly, framework included.
/// </summary>
public sealed class StoryblokComponentTypes
{
    private readonly FrozenDictionary<string, Type> _types;

    public StoryblokComponentTypes(IEnumerable<Assembly> assemblies)
    {
        Dictionary<string, Type> types = new(StringComparer.Ordinal);

        foreach (Type type in assemblies.Distinct().SelectMany(assembly => assembly.GetTypes()))
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
        }

        _types = types.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public bool TryGetType(string componentName, [NotNullWhen(true)] out Type? type) => _types.TryGetValue(componentName, out type);
}
