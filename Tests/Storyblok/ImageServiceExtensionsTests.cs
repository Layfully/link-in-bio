using Storyblok;
using Storyblok.Extensions;

namespace LinkInBio.Tests.Storyblok;

/// <summary>Addresses for Storyblok's image service, and the original size read from an asset's address.</summary>
public sealed class ImageServiceExtensionsTests
{
    private const string Photo = "https://a.storyblok.com/f/268473/608x912/b7730a2cf6/profile.jpg";

    [Theory]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.JPEG")]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.png")]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.webp")]
    public void Raster_images_are_resized_by_the_image_service(string url) =>
        Assert.Equal($"{url}/m/1280x0", new StoryblokAsset { Url = url }.ResizeByWidth(1280));

    [Theory]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/logo.svg")]
    [InlineData("https://a.storyblok.com/f/1/abc/cv.pdf")]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg?v=2")] // a query string: the filter can't simply go at the end
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg/m/300x0")] // already resized: a second filter would be invalid
    public void Other_files_are_left_as_they_are(string url) =>
        Assert.Equal(url, new StoryblokAsset { Url = url }.ResizeByWidth(1280));

    [Fact]
    public void Resize_crops_smartly_unless_told_otherwise()
    {
        StoryblokAsset photo = new() { Url = Photo };

        Assert.Equal($"{Photo}/m/600x600/smart", photo.Resize(600, 600));
        Assert.Equal($"{Photo}/m/256x384", photo.Resize(256, 384, smart: false));
    }

    [Theory]
    [InlineData(Photo, $"{Photo}/m/0x304:608x912/128x128")] // portrait: the bottom square
    [InlineData("https://a.storyblok.com/f/1/900x600/abc/x.jpg", "https://a.storyblok.com/f/1/900x600/abc/x.jpg/m/150x0:750x600/128x128")] // landscape: the middle
    [InlineData("https://a.storyblok.com/f/1/600x600/abc/x.jpg", "https://a.storyblok.com/f/1/600x600/abc/x.jpg/m/0x0:600x600/128x128")]
    public void A_square_is_cut_where_object_position_bottom_would_show_it(string url, string expected) =>
        Assert.Equal(expected, new StoryblokAsset { Url = url }.SquareFromBottom(128));

    [Fact]
    public void A_square_from_an_address_without_a_size_keeps_the_photos_shape()
    {
        // The page's CSS still cuts it, as it did before; only the download is larger.
        Assert.Equal("https://a.storyblok.com/f/1/abc/x.jpg/m/128x0", new StoryblokAsset { Url = "https://a.storyblok.com/f/1/abc/x.jpg" }.SquareFromBottom(128));
        Assert.Equal("https://a.storyblok.com/f/1/600x400/abc/logo.svg", new StoryblokAsset { Url = "https://a.storyblok.com/f/1/600x400/abc/logo.svg" }.SquareFromBottom(128));
    }

    [Fact]
    public void The_original_size_is_read_from_the_address() =>
        Assert.Equal((608, 912), new StoryblokAsset { Url = Photo }.Dimensions());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/photo.jpg")]
    [InlineData("https://a.storyblok.com/f/1/0x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/space/600x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x400")]
    public void An_address_of_another_shape_has_no_size(string? url) => Assert.Null(new StoryblokAsset { Url = url }.Dimensions());
}
