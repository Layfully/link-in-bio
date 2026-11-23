using Prerender;

namespace LinkInBio.Tests.Prerender;

/// <summary>How a failure reads in the deploy's warning.</summary>
public sealed class ExceptionTextTests
{
    [Fact]
    public void Describe_ShouldSayWhatFailedAndWhy_WhenTheExceptionHasAnInnerException()
    {
        // Arrange
        InvalidOperationException exception = new("The home page failed while rendering.",
            new FormatException("The input string was not in a correct format."));

        // Act
        string text = ExceptionText.Describe(exception);

        // Assert
        text.ShouldBe("The home page failed while rendering: The input string was not in a correct format");
    }

    [Fact]
    public void Describe_ShouldWriteARepeatedMessageOnce_WhenTheOuterMessageRepeatsTheInnerOne()
    {
        // Arrange
        const string message = "Storyblok didn't answer within 15 seconds";
        TimeoutException inner = new(message + ".");
        TimeoutException outer = new(inner.Message, inner);

        // Act
        string text = ExceptionText.Describe(outer);

        // Assert
        text.ShouldBe(message);
    }
}
