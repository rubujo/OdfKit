using OdfKit.Core;

using Xunit;

namespace OdfKit.Tests;

public class OdfSynchronousTaskTests
{
    [Fact]
    public void Run_DoesNotUseCallerSynchronizationContext()
    {
        SynchronizationContext? original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new RejectingSynchronizationContext());
        try
        {
            int callerThread = Environment.CurrentManagedThreadId;
            int continuationThread = OdfSynchronousTask.Run(
                async () =>
                {
                    await Task.Yield();
                    return Environment.CurrentManagedThreadId;
                });

            Assert.NotEqual(callerThread, continuationThread);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    [Fact]
    public void Run_PreservesOriginalExceptionType()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => OdfSynchronousTask.Run(
                async () =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("expected");
                }));

        Assert.Equal("expected", exception.Message);
    }

    private sealed class RejectingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            throw new InvalidOperationException("The caller synchronization context must not be captured.");
        }
    }
}
