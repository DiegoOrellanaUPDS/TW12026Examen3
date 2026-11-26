using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Examen_3;

public sealed class RequiereSessionAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var provider = session.GetString("AuthProvider");
        var token = session.GetString("NotionAccessToken");

        if (string.IsNullOrEmpty(token) || !string.Equals(provider, "Notion", StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Acceso denegado: se requiere una sesión activa autenticada con Notion."
            });
        }
    }
}
