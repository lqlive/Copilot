namespace Copilot.Filters;

internal sealed class WebhooksEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        return await next(context);
    }
}
