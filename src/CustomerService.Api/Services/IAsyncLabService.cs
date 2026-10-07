namespace CustomerService.Api.Services;

public interface IAsyncLabService
{
    Task<string> GetMessageAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamMessagesAsync(
        CancellationToken cancellationToken);
}