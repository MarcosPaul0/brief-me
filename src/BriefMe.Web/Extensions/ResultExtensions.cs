using BriefMe.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BriefMe.Web.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToProblem(this Result result, ControllerBase controller, string title)
    {
        if (result.Errors.Count > 0)
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in result.Errors)
                modelState.AddModelError(error.PropertyName, error.ErrorMessage);

            return controller.ValidationProblem(modelState);
        }

        var statusCode = result.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return controller.Problem(detail: result.Error, statusCode: statusCode, title: title);
    }
}
