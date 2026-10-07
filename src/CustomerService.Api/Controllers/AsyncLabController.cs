using CustomerService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Api.Controllers;

[ApiController]
[Route("api/labs/async")]
public sealed class AsyncLabController : ControllerBase
{
    private readonly IAsyncLabService _asyncLabService;
    private readonly ILogger<AsyncLabController> _logger;

    public AsyncLabController(
        IAsyncLabService asyncLabService,
        ILogger<AsyncLabController> logger)
    {
        _asyncLabService = asyncLabService;
        _logger = logger;
    }

    [HttpGet("message")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<ActionResult<string>> GetMessageAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Async lab started. RequestId: {RequestId}",
            HttpContext.TraceIdentifier);

        try
        {
            string message = await _asyncLabService
                .GetMessageAsync(cancellationToken);

            _logger.LogInformation(
                "Async lab completed. RequestId: {RequestId}",
                HttpContext.TraceIdentifier);

            return Ok(message);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Async lab canceled. RequestId: {RequestId}",
                HttpContext.TraceIdentifier);

            throw;
        }
    }


    [HttpGet("messages")]
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<string[]>> GetMessagesAsync(
    CancellationToken cancellationToken)
    {
        Task<string> first = _asyncLabService
            .GetMessageAsync(cancellationToken);

        Task<string> second = _asyncLabService
            .GetMessageAsync(cancellationToken);

        string[] messages = await Task.WhenAll(first, second);

        return Ok(messages);
    }

    [HttpGet("timeout")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
    StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<string>> GetWithTimeoutAsync(
    CancellationToken cancellationToken)
    {
        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            string message = await _asyncLabService
                .GetMessageAsync(timeoutSource.Token);

            return Ok(message);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Async lab canceled by client. RequestId: {RequestId}",
                HttpContext.TraceIdentifier);

            throw;
        }
        catch (OperationCanceledException)
            when (timeoutSource.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Async lab timeout reached. RequestId: {RequestId}",
                HttpContext.TraceIdentifier);

            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Simulated dependency timeout",
                detail: "The simulated dependency exceeded its two-second deadline.");
        }
    }

    [HttpGet("stream")]
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    public IAsyncEnumerable<string> StreamMessages(
    CancellationToken cancellationToken)
    {
        return _asyncLabService.StreamMessagesAsync(cancellationToken);
    }
}