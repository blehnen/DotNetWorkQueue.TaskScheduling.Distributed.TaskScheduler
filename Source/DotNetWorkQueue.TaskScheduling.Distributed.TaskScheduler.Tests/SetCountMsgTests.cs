using System;
using System.Threading;
using NetMQ;
using Xunit;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    [Collection("NetMQ")]
    public class SetCountMsgTests
    {
        [Fact]
        public void SetCountMsg_Equality_IsValueBased()
        {
            var a = new SetCountMsg(5000, 42L);
            var b = new SetCountMsg(5000, 42L);
            var c = new SetCountMsg(5000, 43L);
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void SetCountMsg_RoundTripsThroughNetMqQueue()
        {
            using var queue = new NetMQQueue<SetCountMsg>();
            SetCountMsg received = default;
            var signal = new ManualResetEventSlim();

            queue.ReceiveReady += (_, args) =>
            {
                received = args.Queue.Dequeue();
                signal.Set();
            };

            using var poller = new NetMQPoller { queue };
            poller.RunAsync();

            var sent = new SetCountMsg(12345, 7L);
            queue.Enqueue(sent);

            Assert.True(signal.Wait(TimeSpan.FromSeconds(5)), "SetCountMsg never surfaced on the poller");
            Assert.Equal(sent, received);

            poller.Stop();
        }
    }
}
