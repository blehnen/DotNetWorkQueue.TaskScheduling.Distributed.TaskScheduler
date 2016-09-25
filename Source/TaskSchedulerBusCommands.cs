// ---------------------------------------------------------------------
//This file is part of DotNetWorkQueue
//Copyright © 2016 Brian Lehnen
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
    /// Supported commands
    /// </summary>
    internal enum TaskSchedulerBusCommands
    {
        /// <summary>
        /// All external commands start with this; internal bus commands do not
        /// </summary>
        Publish = 0,
        /// <summary>
        /// Inform everyone else we are here
        /// </summary>
        /// <remarks>
        /// Frame0 = Publish
        /// Frame1 = Broadcast
        /// Frame2 = Our address
        /// </remarks>
        BroadCast = 1,
        /// <summary>
        /// Gets our address
        /// </summary>
        /// <remarks>
        /// Frame0 = Our address
        /// </remarks>
        GetHostAddress = 2,
        /// <summary>
        /// A node has been added
        /// </summary>
        /// <remarks>
        /// Frame0 = AddedNode
        /// Frame1 = Node Address
        /// </remarks>
        AddedNode = 3,
        /// <summary>
        /// A node has been removed
        /// </summary>
        /// <remarks>
        /// Frame0 = RemoveNode
        /// Frame1 = Node Address
        /// </remarks>
        RemovedNode = 4,
        /// <summary>
        /// Broadcast our current count
        /// </summary>
        /// <remarks>
        /// Frame0 = Publish
        /// Frame1 = Broadcast
        /// Frame2 = Our Port
        /// Frame3 = Count
        /// </remarks>
        SetCount = 5
    }
}
