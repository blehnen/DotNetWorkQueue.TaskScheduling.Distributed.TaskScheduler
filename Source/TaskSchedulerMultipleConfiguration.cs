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
namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// Configuration settings for <see cref="TaskSchedulerMultiple"/>
    /// </summary>
    public class TaskSchedulerMultipleConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskSchedulerMultipleConfiguration"/> class.
        /// </summary>
        /// <param name="port">The port to run the bus on.</param>
        /// <param name="beaconInterface">
        /// The interface name or IP address that the UDP beacon should bind to.
        /// <para/>
        /// Valid values (interpreted by <c>NetMQBeacon</c>):
        /// <list type="bullet">
        ///   <item><c>"loopback"</c> — bind to <c>127.0.0.1</c> and broadcast to <c>255.255.255.255</c>. Works on Windows only; on Linux the socket will not receive its own broadcasts.</item>
        ///   <item><c>"*"</c> — bind to all interfaces (<c>0.0.0.0</c>) and broadcast to <c>255.255.255.255</c>.</item>
        ///   <item><c>""</c> (empty) — bind to the first available non-loopback interface and use its subnet broadcast address. Works cross-platform; discovery is scoped to the local subnet.</item>
        ///   <item>An IP address (e.g. <c>"192.168.1.42"</c>) — bind to the interface with that address.</item>
        /// </list>
        /// Defaults to <c>"loopback"</c> to preserve prior behavior.
        /// </param>
        public TaskSchedulerMultipleConfiguration(int port, string beaconInterface = "loopback")
        {
            BroadCastPort = port;
            BeaconInterface = beaconInterface;
        }

        /// <summary>
        /// Gets the broad cast port that the scheduler will announce on
        /// </summary>
        /// <value>
        /// The broad cast port.
        /// </value>
        public int BroadCastPort { get; }

        /// <summary>
        /// Gets the interface name or IP address that the UDP beacon should bind to.
        /// </summary>
        /// <value>
        /// The beacon interface. See the constructor documentation for accepted values.
        /// </value>
        public string BeaconInterface { get; }
    }
}
