namespace Storyblok.Attributes;

/// <summary>Maps a Storyblok component's technical name to a C# class.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StoryblokComponentAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
