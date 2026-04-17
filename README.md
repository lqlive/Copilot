# Copilot

`Copilot` is an experimental service that brings GitHub Copilot capabilities to GitLab, enabling it to join issue and pull request conversations, review changes, modify code, and push updates back to the repository.

## What It Can Do

### Issue Assignment

When an issue is assigned to the configured Copilot account, the agent can:

- read the issue description as the implementation request
- clone the repository
- create a working branch
- update the codebase
- commit and push the changes
- open a pull request automatically

### Issue Discussion

When someone mentions the Copilot account in an issue comment, the agent can:

- answer technical questions
- compare implementation approaches
- recommend a solution
- ask focused clarification questions when the request is unclear

### Pull Request Feedback

When someone leaves a comment on a pull request, the agent can:

- interpret the requested changes
- update the code directly on the pull request source branch
- commit and push the changes back to that branch
- reply in the pull request thread

### Pull Request Review

The agent can also run review-style workflows for pull requests and post feedback focused on:

- obvious bugs
- security issues
- performance risks

## Typical Workflow

1. GitLab sends a webhook.
2. The API receives and parses the event.
3. The event is placed into a Redis-backed task queue.
4. The agent consumes the task and selects the correct command handler.
5. The handler either replies in discussion threads or updates code in a local workspace.
6. Results are posted back to GitLab.

## Projects

- `src/Copilot`
  - Webhook API host.

- `src/Copilot.Agent`
  - Background worker that executes Copilot tasks.

- `src/Copilot.Core`
  - Shared abstractions and infrastructure such as queueing, sessions, workspace management, and cancellation.

- `src/Copilot.Provider.GitLab`
  - GitLab-specific webhook parsing and provider client implementation.

## Configuration

The main configuration sections are:

- `RedisTaskQueue`
  - Redis connection and claim timeout settings.

- `GitLab`
  - GitLab base URL, access token, agent username, and webhook secret.

- `Copilot`
  - Model selection and response timeout.

- `CopilotAgent`
  - Worker concurrency settings.

- `Workspace`
  - Local temporary workspace location.

- `GitClient`
  - Git executable path, bot identity, and optional authenticated push settings.

## Running Locally

### Requirements

- .NET 10 SDK
- Redis
- GitLab
- Git
- GitHub Copilot SDK runtime access

### Start the Webhook API

```bash
dotnet run --project src/Copilot/Copilot.csproj
```

### Start the Agent

```bash
dotnet run --project src/Copilot.Agent/Copilot.Agent.csproj
```

## Current Scope

The current implementation focuses on GitLab and supports webhook-driven automation for:

- issue assignment
- issue comments
- pull request comments
- pull request review workflows

More provider integrations and workflow types can be added later through the existing abstraction layers.
