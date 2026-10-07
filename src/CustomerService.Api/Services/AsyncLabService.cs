namespace CustomerService.Api.Services;

using System.Runtime.CompilerServices;

public sealed class AsyncLabService : IAsyncLabService
{
    public async Task<string> GetMessageAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(20000, cancellationToken);

        return "Operación completada";
    }

    public async IAsyncEnumerable<string> StreamMessagesAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int number = 1; number <= 3; number++)
        {
            await Task.Delay(1000, cancellationToken);

            yield return $"Mensaje {number}";
        }
    }

}