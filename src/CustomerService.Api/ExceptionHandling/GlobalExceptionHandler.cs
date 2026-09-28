using System.Diagnostics;
using CustomerService.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {

        string traceId =
            Activity.Current?.Id
            ?? httpContext.TraceIdentifier;

        int statusCode;
        string title;
        string detail;

        switch (exception)
        {
            case CustomerMinimumAgeException minimumAgeException:
                statusCode = StatusCodes.Status422UnprocessableEntity;
                title = "Customer policy violation";
                detail = minimumAgeException.Message;

                _logger.LogWarning(
                    "Customer minimum age policy rejected request. " +
                    "MinimumAge: {MinimumAge}, TraceId: {TraceId}, " +
                    "RequestId: {RequestId}",
                    minimumAgeException.MinimumAge,
                    traceId,
                    httpContext.TraceIdentifier);
                break;
            case DuplicateCustomerEmailException:
            case DuplicateCustomerDocumentException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Customer conflict";
                detail = exception.Message;

                _logger.LogWarning(
                    "Customer uniqueness conflict handled. " +
                    "ExceptionType: {ExceptionType}, TraceId: {TraceId}, " +
                    "RequestId: {RequestId}",
                    exception.GetType().Name,
                    traceId,
                    httpContext.TraceIdentifier);
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Unexpected error";
                detail = "An unexpected error occurred.";

                _logger.LogError(
                    exception,
                    "Unhandled exception processing {HttpMethod} {RequestPath}. " +
                    "TraceId: {TraceId}, RequestId: {RequestId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    traceId,
                    httpContext.TraceIdentifier);
                break;
        }

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path.Value
        };

        if (exception is CustomerMinimumAgeException ageException)
        {
            problemDetails.Extensions["minimumAge"] =
                ageException.MinimumAge;
        }

        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = statusCode;

        ProblemDetailsContext problemDetailsContext = new()
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        };

        bool written = await _problemDetailsService.TryWriteAsync(
            problemDetailsContext);

        if (!written)
        {
            httpContext.Response.ContentType =
                "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);
        }

        return true;
    }
}