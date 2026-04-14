using System;
using System.Threading;
using NetMQ;
using Xunit;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    [Collection("NetMQ")]
    public class NetMqQueueApiProbeTests
    {
        [Fact]
        public void NetMqQueue_WithPoller_ReceivesEnqueuedItem()
        {
            using var queue = new NetMQQueue<int>();
            var received = 0;
            var signal = new ManualResetEventSlim();

            queue.ReceiveReady += (_, args) =>
            {
                received = args.Queue.Dequeue();
                signal.Set();
            };

            using var poller = new NetMQPoller { queue };
            poller.RunAsync();

            queue.Enqueue(42);

            Assert.True(signal.Wait(TimeSpan.FromSeconds(5)), "Queue ReceiveReady never fired");
            Assert.Equal(42, received);

            poller.Stop();
        }
    }
}
