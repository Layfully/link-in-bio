using Markdig;

namespace Storyblok;

public class Markdown
{
    public Markdown() { }

    public Markdown(string? value)
    {
        Value = value;
    }

    public string? Value { get; set; }

    public string Html
    {
        get
        {
            MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

            return Markdig.Markdown.ToHtml(Value ?? string.Empty, pipeline);
        }
    }
}
