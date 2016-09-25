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
using System;
namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// Keeps track of local and remote task counts
    /// </summary>
    /// <remarks>Does not share work group counts</remarks>
    /// <seealso cref="System.IDisposable" />
    public interface ITaskSchedulerJobCountSync: IDisposable
    {
        /// <summary>
        /// Occurs when the counter has changed due to a remote change
        /// </summary>
        event EventHandler RemoteCountChanged;

        /// <summary>
        /// Gets the current task count.
        /// </summary>
        /// <returns></returns>
        long GetCurrentTaskCount();

        /// <summary>
        /// Increases the current task count.
        /// </summary>
        /// <returns>The new value</returns>
        long IncreaseCurrentTaskCount();

        /// <summary>
        /// Decreases the current task count.
        /// </summary>
        /// <returns>The new value</returns>
        long DecreaseCurrentTaskCount();

        /// <summary>
        /// Starts this instance.
        /// </summary>
        void Start();
    }
}
