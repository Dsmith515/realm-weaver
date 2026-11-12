using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using RealmWeaver.Generation.Model;
using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Catalog.Endpoints;

internal static class CatalogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var components = app.MapGroup("/api/components").WithTags("Components");

        components.MapGet("/", SearchAsync).WithName("ListComponents");
        components.MapGet("/{id:guid}", GetAsync).WithName("GetComponent");
        components.MapPost("/", CreateAsync).WithName("CreateComponent");
        components.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateComponent");
        components.MapDelete("/{id:guid}", ArchiveAsync).WithName("ArchiveComponent")
            .WithDescription("Archives the component. It disappears from the catalog but stays readable for players who own it.");

        var settings = app.MapGroup("/api/settings").WithTags("Settings");
        settings.MapGet("/", GetSettingsAsync).WithName("GetSettings");
        settings.MapPut("/", UpdateSettingsAsync).WithName("UpdateSettings");
    }

    private static async Task<Results<Ok<PagedResult<ComponentDto>>, ValidationProblem>> SearchAsync(
        CatalogService catalog,
        CancellationToken cancellationToken,
        string? type = null,
        string? rarity = null,
        string? biome = null,
        string? search = null,
        bool? assignable = null,
        bool includeArchived = false,
        int page = 1,
        int pageSize = 50)
    {
        // Enums arrive as camelCase strings (as in JSON bodies); minimal-API binding would be case-sensitive.
        var errors = new ValidationErrors();
        var typeFilter = ParseEnum<ComponentType>(type, "type", errors);
        var rarityFilter = ParseEnum<Rarity>(rarity, "rarity", errors);
        var biomeFilter = ParseEnum<Biome>(biome, "biome", errors);
        errors.Require(page >= 1, "page", "Pages start at 1.");
        errors.Require(pageSize is >= 1 and <= CatalogService.MaxPageSize, "pageSize", $"Page size must be between 1 and {CatalogService.MaxPageSize}.");
        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        var query = new ComponentQuery(typeFilter, rarityFilter, biomeFilter, search, assignable, includeArchived, page, pageSize);
        return TypedResults.Ok(await catalog.SearchAsync(query, cancellationToken));
    }

    private static T? ParseEnum<T>(string? value, string field, ValidationErrors errors)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(value, out _))
        {
            return parsed;
        }

        errors.Add(field, $"Use one of: {string.Join(", ", Enum.GetNames<T>().Select(n => char.ToLowerInvariant(n[0]) + n[1..]))}.");
        return null;
    }

    private static async Task<Results<Ok<ComponentDto>, ProblemHttpResult>> GetAsync(Guid id, CatalogService catalog, CancellationToken cancellationToken) =>
        await catalog.GetComponentAsync(id, cancellationToken) is { } component
            ? TypedResults.Ok(component)
            : Problems.NotFound("Component", id);

    private static async Task<Results<Created<ComponentDto>, ValidationProblem>> CreateAsync(
        [FromBody] ComponentInput input, CatalogService catalog, CancellationToken cancellationToken)
    {
        var errors = ComponentRules.Validate(input);
        if (errors.IsValid)
        {
            await catalog.ValidateReferencesAsync(input, errors, cancellationToken);
        }

        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        var created = await catalog.CreateAsync(input, id: null, cancellationToken);
        return TypedResults.Created($"/api/components/{created.Id}", created);
    }

    private static async Task<Results<Ok<ComponentDto>, ProblemHttpResult, ValidationProblem>> UpdateAsync(
        Guid id, [FromBody] ComponentInput input, CatalogService catalog, CancellationToken cancellationToken)
    {
        var existing = await catalog.GetComponentAsync(id, cancellationToken);
        if (existing is null)
        {
            return Problems.NotFound("Component", id);
        }

        var errors = ComponentRules.Validate(input);
        errors.Require(input.Type == existing.Type, "type", "A component's type can't be changed.");
        if (errors.IsValid)
        {
            await catalog.ValidateReferencesAsync(input, errors, cancellationToken);
        }

        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        return await catalog.UpdateAsync(id, input, cancellationToken) is { } updated
            ? TypedResults.Ok(updated)
            : Problems.NotFound("Component", id);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ArchiveAsync(Guid id, CatalogService catalog, CancellationToken cancellationToken) =>
        await catalog.ArchiveAsync(id, cancellationToken)
            ? TypedResults.NoContent()
            : Problems.NotFound("Component", id);

    private static async Task<Ok<CatalogSettingsDto>> GetSettingsAsync(CatalogService catalog, CancellationToken cancellationToken) =>
        TypedResults.Ok(await catalog.GetSettingsAsync(cancellationToken));

    private static async Task<Ok<CatalogSettingsDto>> UpdateSettingsAsync(
        [FromBody] CatalogSettingsDto input, CatalogService catalog, CancellationToken cancellationToken) =>
        TypedResults.Ok(await catalog.UpdateSettingsAsync(input, cancellationToken));
}
