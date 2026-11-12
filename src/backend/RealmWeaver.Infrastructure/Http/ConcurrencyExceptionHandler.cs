using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace RealmWeaver.Infrastructure.Http;

/// <summary>
/// Turns an optimistic-concurrency failure (the row's Postgres <c>xmin</c> changed under us) into
/// a 409 ProblemDetails, so clients know to reload and retry.
/// </summary>
public sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The resource was changed by another request.",
                Detail = "Reload it and try again.",
            },
        });
    }
}
