using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    [Collection("NetMQ")]
    public class TaskSchedulerJobCountSyncLifecycleTests
    {
        // Port base 60000 — disjoint from all other test files.
        private static int _nextPort = 60000 + Random.Shared.Next(0, 1000);
        private static int NextPort() => Interlocked.Increment(ref _nextPort);

        private static readonly string BeaconInterface =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "" : "loopback";

        private readonly ITestOutputHelper _output;

        public TaskSchedulerJobCountSyncLifecycleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Start_Operate_Dispose_Completes_Within_Timeout()
        {
            var port = NextPort();
            var config = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var bus = new TaskSchedulerBus(new XunitLogger(_output), config);
            var sync = new TaskSchedulerJobCountSync(bus, new XunitLogger(_output));

            try
            {
                sync.Start();
                await Task.Delay(2500);

                // Operate
                Assert.Equal(1L, sync.IncreaseCurrentTaskCount());
                Assert.Equal(2L, sync.IncreaseCurrentTaskCount());
                Assert.Equal(1L, sync.DecreaseCurrentTaskCount());
                Assert.Equal(1L, sync.GetCurrentTaskCount());

                // Dispose must not hang.
                var disposeTask = Task.Run(() => sync.Dispose());
                var deadline = Task.Delay(TimeSpan.FromSeconds(10));
                var completed = await Task.WhenAny(disposeTask, deadline);
                Assert.True(
                    completed == disposeTask,
                    "Dispose() did not complete within 10s — likely hot-wait regression or poller thread not exiting");
                await disposeTask; // surface any exceptions
            }
            finally
            {
                // Idempotent safety net: if any assertion above throws before the
                // deliberate Dispose call, ensure the poller thread still exits so
                // subsequent tests in [Collection("NetMQ")] aren't polluted.
                try { sync.Dispose(); } catch { /* already failing */ }
            }
        }

        // Copied verbatim from TaskSchedulerJobCountSyncTests.cs:154-168. Non-generic.
        private class XunitLogger : ILogger
        {
            private readonly ITestOutputHelper _output;

            public XunitLogger(ITestOutputHelper output) => _output = output;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try { _output.WriteLine($"[{logLevel}] {formatter(state, exception)}"); }
                catch { /* test may have ended */ }
            }
        }
    }
}
