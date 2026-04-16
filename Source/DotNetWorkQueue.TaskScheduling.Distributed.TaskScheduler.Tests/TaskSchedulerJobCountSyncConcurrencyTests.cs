using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    [Collection("NetMQ")]
    public class TaskSchedulerJobCountSyncConcurrencyTests
    {
        private readonly ITestOutputHelper _output;

        public TaskSchedulerJobCountSyncConcurrencyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Increase_And_Decrease_Under_Contention_Final_Count_Matches_Delta()
        {
            var port = TestPorts.Next();
            var config = new TaskSchedulerMultipleConfiguration(port, BeaconInterfaces.Default);
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
    }
}
