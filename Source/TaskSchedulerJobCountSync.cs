// ---------------------------------------------------------------------
//This file is part of DotNetWorkQueue
//Copyright © 2017 Brian Lehnen
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
using NetMQ;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// Keeps track of local and remote counts
    /// </summary>
    public class TaskSchedulerJobCountSync: ITaskSchedulerJobCountSync
    {
        private bool _stopRequested;
        private bool _running;
        private long _currentTaskCount;
        private readonly object _lockSocket = new object();
        private readonly ConcurrentDictionary<int, long> _otherProcessorCounts;
        private string _hostAddress;
        private int _hostPort;

        private NetMQActor _actor;
        private readonly ITaskSchedulerBus _bus;
        private readonly ILog _log;

        /// <summary>
        /// Occurs when the counter has changed due to a remote change
        /// </summary>
        public event EventHandler RemoteCountChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskSchedulerJobCountSync" /> class.
        /// </summary>
        /// <param name="bus">The bus.</param>
        /// <param name="logFactory">The log factory.</param>
        public TaskSchedulerJobCountSync(ITaskSchedulerBus bus, ILogFactory logFactory)
        {
            _bus = bus;
            _log = logFactory.Create();
            _otherProcessorCounts = new ConcurrentDictionary<int, long>();
        }
        /// <summary>
        /// Gets the current task count.
        /// </summary>
        /// <returns></returns>
        public long GetCurrentTaskCount()
        {
            long myCount;
            long otherCount;
            lock (_lockSocket)
            {
                myCount = Interlocked.Read(ref _currentTaskCount);
                otherCount = _otherProcessorCounts.Values.Sum();
            }

            var total = myCount + otherCount;
            _log.Debug(() => $"Total {total} = [M]{myCount}+[O]{otherCount}");
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
            lock (_lockSocket)
            {
                var current = Interlocked.Increment(ref _currentTaskCount);
                _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString())
                    .SendMoreFrame(TaskSchedulerBusCommands.SetCount.ToString())
                    .SendMoreFrame(_hostPort.ToString())
                    .SendFrame(current.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return current;
            }
        }

        /// <summary>
        /// Decreases the current task count.
        /// </summary>
        /// <returns>
        /// The new value
        /// </returns>
        public long DecreaseCurrentTaskCount()
        {
            lock (_lockSocket)
            {
                var current = Interlocked.Decrement(ref _currentTaskCount);
                _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString())
                    .SendMoreFrame(TaskSchedulerBusCommands.SetCount.ToString())
                    .SendMoreFrame(_hostPort.ToString())
                    .SendFrame(current.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return current;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            lock (_lockSocket)
            {
                _actor = _bus.Start();
            }
            _currentTaskCount = 0;
            _running = true;
            try
            {
                lock (_lockSocket)
                {
                    _actor.SendFrame(TaskSchedulerBusCommands.GetHostAddress.ToString());
                }
                _hostAddress = _actor.ReceiveFrameString();
                _hostPort = new Uri("http://" + _hostAddress).Port;

                //second beacon time, so we wait to ensure beacon has fired
                Thread.Sleep(1100);

                //let other nodes know we are here
                lock (_lockSocket)
                {
                    _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString())
                        .SendMoreFrame(TaskSchedulerBusCommands.BroadCast.ToString())
                        .SendFrame(_hostAddress);
                }

                // receive messages from other nodes on the bus
                while (!_stopRequested)
                {
                    try
                    {
                        ProcessMessages();
                    }
                    catch (Exception error)
                    {
                        _log.ErrorException("Failed to handle NetMCQ commands", error);
                    }
                }
            }
            catch (Exception error)
            {
                _log.ErrorException("A fatal error occurred while processing NetMCQ commands", error);
            }
            finally
            {
                _running = false;
            }
        }

        private void ProcessMessages()
        {
            lock (_lockSocket)
            {
                if(_stopRequested) return;
                
                string message;
                if (!_actor.TryReceiveFrameString(TimeSpan.FromMilliseconds(10), Encoding.ASCII, out message)) return;
                if (message == TaskSchedulerBusCommands.BroadCast.ToString())
                {
                    // another node has let us know they are here
                    var fromHostAddress = _actor.ReceiveFrameString();
                    var msg = fromHostAddress + " broadcasting";
                    _log.Debug(() => msg);

                    // send back a welcome message via the Bus publisher
                    var reply = _hostAddress + " received";
                    _actor.SendMoreFrame(TaskSchedulerBusCommands.Publish.ToString()).SendFrame(reply);
                }
                else if (message == TaskSchedulerBusCommands.AddedNode.ToString())
                {
                    var addedAddress = _actor.ReceiveFrameString();
                    _log.Debug(() => $"Added node {addedAddress} to the Bus");
                }
                else if (message == TaskSchedulerBusCommands.RemovedNode.ToString())
                {
                    var removedAddress = _actor.ReceiveFrameString();
                    var uri = new Uri(removedAddress);
                    long temp;
                    _otherProcessorCounts.TryRemove(uri.Port, out temp);
                    _log.Debug(() => $"Removed node {removedAddress} from the Bus; it's processing count was {temp}");
                    RemoteCountChanged?.Invoke(this, EventArgs.Empty);
                }
                else if (message == TaskSchedulerBusCommands.SetCount.ToString())
                {
                    var key = int.Parse(_actor.ReceiveFrameString());
                    var value = long.Parse(_actor.ReceiveFrameString());
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
                    _log.Debug(() => message);
                }
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
                    _stopRequested = true;
                    try
                    {
                        lock (_lockSocket)
                        {
                            _actor?.Dispose();
                        }
                    }
                    catch (SocketException error)
                    {
                        if (error.ErrorCode == 10035 || error.ErrorCode == 10054) //ignore socket close errors when exiting
                        {
                            return;
                        }
                        throw;
                    }
                    while (_running)
                    {
                        Thread.Sleep(100);
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
}
