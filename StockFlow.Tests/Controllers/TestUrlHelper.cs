using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace StockFlow.Tests.Controllers;

internal sealed class TestUrlHelper(string indexPath) : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();

    public string? Action(UrlActionContext actionContext)
    {
        return actionContext.Action == "Index" ? indexPath : null;
    }

    public string? Content(string? contentPath)
    {
        return contentPath?.StartsWith("~/", StringComparison.Ordinal) == true
            ? contentPath[1..]
            : contentPath;
    }

    public bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url[0] == '/')
        {
            return url.Length == 1 || url[1] is not ('/' or '\\');
        }

        return url.Length > 1
            && url[0] == '~'
            && url[1] == '/'
            && (url.Length == 2 || url[2] is not ('/' or '\\'));
    }

    public string? Link(string? routeName, object? values)
    {
        return null;
    }

    public string? RouteUrl(UrlRouteContext routeContext)
    {
        return null;
    }
}
