using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    [Collection("NetMQ")]
    public class TaskSchedulerJobCountSyncTests
    {
        private static int _nextPort = 40000 + Random.Shared.Next(0, 10000);
        private static int NextPort() => Interlocked.Increment(ref _nextPort);

        // On Linux, NetMQBeacon's "loopback" mode binds to 127.0.0.1 but sends to 255.255.255.255,
        // and the kernel will not deliver those broadcasts back to a 127.0.0.1 socket. Use the
        // first available interface instead, which binds to the subnet broadcast address and works
        // for same-host peer discovery on both platforms.
        private static readonly string BeaconInterface =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "" : "loopback";

        private readonly ILogger _logger;

        public TaskSchedulerJobCountSyncTests(ITestOutputHelper output)
        {
            _logger = new XunitLogger(output);
        }

        [Fact]
        public async Task LocalCountIncrementAndDecrement()
        {
            var port = NextPort();
            var config = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var bus = new TaskSchedulerBus(_logger, config);
            using var sync = new TaskSchedulerJobCountSync(bus, _logger);

            _ = Task.Run(() => sync.Start());
            await Task.Delay(2500);

            Assert.Equal(0, sync.GetCurrentTaskCount());

            sync.IncreaseCurrentTaskCount();
            Assert.Equal(1, sync.GetCurrentTaskCount());

            sync.IncreaseCurrentTaskCount();
            Assert.Equal(2, sync.GetCurrentTaskCount());

            sync.DecreaseCurrentTaskCount();
            Assert.Equal(1, sync.GetCurrentTaskCount());
        }

        [Fact]
        public async Task TwoNodesDiscoverEachOtherAndSyncCounts()
        {
            var port = NextPort();

            var config1 = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var bus1 = new TaskSchedulerBus(_logger, config1);
            var sync1 = new TaskSchedulerJobCountSync(bus1, _logger);

            var config2 = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var bus2 = new TaskSchedulerBus(_logger, config2);
            var sync2 = new TaskSchedulerJobCountSync(bus2, _logger);

            try
            {
                _ = Task.Run(() => sync1.Start());
                _ = Task.Run(() => sync2.Start());
                await Task.Delay(3500);

                Assert.Equal(0, sync1.GetCurrentTaskCount());
                Assert.Equal(0, sync2.GetCurrentTaskCount());

                sync1.IncreaseCurrentTaskCount();
                await Task.Delay(500);

                Assert.Equal(1, sync2.GetCurrentTaskCount());
                Assert.Equal(1, sync1.GetCurrentTaskCount());

                sync2.IncreaseCurrentTaskCount();
                await Task.Delay(500);

                Assert.Equal(2, sync1.GetCurrentTaskCount());
                Assert.Equal(2, sync2.GetCurrentTaskCount());

                sync1.DecreaseCurrentTaskCount();
                await Task.Delay(500);

                Assert.Equal(1, sync1.GetCurrentTaskCount());
                Assert.Equal(1, sync2.GetCurrentTaskCount());
            }
            finally
            {
                sync1.Dispose();
                sync2.Dispose();
            }
        }

        [Fact]
        public async Task ThreeNodesAllSeeSharedCount()
        {
            var port = NextPort();

            var syncs = new TaskSchedulerJobCountSync[3];
            for (var i = 0; i < 3; i++)
            {
                var config = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
                var bus = new TaskSchedulerBus(_logger, config);
                syncs[i] = new TaskSchedulerJobCountSync(bus, _logger);
            }

            try
            {
                // Stagger starts slightly to help discovery
                for (var i = 0; i < 3; i++)
                {
                    var index = i;
                    _ = Task.Run(() => syncs[index].Start());
                    await Task.Delay(500);
                }

                // Wait for all beacons to fire and nodes to discover each other
                await Task.Delay(5000);

                // Increment each node one at a time with propagation delay
                for (var i = 0; i < 3; i++)
                {
                    syncs[i].IncreaseCurrentTaskCount();
                    await Task.Delay(500);
                }

                await Task.Delay(1000);

                // All nodes should see a total of 3
                for (var i = 0; i < 3; i++)
                {
                    var count = syncs[i].GetCurrentTaskCount();
                    _logger.LogInformation("Node {Index} sees count {Count}", i, count);
                    Assert.Equal(3, count);
                }
            }
            finally
            {
                foreach (var sync in syncs)
                {
                    sync.Dispose();
                }
            }
        }

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
