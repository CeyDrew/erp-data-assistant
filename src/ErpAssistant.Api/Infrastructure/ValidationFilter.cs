using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpAssistant.Api.Infrastructure;

/// <summary>
/// Action argümanlarından kayıtlı bir <see cref="IValidator{T}"/> olanları doğrular; hata varsa
/// action çalışmadan 400 ValidationProblem döner.
/// </summary>
internal sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Title = "Geçersiz istek",
                Status = StatusCodes.Status400BadRequest,
            });
            return;
        }

        await next();
    }
}
