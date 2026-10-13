using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this WebApplication app)
    {
        app.MapPost("/api/links", CreateLinkAsync)
            .WithName("CreateLink")
            .WithSummary("Cria um link curto")
            .WithDescription("Cria um link curto e devolve a chave privada de gerenciamento.")
            .Produces<CreateLinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/{code}", RedirectAsync)
            .WithName("RedirectLink")
            .WithSummary("Redireciona para o destino")
            .WithDescription("Redireciona um código válido para a URL de destino.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        app.MapGet("/api/links/{code}/stats", GetStatsAsync)
            .WithName("GetLinkStats")
            .WithSummary("Consulta os acessos do link")
            .WithDescription("Exige a chave de gerenciamento enviada no header X-Management-Key.")
            .Produces<LinkStatsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateLinkAsync(
        CreateLinkRequest request,
        HttpContext httpContext,
        IUrlValidator urlValidator,
        ILinkService linkService,
        CancellationToken cancellationToken)
    {
        var urlError = await urlValidator.ValidateAsync(request.TargetUrl, cancellationToken);

        if (urlError is not null)
        {
            return ValidationError("targetUrl", urlError);
        }

        var result = await linkService.CreateAsync(
            new CreateLinkCommand(request.TargetUrl!, request.Code, request.ExpiresAtUtc),
            cancellationToken);

        return result.Status switch
        {
            CreateLinkStatus.Created => TypedResults.Created(
                $"/{result.Code}",
                new CreateLinkResponse(
                    $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{result.Code}",
                    result.Code!,
                    result.ManagementKey!)),
            CreateLinkStatus.InvalidCode => ValidationError(
                "code",
                "O código deve conter de 4 a 20 caracteres, usando somente A-Z maiúsculo e os dígitos de 1 a 9."),
            CreateLinkStatus.ExpirationInPast => ValidationError(
                "expiresAtUtc",
                "A data de expiração não pode estar no passado."),
            CreateLinkStatus.CodeAlreadyExists => TypedResults.Problem(
                title: "Código em uso",
                detail: "O código informado já está em uso.",
                statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Problem(
                title: "Não foi possível criar o link",
                detail: "Não foi possível gerar um código exclusivo. Tente novamente.",
                statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        ILinkService linkService,
        CancellationToken cancellationToken)
    {
        var result = await linkService.AccessAsync(code, cancellationToken);

        if (!result.Exists)
        {
            return TypedResults.Problem(
                title: "Link não encontrado",
                detail: "O código informado não existe.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (result.IsExpired)
        {
            return TypedResults.Problem(
                title: "Link expirado",
                detail: "A validade deste link terminou.",
                statusCode: StatusCodes.Status410Gone);
        }

        return TypedResults.Redirect(result.TargetUrl!, permanent: false);
    }

    private static async Task<IResult> GetStatsAsync(
        string code,
        HttpRequest request,
        ILinkService linkService,
        CancellationToken cancellationToken)
    {
        request.Headers.TryGetValue("X-Management-Key", out var key);
        var result = await linkService.GetStatsAsync(code, key.ToString(), cancellationToken);

        return result.Status switch
        {
            LinkStatsStatus.Success => TypedResults.Ok(
                new LinkStatsResponse(code, result.AccessCount!.Value)),
            LinkStatsStatus.NotFound => TypedResults.Problem(
                title: "Link não encontrado",
                detail: "O código informado não existe.",
                statusCode: StatusCodes.Status404NotFound),
            _ => TypedResults.Problem(
                title: "Chave de gerenciamento inválida",
                detail: "A chave de gerenciamento está ausente ou é inválida.",
                statusCode: StatusCodes.Status401Unauthorized)
        };
    }

    private static ValidationProblem ValidationError(string field, string message) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]> { [field] = [message] },
            title: "A solicitação contém dados inválidos.");
}

/// <summary>Dados necessários para criar um link curto.</summary>
public sealed record CreateLinkRequest
{
    /// <summary>URL HTTP ou HTTPS de destino.</summary>
    public string? TargetUrl { get; init; }

    /// <summary>Código personalizado opcional de 4 a 20 caracteres.</summary>
    public string? Code { get; init; }

    /// <summary>Data de expiração interpretada como uma data UTC.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

/// <summary>Link curto criado e sua chave de gerenciamento privada.</summary>
public sealed record CreateLinkResponse(
    string ShortUrl,
    string Code,
    string ManagementKey);

/// <summary>Total de acessos registrado para um link.</summary>
public sealed record LinkStatsResponse(string Code, long AccessCount);
