using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Redis
{
    /// <summary>
    /// Cooperatively honours a per-command cancellation budget over StackExchange.Redis commands, which expose no
    /// <see cref="CancellationToken"/> of their own. <see cref="Read"/> and <see cref="Write{T}"/> make the
    /// <em>await</em> unwind promptly when the budget is cancelled (releasing the caller without waiting out the
    /// dependency's own command timeout); the underlying command still runs to completion in the background.
    /// <para>
    /// The two differ in how a post-cancellation fault on that abandoned command is treated: a <see cref="Read"/>
    /// loses only an unobserved read, so none is attached, whereas a <see cref="Write{T}"/> could have silently
    /// failed with no other signal, so a fault-logging continuation is attached. <see cref="CancellationToken.None"/>
    /// makes <c>WaitAsync</c> a zero-overhead no-op, so uncancelled callers pay nothing.
    /// </para>
    /// </summary>
    internal static class RedisCommandBudget
    {
        public static async Task<T> Read<T>(Task<T> command, CancellationToken cancellationToken)
        {
            // Honour an already-cancelled budget before awaiting: WaitAsync alone is racy because it returns the
            // inner task unchanged when that task is already complete (it checks IsCompleted before the token), so
            // a command that finished first would silently swallow the cancellation.
            cancellationToken.ThrowIfCancellationRequested();
            return await command.WaitAsync(cancellationToken);
        }

        public static async Task<T> Write<T>(Task<T> command, CancellationToken cancellationToken, ILogger logger, string faultMessage)
        {
            try
            {
                return await command.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObserveFault(command, logger, faultMessage);
                throw;
            }
        }

        public static async Task Write(Task command, CancellationToken cancellationToken, ILogger logger, string faultMessage)
        {
            try
            {
                await command.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObserveFault(command, logger, faultMessage);
                throw;
            }
        }

        // The abandoned command settles in the background after the cancelled await; OnlyOnFaulted +
        // ExecuteSynchronously keeps this allocation-light and silent on success, logging only an actual fault.
        private static void ObserveFault(Task command, ILogger logger, string faultMessage)
        {
            _ = command.ContinueWith(
                faulted => logger.LogError(faulted.Exception, "{FaultMessage}", faultMessage),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
