using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net;
using System.Net.Http.Headers;

namespace Api;

public static class DeployTriggerClientExtensions
{
    public static IHttpClientBuilder AddDeployTriggerClient(this IServiceCollection services)
    {
        IHttpClientBuilder client = services.AddHttpClient<DeployTrigger>(http =>
        {
            http.BaseAddress = new Uri("https://api.github.com/");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("link-in-bio-deploy-trigger", "1.0"));
            http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            http.Timeout = TimeSpan.FromSeconds(35);
        });

        client.AddResilienceHandler("github-dispatch", (pipeline, context) =>
        {
            pipeline.TimeProvider = context.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
            // Retrying a POST can duplicate a dispatch if GitHub accepted it before the connection failed.
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                ShouldHandle = args => ValueTask.FromResult(
                    !args.Context.CancellationToken.IsCancellationRequested &&
                    args.Outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests &&
                    args.Outcome.Result?.Headers.RetryAfter is null &&
                    HttpClientResiliencePredicates.IsTransient(args.Outcome)),
            }).AddTimeout(TimeSpan.FromSeconds(10));
        });
        return client;
    }
}
