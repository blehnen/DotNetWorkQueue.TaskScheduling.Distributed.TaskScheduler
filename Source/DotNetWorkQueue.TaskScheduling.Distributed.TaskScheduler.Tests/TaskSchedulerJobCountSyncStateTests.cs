using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    [Collection("NetMQ")]
    public class TaskSchedulerJobCountSyncStateTests
    {
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
            var port = TestPorts.Next();

            var configA = new TaskSchedulerMultipleConfiguration(port, BeaconInterfaces.Default);
            var busA = new TaskSchedulerBus(new XunitLogger(_output), configA);
            var syncA = new TaskSchedulerJobCountSync(busA, new XunitLogger(_output));

            var configB = new TaskSchedulerMultipleConfiguration(port, BeaconInterfaces.Default);
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
    }
}
