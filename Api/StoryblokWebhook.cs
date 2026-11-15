using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api;

/// <summary>
/// <c>POST /api/storyblok-webhook?secret=…</c>: the address Storyblok's webhook calls on a publish. Anonymous, because
/// Storyblok sends no credentials; the secret in the address is checked instead (<see cref="DeployTrigger"/>).
/// </summary>
public sealed class StoryblokWebhook(DeployTrigger trigger)
{
    [Function(nameof(StoryblokWebhook))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "storyblok-webhook")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        DeployTriggerResult result = await trigger.TriggerAsync(request.Query["secret"], cancellationToken);

        HttpResponseData response = request.CreateResponse(result.Status);
        await response.WriteStringAsync(result.Message, cancellationToken);
        return response;
    }
}
