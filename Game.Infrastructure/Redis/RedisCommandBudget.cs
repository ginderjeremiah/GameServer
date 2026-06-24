using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Redis
{
    /// <summary>
    /// Cooperatively honours a per-command cancellation budget over StackExchange.Redis commands, which expose no
    /// <see cref="CancellationToken"/> of their own. <see cref="Read"/> and <see cref="Write{T}"/> make the
    /// <em>await</em> unwind promptly when the budget is cancelled (releasing the caller without waiting out the
    /// dependency's own command timeout); the underlying command still runs to completion in the background.
    /// <para>
    /// Both observe a post-cancellation fault on that abandoned command (so it never surfaces via
    /// <see cref="TaskScheduler.UnobservedTaskException"/> on finalization), but they differ in how loudly: a
    /// <see cref="Read"/> discards only a value, so its fault is observed <em>silently</em>, whereas a
    /// <see cref="Write{T}"/> could have silently failed with no other signal, so its fault is <em>logged</em>.
    /// <see cref="CancellationToken.None"/> makes <c>WaitAsync</c> a zero-overhead no-op, so uncancelled callers
    /// pay nothing.
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
            try
            {
                return await command.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Losing the read *value* is fine, but the abandoned command may still *fault* (the connection drop
                // or timeout that triggered the cancellation). With no continuation that fault would surface via
                // TaskScheduler.UnobservedTaskException on finalization, so observe it — silently, since unlike a
                // Write a discarded read carries no signal worth logging.
                ObserveFaultSilently(command);
                throw;
            }
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

        // The abandoned command settles in the background after the cancelled await; this logs the fault as an
        // error so a write that may not have applied isn't lost silently.
        private static void ObserveFault(Task command, ILogger logger, string faultMessage)
        {
            OnFault(command, faulted => logger.LogError(faulted.Exception, "{FaultMessage}", faultMessage));
        }

        // Observe an abandoned read's fault without logging: a discarded read has no signal worth an error, but
        // the fault must still be observed so it doesn't surface via TaskScheduler.UnobservedTaskException.
        private static void ObserveFaultSilently(Task command)
        {
            OnFault(command, static faulted => _ = faulted.Exception);
        }

        // OnlyOnFaulted + ExecuteSynchronously keeps the continuation allocation-light and never schedules on
        // success, running only when the abandoned command actually faults.
        private static void OnFault(Task command, Action<Task> onFaulted)
        {
            _ = command.ContinueWith(
                onFaulted,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
