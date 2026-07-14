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
using Microsoft.Extensions.Logging;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// Compile-time, source-generated log messages for the distributed task
    /// scheduler. Using <see cref="LoggerMessageAttribute"/> avoids evaluating
    /// message arguments when the target log level is disabled (resolves CA1873)
    /// and produces allocation-free logging on hot paths such as
    /// <see cref="TaskSchedulerJobCountSync.GetCurrentTaskCount"/>.
    /// </summary>
    internal static partial class TaskSchedulerLog
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Bus subscriber is bound to {Endpoint}")]
        internal static partial void BusSubscriberBound(this ILogger logger, string endpoint);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Beacon is being configured to UDP port {BroadcastPort} on interface '{BeaconInterface}'")]
        internal static partial void BeaconConfiguring(this ILogger logger, int broadcastPort, string beaconInterface);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Beacon is publishing the Bus subscriber port {RandomPort}")]
        internal static partial void BeaconPublishingPort(this ILogger logger, int randomPort);

        [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Beacon is subscribing to all beacons on UDP port {BroadcastPort}")]
        internal static partial void BeaconSubscribing(this ILogger logger, int broadcastPort);

        [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Total {Total} = [M]{MyCount}+[O]{OtherCount}")]
        internal static partial void CurrentTaskCount(this ILogger logger, long total, long myCount, long otherCount);

        [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "{FromHostAddress} broadcasting")]
        internal static partial void NodeBroadcasting(this ILogger logger, string fromHostAddress);

        [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Added node {AddedAddress} to the Bus")]
        internal static partial void NodeAdded(this ILogger logger, string addedAddress);

        [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Removed node {RemovedAddress} from the Bus; it's processing count was {Count}")]
        internal static partial void NodeRemoved(this ILogger logger, string removedAddress, long count);

        [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "{Message}")]
        internal static partial void BroadcastResponse(this ILogger logger, string message);

        [LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "A fatal error occurred while processing NetMQ commands")]
        internal static partial void ProcessCommandsFailed(this ILogger logger, Exception error);

        [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "TaskSchedulerJobCountSync poller thread terminated")]
        internal static partial void PollerThreadTerminated(this ILogger logger, Exception error);

        [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Failed to handle NetMQ commands")]
        internal static partial void HandleCommandsFailed(this ILogger logger, Exception error);

        [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "TaskSchedulerJobCountSync poller thread did not exit within 5s; forcing disposal")]
        internal static partial void PollerThreadForcedDisposal(this ILogger logger);
    }
}
