using GitHub.Copilot.SDK;
using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Agent.Commands;

internal sealed class IssueCommentCommandHandler : BaseCommandHandler
{
    private const string SystemPrompt = """
           You are a professional technical discussion assistant participating in Git issue conversations.
           Your job is to reply to issue comments in a clear, concise, and practical way.
           Follow these rules:
           - If the comment is discussing multiple approaches, compare the options and recommend the best one.
           - If the problem is under-specified, ask 1-3 focused clarification questions.
           - If the direction is already clear, provide a concrete recommendation with reasoning.
           - Prioritize maintainability, implementation complexity, extensibility, risk, and delivery cost.
           - Avoid vague or generic answers.
           - Keep the response suitable for posting directly in an issue comment.
           - Use Markdown format.
    """;
    public IssueCommentCommandHandler(
        ISessionStore sessionStore,
        CopilotClient copilotClient,
        IGitProviderClient gitProviderClient,
        IOptions<CopilotOptions> options,
        ILogger<IssueCommentCommandHandler> logger)
        : base(sessionStore, copilotClient, gitProviderClient, options, logger)
    {
    }
    public override async Task HandleAsync(CopilotTask task, CancellationToken ct = default)
    {
        var comment = task.Event.TriggerComment;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }
        var session = await GetOrCreateSessionAsync(task, ct);
        var prompt = $"""
            {SystemPrompt}
            Someone mentioned @copilot in this issue discussion. Please reply directly to the following comment.

            Comment:
            {comment}

            Please do the following:
            1. If this is a design discussion, compare the relevant options and recommend one.
            2. If the context is insufficient, ask the most important clarification questions.
            3. If the implementation direction is already clear, provide a concrete recommendation with reasons.
            4. Keep the reply concise and suitable for posting directly in the issue thread.
           """;
        var response = await InvokeAsync(session, prompt, task, cancellationToken: ct);

        await GitProviderClient.PostIssueCommentAsync(
            task.Event.RepositoryId,
            task.Event.SessionKey.ResourceId,
            response,
            ct);
    }
    
}