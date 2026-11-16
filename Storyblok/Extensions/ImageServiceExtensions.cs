using System.Globalization;

namespace Storyblok.Extensions;

public interface IImageService
{
    string? Url { get; }
}

public static class ImageServiceExtensions
{
    // The formats Storyblok's image service can resize. It can't resize SVG, so those addresses stay as they are.
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    public static string? ResizeByWidth(this IImageService image, int newWidth)
    {
        return image.IsSupported()
            ? image.Url + $"/m/{newWidth}x0"
            : image.Url;
    }

    public static string? Resize(this IImageService image, int newWidth, int newHeight, bool smart = true)
    {
        return image.IsSupported()
            ? image.Url + $"/m/{newWidth}x{newHeight}{(smart ? "/smart" : "")}"
            : image.Url;
    }

    public static bool IsSupported(this IImageService image)
    {
        if (string.IsNullOrWhiteSpace(image.Url))
        {
            return false;
        }

        return ImageExtensions.Any(x => image.Url.EndsWith(x, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The original pixel size, read from the asset address: Storyblok stores uploads as
    /// <c>https://a.storyblok.com/f/{space}/{width}x{height}/{hash}/{name}</c>. Null when the address has another shape.
    /// </summary>
    public static (int Width, int Height)? Dimensions(this IImageService image)
    {
        if (string.IsNullOrWhiteSpace(image.Url))
        {
            return null;
        }

        // Read by hand rather than with a regular expression: System.Text.RegularExpressions would be one more assembly for
        // every visitor to download, for this one address.
        int files = image.Url.IndexOf("/f/", StringComparison.Ordinal);
        string[] parts = files < 0 ? [] : image.Url[(files + "/f/".Length)..].Split('/');
        if (parts.Length < 3 || !IsNumber(parts[0]) || parts[1].Split('x') is not [string width, string height] || !IsNumber(width) || !IsNumber(height))
        {
            return null;
        }

        (int Width, int Height) size = (int.Parse(width, CultureInfo.InvariantCulture), int.Parse(height, CultureInfo.InvariantCulture));
        return size is { Width: > 0, Height: > 0 } ? size : null;
    }

    // Digits only, and few enough to fit an int.
    private static bool IsNumber(string text) => text.Length is > 0 and < 10 && text.All(char.IsAsciiDigit);
}
