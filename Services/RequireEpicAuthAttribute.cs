using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Examen_3.Services;

/// <summary>
/// Requiere una sesión autenticada con Epic Games para ejecutar la acción.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireEpicAuthAttribute : Attribute, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var (isAuthenticated, _) = EpicSessionHelper.GetStatus(context.HttpContext.Session);
        if (!isAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Title = "No autenticado",
                Detail = "Debes iniciar sesión con Epic Games para acceder a este recurso.",
                Status = StatusCodes.Status401Unauthorized
            });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
