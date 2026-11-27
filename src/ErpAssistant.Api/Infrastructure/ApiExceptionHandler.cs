using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Infrastructure;

/// <summary>
/// Uygulama istisnalarını RFC 9457 ProblemDetails yanıtlarına çevirir. Mobil istemcinin dallanabilmesi
/// için her yanıtta makine tarafından okunur bir <c>code</c> alanı da bulunur.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, code) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı", "not_found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Erişim reddedildi", "forbidden"),
            ConflictException c => (StatusCodes.Status409Conflict, "Çakışma", c.Code),
            IdempotencyKeyReusedException => (StatusCodes.Status422UnprocessableEntity, "Idempotency anahtarı yeniden kullanıldı",
                IdempotencyKeyReusedException.Code),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "İş kuralı ihlali", "domain"),
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek", "validation"),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "İstek iptal edildi", "cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata", "internal"),
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
        problem.Extensions["code"] = code;

        if (exception is FluentValidation.ValidationException ve)
        {
            problem.Extensions["errors"] = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (exception is ConflictException conflict)
        {
            foreach (var (key, value) in conflict.Extensions)
                problem.Extensions[key] = value;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
