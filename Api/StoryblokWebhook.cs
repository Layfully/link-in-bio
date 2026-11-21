using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api;

/// <summary>
/// Receives Storyblok publish events. <see cref="DeployTrigger"/> validates the shared secret from the query string.
/// </summary>
public sealed class StoryblokWebhook(DeployTrigger trigger)
{
    [Function(nameof(StoryblokWebhook))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "storyblok-webhook")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        DeployTriggerResult result = await trigger.TriggerFromRequestAsync(request.Query["secret"], ReadBodyAsync, cancellationToken);

        HttpResponseData response = request.CreateResponse(result.Status);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync(result.Message, cancellationToken);
        return response;

        async Task<string?> ReadBodyAsync(CancellationToken token)
        {
            using StreamReader reader = new(request.Body, leaveOpen: true);
            return await reader.ReadToEndAsync(token);
        }
    }
}
