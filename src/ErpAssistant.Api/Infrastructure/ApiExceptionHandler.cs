using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Infrastructure;

/// <summary>Uygulama istisnalarını RFC 9457 ProblemDetails yanıtlarına çevirir.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Erişim reddedildi"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "İş kuralı ihlali"),
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "İstek iptal edildi"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata"),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "İşlenmeyen hata: {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
        };

        if (exception is FluentValidation.ValidationException ve)
        {
            problem.Extensions["errors"] = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
