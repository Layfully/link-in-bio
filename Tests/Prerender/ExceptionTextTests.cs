using Prerender;

namespace LinkInBio.Tests.Prerender;

/// <summary>How a failure reads in the deploy's warning.</summary>
public sealed class ExceptionTextTests
{
    [Fact]
    public void The_warning_says_what_failed_and_why()
    {
        InvalidOperationException exception = new("The home page failed while rendering.",
            new FormatException("The input string was not in a correct format."));

        Assert.Equal("The home page failed while rendering: The input string was not in a correct format", ExceptionText.Describe(exception));
    }

    [Fact]
    public void A_message_repeated_inside_is_written_once()
    {
        TimeoutException timeout = new("Storyblok didn't answer within 15 seconds.");

        Assert.Equal("Storyblok didn't answer within 15 seconds", ExceptionText.Describe(new TimeoutException(timeout.Message, timeout)));
    }
}
