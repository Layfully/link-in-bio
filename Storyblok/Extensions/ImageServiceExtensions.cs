using System.Globalization;

namespace Storyblok.Extensions;

public interface IImageService
{
    string? Url { get; }
}

public static class ImageServiceExtensions
{
    // Raster formats supported by Storyblok resizing.
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    public static string? ResizeByWidth(this IImageService image, int newWidth) =>
        image.Resize(newWidth, 0, smart: false);

    public static string? Resize(this IImageService image, int newWidth, int newHeight, bool smart = true) =>
        image.IsSupported()
            ? image.Url + $"/m/{newWidth}x{newHeight}{(smart ? "/smart" : "")}"
            : image.Url;

    /// <summary>
    /// Crops a <paramref name="side"/> px square from the bottom center. Falls back to width-only resizing when dimensions are unknown.
    /// </summary>
    public static string? SquareFromBottom(this IImageService image, int side)
    {
        if (!image.IsSupported())
        {
            return image.Url;
        }

        if (image.Dimensions() is not (int width, int height))
        {
            return image.ResizeByWidth(side);
        }

        int crop = Math.Min(width, height);
        int left = (width - crop) / 2;
        int top = height - crop;
        return image.Url + $"/m/{left}x{top}:{left + crop}x{height}/{side}x{side}";
    }

    public static bool IsSupported(this IImageService image)
    {
        string? url = image.Url;
        return !string.IsNullOrWhiteSpace(url) && ImageExtensions.Any(extension => url.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The original pixel size, read from the asset address: Storyblok stores uploads as
    /// <c>https://a.storyblok.com/f/{space}/{width}x{height}/{hash}/{name}</c>. Null when the address has another shape.
    /// </summary>
    public static (int Width, int Height)? Dimensions(this IImageService image)
    {
        string? url = image.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        int files = url.IndexOf("/f/", StringComparison.Ordinal);
        string[] parts = files < 0 ? [] : url[(files + "/f/".Length)..].Split('/');
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
