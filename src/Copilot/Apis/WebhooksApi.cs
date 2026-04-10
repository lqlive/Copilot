using System.Text.Json;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Copilot.Apis;

public static class WebhooksApi
{
    public static IEndpointRouteBuilder MapWebhooks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/webhooks");
        group.MapPost("/{provider}", HandleAsync);
        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string provider,
        [FromHeader(Name = "X-Gitlab-Event")] string? gitlabEvent,
        [FromBody] JsonElement payload, 
        IWebhookParser parser,
        ITaskQueue queue,
        ITaskCancellationRegistry cancellationRegistry,
        CancellationToken cancellationToken)
    {
        var eventHeader = gitlabEvent ?? string.Empty;
        var copilotEvent = parser.Parse(eventHeader, payload);
        if (copilotEvent is null)
            return TypedResults.Ok();

        if (copilotEvent.Type == CopilotEventType.IssueUnassigned)
        {
            await cancellationRegistry.CancelAsync(copilotEvent.SessionKey, cancellationToken);
            return TypedResults.Ok();
        }

        await queue.ProduceAsync(copilotEvent, cancellationToken);
        return TypedResults.Accepted(string.Empty);
    }
}