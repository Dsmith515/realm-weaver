using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RealmWeaver.Infrastructure.Http;

/// <summary>Consistent ProblemDetails responses for module endpoints.</summary>
public static class Problems
{
    public static ProblemHttpResult NotFound(string what, Guid id) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"{what} not found.", detail: $"No {what.ToLowerInvariant()} has id '{id}'.");

    public static ProblemHttpResult Forbidden(string title, string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: title, detail: detail);

    public static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The change conflicts with existing data.", detail: detail);

    public static ValidationProblem Validation(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, title: "One or more fields are invalid.");
}

/// <summary>Collects field errors, keyed by camelCase JSON path, for a ValidationProblem response.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);

    public bool IsValid => errors.Count == 0;

    public void Add(string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }

    public void Require(bool condition, string field, string message)
    {
        if (!condition)
        {
            Add(field, message);
        }
    }

    public IDictionary<string, string[]> ToDictionary() =>
        errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
}
