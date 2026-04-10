using System.Text.Json;
using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;

public interface IWebhookParser
{
    CopilotEvent? Parse(string eventHeader, JsonElement payload);
}
