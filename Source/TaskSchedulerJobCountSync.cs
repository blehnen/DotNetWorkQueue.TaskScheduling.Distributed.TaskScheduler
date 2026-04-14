// ---------------------------------------------------------------------
//This file is part of DotNetWorkQueue
//Copyright © 2017-2020 Brian Lehnen
//
//This library is free software; you can redistribute it and/or
//modify it under the terms of the GNU Lesser General Public
//License as published by the Free Software Foundation; either
//version 2.1 of the License, or (at your option) any later version.
//
//This library is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//Lesser General Public License for more details.
//
//You should have received a copy of the GNU Lesser General Public
//License along with this library; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
// ---------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DotNetWorkQueue.Logging;
using Microsoft.Extensions.Logging;
using NetMQ;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// Keeps track of local and remote counts
    /// </summary>
    public class TaskSchedulerJobCountSync: ITaskSchedulerJobCountSync
    {
        private long _currentTaskCount;
        private readonly ConcurrentDictionary<int, long> _otherProcessorCounts;
        private string _hostAddress;
        private int _hostPort;

        private NetMQActor _actor;
        private NetMQPoller _poller;
        private NetMQQueue<SetCountMsg> _outbound;
        private readonly ITaskSchedulerBus _bus;
        private readonly ILogger _log;

        /// <summary>
        /// Occurs when the counter has changed due to a remote change
        /// </summary>
        public event EventHandler RemoteCountChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskSchedulerJobCountSync" /> class.
        /// </summary>
        /// <param name="bus">The bus.</param>
        /// <param name="log">The log.</param>
        public TaskSchedulerJobCountSync(ITaskSchedulerBus bus, ILogger log)
        {
            _bus = bus;
            _log = log;
            _otherProcessorCounts = new ConcurrentDictionary<int, long>();
        }
        /// <summary>
        /// Gets the current task count.
        /// </summary>
        /// <returns></returns>
        public long GetCurrentTaskCount()
        {
            var myCount = Interlocked.Read(ref _currentTaskCount);
            var otherCount = _otherProcessorCounts.Values.Sum();
            var total = myCount + otherCount;
            _log.LogDebug($"Total {total} = [M]{myCount}+[O]{otherCount}");
            return total;
        }

        /// <summary>
        /// Increases the current task count.
        /// </summary>
        /// <returns>
        /// The new value
        /// </returns>
        public long IncreaseCurrentTaskCount()
        {
            var newValue = Interlocked.Increment(ref _currentTaskCount);
            _outbound?.Enqueue(new SetCountMsg(_hostPort, newValue));
            return newValue;
        }

        /// <summary>
        /// Decreases the current task count.
        /// </summary>
        /// <returns>
        /// The new value
        /// </returns>
        public long DecreaseCurrentTaskCount()
        {
            var newValue = Interlocked.Decrement(ref _currentTaskCount);
            _outbound?.Enqueue(new SetCountMsg(_hostPort, newValue));
            return newValue;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            _actor = _bus.Start();
            _currentTaskCount = 0;
            try
            {
                _actor.SendFrame(TaskSchedulerBusCommands.GetHostAddress.ToString());
                _hostAddress = _actor.ReceiveFrameString();
                _hostPort = new Uri("http://" + _hostAddress).Port;

                //second beacon time, so we wait to ensure beacon has fired
                Thread.Sleep(1100);

                //let other nodes know we are here — sent on the caller thread
                //BEFORE the poller takes ownership of the socket.
                _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString())
                    .SendMoreFrame(TaskSchedulerBusCommands.BroadCast.ToString())
                    .SendFrame(_hostAddress);

                // wire the poller AFTER the initial broadcast so the poller owns the
                // socket for the rest of the lifetime. Outbound SetCount messages
                // flow through _outbound so they always hit the socket on the
                // poller thread — no user lock required.
                _outbound = new NetMQQueue<SetCountMsg>();
                _outbound.ReceiveReady += OnOutboundReady;
                _actor.ReceiveReady += OnActorReady;
                _poller = new NetMQPoller { _actor, _outbound };

                // Blocks until _poller.Stop() is called from Dispose.
                _poller.Run();
            }
            catch (Exception error)
            {
                _log.LogError($"A fatal error occurred while processing NetMCQ commands{System.Environment.NewLine}{error}");
            }
        }

        private void OnActorReady(object sender, NetMQActorEventArgs e)
        {
            try
            {
                var message = _actor.ReceiveFrameString(Encoding.ASCII);
                if (message == TaskSchedulerBusCommands.BroadCast.ToString())
                {
                    // another node has let us know they are here
                    var fromHostAddress = _actor.ReceiveFrameString();
                    var msg = fromHostAddress + " broadcasting";
                    _log.LogDebug(msg);

                    // send back a welcome message via the Bus publisher
                    var reply = _hostAddress + " received";
                    _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString()).SendFrame(reply);
                }
                else if (message == TaskSchedulerBusCommands.AddedNode.ToString())
                {
                    var addedAddress = _actor.ReceiveFrameString();
                    _log.LogDebug($"Added node {addedAddress} to the Bus");
                }
                else if (message == TaskSchedulerBusCommands.RemovedNode.ToString())
                {
                    var removedAddress = _actor.ReceiveFrameString();
                    var uri = new Uri(removedAddress);
                    long temp;
                    _otherProcessorCounts.TryRemove(uri.Port, out temp);
                    _log.LogDebug($"Removed node {removedAddress} from the Bus; it's processing count was {temp}");
                    RemoteCountChanged?.Invoke(this, EventArgs.Empty);
                }
                else if (message == TaskSchedulerBusCommands.SetCount.ToString())
                {
                    if (!int.TryParse(_actor.ReceiveFrameString(), out var key))
                    {
                        return;
                    }
                    if (!long.TryParse(_actor.ReceiveFrameString(), out var value))
                    {
                        return;
                    }
                    if (!_otherProcessorCounts.ContainsKey(key))
                    {
                        _otherProcessorCounts.TryAdd(key, value);
                    }
                    else
                    {
                        _otherProcessorCounts[key] = value;
                    }
                    RemoteCountChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    //response to broadcast
                    _log.LogDebug(message);
                }
            }
            catch (Exception error)
            {
                _log.LogError($"Failed to handle NetMCQ commands{System.Environment.NewLine}{error}");
            }
        }

        private void OnOutboundReady(object sender, NetMQQueueEventArgs<SetCountMsg> e)
        {
            while (e.Queue.TryDequeue(out var msg, TimeSpan.Zero))
            {
                _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString())
                    .SendMoreFrame(TaskSchedulerBusCommands.SetCount.ToString())
                    .SendMoreFrame(msg.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .SendFrame(msg.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _poller?.Stop();
                    try
                    {
                        _poller?.Dispose();
                        _outbound?.Dispose();
                        _actor?.Dispose();
                    }
                    catch (SocketException error)
                    {
                        if (error.ErrorCode == 10035 || error.ErrorCode == 10054) //ignore socket close errors when exiting
                        {
                            return;
                        }
                        throw;
                    }
                }
                _disposedValue = true;
            }
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }

    /// <summary>
    /// Outbound message placed on the NetMQQueue&lt;SetCountMsg&gt; by
    /// IncreaseCurrentTaskCount / DecreaseCurrentTaskCount; drained on the
    /// poller thread and translated into a Publish/SetCount wire frame.
    /// </summary>
    internal readonly record struct SetCountMsg(int Port, long Count);
}
