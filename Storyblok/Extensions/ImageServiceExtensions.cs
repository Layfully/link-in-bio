using System.Text.RegularExpressions;

namespace Storyblok.Extensions;

public interface IImageService
{
    string? Url { get; }
}

public static partial class ImageServiceExtensions
{
    // please note that SVG is not supported in ImageService
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp"];

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

        Match match = AssetSize().Match(image.Url);
        return match.Success && int.TryParse(match.Groups[1].Value, out int width) && int.TryParse(match.Groups[2].Value, out int height) && width > 0 && height > 0
            ? (width, height)
            : null;
    }

    [GeneratedRegex(@"/f/\d+/(\d+)x(\d+)/")]
    private static partial Regex AssetSize();
}
