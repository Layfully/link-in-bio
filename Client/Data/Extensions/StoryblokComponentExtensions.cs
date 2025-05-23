﻿using Client.Data.Components;
using Storyblok;

namespace Client.Data.Extensions;

public static class StoryblokComponentExtensions
{
    public static List<T> ToList<T>(this StoryblokComponent source) where T : StoryblokComponent
    {
        return (source as StoryblokList)?.Entries?.OfType<T>().ToList() ?? [];
    }
}