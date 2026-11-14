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
    public void Raster_images_are_resized_by_the_image_service(string url)
    {
        Assert.Equal($"{url}/m/1280x0", new StoryblokAsset { Url = url }.ResizeByWidth(1280));
    }

    [Theory]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/logo.svg")]
    [InlineData("https://a.storyblok.com/f/1/abc/cv.pdf")]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg?v=2")] // a query string: the filter can't simply go at the end
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg/m/300x0")] // already resized: a second filter would be invalid
    public void Other_files_are_left_as_they_are(string url)
    {
        Assert.Equal(url, new StoryblokAsset { Url = url }.ResizeByWidth(1280));
    }

    [Fact]
    public void Resize_crops_smartly_unless_told_otherwise()
    {
        StoryblokAsset photo = new() { Url = Photo };

        Assert.Equal($"{Photo}/m/600x600/smart", photo.Resize(600, 600));
        Assert.Equal($"{Photo}/m/256x384", photo.Resize(256, 384, smart: false));
    }

    [Fact]
    public void The_original_size_is_read_from_the_address()
    {
        Assert.Equal((608, 912), new StoryblokAsset { Url = Photo }.Dimensions());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/photo.jpg")]
    [InlineData("https://a.storyblok.com/f/1/0x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/space/600x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x400")]
    public void An_address_of_another_shape_has_no_size(string? url)
    {
        Assert.Null(new StoryblokAsset { Url = url }.Dimensions());
    }
}
