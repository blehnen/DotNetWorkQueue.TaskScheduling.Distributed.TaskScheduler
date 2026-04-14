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
    public class TaskSchedulerJobCountSyncStateTests
    {
        // Port base 55000 — disjoint from the concurrency test file (50000) and the
        // existing TaskSchedulerJobCountSyncTests.cs range (40000-49999).
        private static int _nextPort = 55000 + Random.Shared.Next(0, 1000);
        private static int NextPort() => Interlocked.Increment(ref _nextPort);

        private static readonly string BeaconInterface =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "" : "loopback";

        private readonly ITestOutputHelper _output;

        public TaskSchedulerJobCountSyncStateTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task RemoteSetCount_From_Node_A_Is_Aggregated_By_Node_B()
        {
            // Two nodes on the SAME port — beacon discovery finds peers by the configured bus
            // port. Confirmed against the existing TwoNodesDiscoverEachOtherAndSyncCounts test
            // which reuses a single `port` for both configs.
            var port = NextPort();

            var configA = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var busA = new TaskSchedulerBus(new XunitLogger(_output), configA);
            var syncA = new TaskSchedulerJobCountSync(busA, new XunitLogger(_output));

            var configB = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var busB = new TaskSchedulerBus(new XunitLogger(_output), configB);
            var syncB = new TaskSchedulerJobCountSync(busB, new XunitLogger(_output));

            try
            {
                syncA.Start();
                syncB.Start();
                await Task.Delay(3000); // beacon + handshake

                // Scripted sequence on A
                syncA.IncreaseCurrentTaskCount(); // A=1
                syncA.IncreaseCurrentTaskCount(); // A=2
                syncA.IncreaseCurrentTaskCount(); // A=3
                syncA.DecreaseCurrentTaskCount(); // A=2

                // Poll with deadline
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < deadline && syncB.GetCurrentTaskCount() != 2L)
                {
                    await Task.Delay(50);
                }

                Assert.Equal(2L, syncB.GetCurrentTaskCount());
                Assert.Equal(2L, syncA.GetCurrentTaskCount());
            }
            finally
            {
                syncA.Dispose();
                syncB.Dispose();
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
