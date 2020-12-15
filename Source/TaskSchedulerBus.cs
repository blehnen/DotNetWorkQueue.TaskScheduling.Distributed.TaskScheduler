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
using System.Collections.Generic;
using System.Linq;
using DotNetWorkQueue.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// Bus for passing messages between the nodes
    /// </summary>
    internal class TaskSchedulerBus: ITaskSchedulerBus
    {
        // Dead nodes timeout
        private readonly TimeSpan _deadNodeTimeout = TimeSpan.FromSeconds(10);
        private readonly int _broadcastPort;
        private NetMQActor _actor;
        private PublisherSocket _publisher;
        private SubscriberSocket _subscriber;
        private NetMQBeacon _beacon;
        private NetMQPoller _poller;
        private PairSocket _shim;
        private readonly Dictionary<NodeKey, DateTime> _nodes; 
        private int _randomPort;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskSchedulerBus" /> class.
        /// </summary>
        /// <param name="logFactory">The log factory.</param>
        /// <param name="configuration">The configuration.</param>
        public TaskSchedulerBus(ILogFactory logFactory, TaskSchedulerMultipleConfiguration configuration)
        {
            _log = logFactory.Create();
            _nodes = new Dictionary<NodeKey, DateTime>();
            _broadcastPort = configuration.BroadCastPort;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public NetMQActor Start()
        {
            _actor = NetMQActor.Create(RunActor);
            return _actor;
        }

        /// <summary>
        /// Creates the actor
        /// </summary>
        /// <param name="shim">The shim.</param>
        private void RunActor(PairSocket shim)
        {
            // save the shim to the class to use later
            _shim = shim;

            // create all subscriber, publisher and beacon
            using (_subscriber = new SubscriberSocket())
            using (_publisher = new PublisherSocket())
            using (_beacon = new NetMQBeacon())
            {
                // listen to actor commands
                _shim.ReceiveReady += OnShimReady;

                // subscribe to all messages
                _subscriber.Subscribe("");

                // we bind to a random port, we will later publish this port
                // using the beacon
                _randomPort = _subscriber.BindRandomPort("tcp://*");
                _log.Debug(() => $"Bus subscriber is bound to {_subscriber.Options.LastEndpoint}");

                // listen to incoming messages from other publishers, forward them to the shim
                _subscriber.ReceiveReady += OnSubscriberReady;

                // configure the beacon to listen on the broadcast port
                _log.Debug(() => $"Beacon is being configured to UDP port {_broadcastPort}");
                _beacon.Configure(_broadcastPort, "loopback");

                // publishing the random port to all other nodes
                _log.Debug(() => $"Beacon is publishing the Bus subscriber port {_randomPort}");
                _beacon.Publish(_randomPort.ToString(), TimeSpan.FromSeconds(1));

                // Subscribe to all beacon on the port
                _log.Debug(() => $"Beacon is subscribing to all beacons on UDP port {_broadcastPort}");
                _beacon.Subscribe("");

                // listen to incoming beacons
                _beacon.ReceiveReady += OnBeaconReady;

                // Create a timer to clear dead nodes
                var timer = new NetMQTimer(TimeSpan.FromSeconds(1));
                timer.Elapsed += ClearDeadNodes;

                // Create and configure the poller with all sockets and the timer
                _poller = new NetMQPoller { _shim, _subscriber, _beacon, timer };

                // signal the actor that we finished with configuration and
                // ready to work
                _shim.SignalOK();

                // polling until canceled
                _poller.Run();
            }
        }

        private void OnShimReady(object sender, NetMQSocketEventArgs e)
        {
            // new actor command
            var command = _shim.ReceiveFrameString();

            // check if we received end shim command
            if (command == NetMQActor.EndShimMessage)
            {
                // we cancel the socket which dispose and exist the shim
                _poller.Stop();
            }
            else if (command == TaskSchedulerBusCommands.Publish.ToString())
            {
                // it is a publish command
                // we just forward everything to the publisher until end of message
                var message = _shim.ReceiveMultipartMessage();
                _publisher.SendMultipartMessage(message);
            }
            else if (command == TaskSchedulerBusCommands.GetHostAddress.ToString())
            {
                var address = _beacon.HostName + ":" + _randomPort;
                _shim.SendFrame(address);
            }
        }

        private void OnSubscriberReady(object sender, NetMQSocketEventArgs e)
        {
            // we got a new message from the bus
            // let's forward everything to the shim
            var message = _subscriber.ReceiveMultipartMessage();
            _shim.SendMultipartMessage(message);
        }

        private void OnBeaconReady(object sender, NetMQBeaconEventArgs e)
        {
            // we got another beacon
            // let's check if we already know about the beacon
            var message = _beacon.Receive();
            var port = int.Parse(message.String);
            var node = new NodeKey(message.PeerHost, port);

            // check if node already exist
            if (!_nodes.ContainsKey(node))
            {
                // we have a new node, let's add it and connect to subscriber
                _nodes.Add(node, DateTime.UtcNow);
                _publisher.Connect(node.Address);
                _shim.SendMoreFrame(TaskSchedulerBusCommands.AddedNode.ToString()).SendFrame(node.Address);
            }
            else
            {
                _nodes[node] = DateTime.UtcNow; //heartbeat
            }
        }

        private void ClearDeadNodes(object sender, NetMQTimerEventArgs e)
        {
            // create an array with the dead nodes
            var deadNodes = _nodes.
                Where(n => DateTime.UtcNow > n.Value + _deadNodeTimeout)
                .Select(n => n.Key).ToArray();

            // remove all the dead nodes from the nodes list and disconnect from the publisher
            foreach (var node in deadNodes)
            {
                _nodes.Remove(node);
                _publisher.Disconnect(node.Address);
                _shim.SendMoreFrame(TaskSchedulerBusCommands.RemovedNode.ToString()).SendFrame(node.Address);
            }
        }
    }
}