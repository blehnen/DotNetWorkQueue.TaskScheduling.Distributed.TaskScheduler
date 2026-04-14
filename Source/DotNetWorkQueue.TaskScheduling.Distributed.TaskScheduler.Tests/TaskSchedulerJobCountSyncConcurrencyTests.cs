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
    public class TaskSchedulerJobCountSyncConcurrencyTests
    {
        // Port base 50000 — disjoint from existing TaskSchedulerJobCountSyncTests (40000-49999)
        // and from sibling state/lifecycle files (55000, 60000).
        private static int _nextPort = 50000 + Random.Shared.Next(0, 1000);
        private static int NextPort() => Interlocked.Increment(ref _nextPort);

        private static readonly string BeaconInterface =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "" : "loopback";

        private readonly ITestOutputHelper _output;

        public TaskSchedulerJobCountSyncConcurrencyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Increase_And_Decrease_Under_Contention_Final_Count_Matches_Delta()
        {
            var port = NextPort();
            var config = new TaskSchedulerMultipleConfiguration(port, BeaconInterface);
            var bus = new TaskSchedulerBus(new XunitLogger(_output), config);
            using var sync = new TaskSchedulerJobCountSync(
                bus,
                new XunitLogger(_output));

            sync.Start();
            await Task.Delay(2500); // beacon settle

            const int threadCount = 12;
            const int iterationsPerThread = 5000;
            var increments = 0L;
            var decrements = 0L;
            var doneBarrier = new CountdownEvent(threadCount);
            var overallDeadline = Task.Delay(TimeSpan.FromSeconds(30));

            for (var t = 0; t < threadCount; t++)
            {
                var isIncrementer = t % 2 == 0;
                var thread = new Thread(() =>
                {
                    try
                    {
                        for (var i = 0; i < iterationsPerThread; i++)
                        {
                            if (isIncrementer)
                            {
                                sync.IncreaseCurrentTaskCount();
                                Interlocked.Increment(ref increments);
                            }
                            else
                            {
                                sync.DecreaseCurrentTaskCount();
                                Interlocked.Increment(ref decrements);
                            }
                        }
                    }
                    finally
                    {
                        doneBarrier.Signal();
                    }
                }) { IsBackground = true };
                thread.Start();
            }

            var completed = await Task.WhenAny(
                Task.Run(() => doneBarrier.Wait()),
                overallDeadline);
            Assert.True(
                completed != overallDeadline,
                $"Deadlock detected: producer threads did not finish within 30s. increments={increments}, decrements={decrements}");

            var expectedDelta = Interlocked.Read(ref increments) - Interlocked.Read(ref decrements);
            Assert.Equal(expectedDelta, sync.GetCurrentTaskCount());
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
