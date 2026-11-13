namespace Storyblok.Attributes;

/// <summary>Marks the class a Storyblok component is read into, by the component's technical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StoryblokComponentAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
