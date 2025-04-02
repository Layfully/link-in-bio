using Storyblok.Attributes;

namespace Storyblok;

public static class StoryblokMappings
{
    private static IDictionary<string, Mapping>? _mappingsCache; // make static, because it should be "valid" for as long as the app is running

    public static IDictionary<string, Mapping> Mappings
    {
        get
        {
            if (_mappingsCache is not null)
            {
                return _mappingsCache;
            }

            var components = from a in AppDomain.CurrentDomain.GetAssemblies()
                from t in a.GetTypes()
                let attributes = t.GetCustomAttributes(typeof(StoryblokComponentAttribute), true)
                where attributes is { Length: > 0 }
                select new { Type = t, Attribute = attributes.Cast<StoryblokComponentAttribute>().First() };

            Dictionary<string, Mapping> mappingsCache = new();

            foreach (var component in components)
            {
                if (mappingsCache.ContainsKey(component.Attribute.Name))
                {
                    continue;
                }

                mappingsCache[component.Attribute.Name] = new Mapping
                {
                    Type = component.Type,
                    ComponentName = component.Attribute.Name,
                    View = component.Attribute.View
                };
            }

            _mappingsCache = mappingsCache; // this makes sure we don't have concurrent operations on the dictionary while it's filling
            return mappingsCache;

        }
    }

    public class Mapping
    {
        public string ComponentName { get; set; } = string.Empty;
        public Type Type { get; set; } = typeof(object);
        public string? View { get; set; }
    }
}
