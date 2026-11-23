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
    public void ResizeByWidth_ShouldAppendTheResizeFilter_WhenTheImageIsRaster(string url)
    {
        // Arrange
        StoryblokAsset sut = new() { Url = url };

        // Act
        string? result = sut.ResizeByWidth(1280);

        // Assert
        result.ShouldBe($"{url}/m/1280x0");
    }

    [Theory]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/logo.svg")]
    [InlineData("https://a.storyblok.com/f/1/abc/cv.pdf")]
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg?v=2")] // a query string: the filter can't simply go at the end
    [InlineData("https://a.storyblok.com/f/1/600x400/abc/x.jpg/m/300x0")] // already resized: a second filter would be invalid
    public void ResizeByWidth_ShouldLeaveTheAddressAsItIs_WhenTheFileCantBeResized(string url)
    {
        // Arrange
        StoryblokAsset sut = new() { Url = url };

        // Act
        string? result = sut.ResizeByWidth(1280);

        // Assert
        result.ShouldBe(url);
    }

    [Fact]
    public void Resize_ShouldCropSmartly_WhenNotToldOtherwise()
    {
        // Arrange
        StoryblokAsset sut = new() { Url = Photo };

        // Act
        string? smart = sut.Resize(600, 600);
        string? plain = sut.Resize(256, 384, smart: false);

        // Assert
        smart.ShouldBe($"{Photo}/m/600x600/smart");
        plain.ShouldBe($"{Photo}/m/256x384");
    }

    [Theory]
    [InlineData(Photo, $"{Photo}/m/0x304:608x912/128x128")] // portrait: the bottom square
    [InlineData("https://a.storyblok.com/f/1/900x600/abc/x.jpg", "https://a.storyblok.com/f/1/900x600/abc/x.jpg/m/150x0:750x600/128x128")] // landscape: the middle
    [InlineData("https://a.storyblok.com/f/1/600x600/abc/x.jpg", "https://a.storyblok.com/f/1/600x600/abc/x.jpg/m/0x0:600x600/128x128")]
    public void SquareFromBottom_ShouldCutWhereObjectPositionBottomWouldShowIt_WhenTheAddressHasASize(string url, string expected)
    {
        // Arrange
        StoryblokAsset sut = new() { Url = url };

        // Act
        string? result = sut.SquareFromBottom(128);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SquareFromBottom_ShouldKeepThePhotosShape_WhenTheAddressHasNoSize()
    {
        // The page's CSS still cuts it, as it did before; only the download is larger.
        // Arrange
        StoryblokAsset sut = new() { Url = "https://a.storyblok.com/f/1/abc/x.jpg" };
        StoryblokAsset logo = new() { Url = "https://a.storyblok.com/f/1/600x400/abc/logo.svg" };

        // Act
        string? photoResult = sut.SquareFromBottom(128);
        string? logoResult = logo.SquareFromBottom(128);

        // Assert
        photoResult.ShouldBe("https://a.storyblok.com/f/1/abc/x.jpg/m/128x0");
        logoResult.ShouldBe("https://a.storyblok.com/f/1/600x400/abc/logo.svg");
    }

    [Fact]
    public void Dimensions_ShouldBeReadFromTheAddress_WhenTheAddressHasASize()
    {
        // Arrange
        StoryblokAsset sut = new() { Url = Photo };

        // Act
        (int Width, int Height)? result = sut.Dimensions();

        // Assert
        result.ShouldBe((608, 912));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/photo.jpg")]
    [InlineData("https://a.storyblok.com/f/1/0x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/space/600x400/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x/abc/x.jpg")]
    [InlineData("https://a.storyblok.com/f/1/600x400")]
    public void Dimensions_ShouldBeNull_WhenTheAddressIsOfAnotherShape(string? url)
    {
        // Arrange
        StoryblokAsset sut = new() { Url = url };

        // Act
        (int Width, int Height)? result = sut.Dimensions();

        // Assert
        result.ShouldBeNull();
    }
}
