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
using System.Threading.Tasks;
using DotNetWorkQueue.Validation;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
{
    /// <summary>
    /// A task scheduler that syncs task counts with other instances of this task scheduler on the same machine and same broadcast port.
    /// </summary>
    /// <seealso cref="DotNetWorkQueue.TaskScheduling.SmartThreadPoolTaskScheduler" />
    public class TaskSchedulerMultiple: SmartThreadPoolTaskScheduler
    {
        private readonly ITaskSchedulerJobCountSync _jobCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskSchedulerMultiple"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="waitForFreeThread">The wait for free thread.</param>
        /// <param name="metrics">The metrics.</param>
        /// <param name="jobCounter">The job counter.</param>
        /// <param name="multipleConfiguration">The multiple configuration.</param>
        public TaskSchedulerMultiple(ITaskSchedulerConfiguration configuration, IWaitForEventOrCancelThreadPool waitForFreeThread, IMetrics metrics,
            ITaskSchedulerJobCountSync jobCounter, TaskSchedulerMultipleConfiguration multipleConfiguration) : base(configuration, waitForFreeThread, metrics)
        {
            Guard.NotNull(() => jobCounter, jobCounter);
            Guard.NotNull(() => multipleConfiguration, multipleConfiguration);

            _jobCount = jobCounter;
            MultipleConfiguration = multipleConfiguration;
        }
        /// <summary>
        /// Starts this instance.
        /// </summary>
        public override void Start()
        {
            _jobCount.RemoteCountChanged += JobCountHasChanged;
            Task.Run(() =>
            {
                _jobCount.Start();
            });
            base.Start();
        }

        /// <summary>
        /// Configuration settings specific to this module.
        /// </summary>
        /// <value>
        /// The multiple configuration.
        /// </value>
        public TaskSchedulerMultipleConfiguration MultipleConfiguration { get; }

        /// <summary>
        /// Gets a value indicating whether [have room for task].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [have room for task]; otherwise, <c>false</c>.
        /// </value>
        protected override bool HaveRoomForTask => _jobCount.GetCurrentTaskCount() < MaximumConcurrencyLevel;

        /// <summary>
        /// Gets the current task count.
        /// </summary>
        /// <value>
        /// The current task count.
        /// </value>
        protected override long CurrentTaskCount => _jobCount.GetCurrentTaskCount();

        /// <summary>
        /// Increments the counter.
        /// </summary>
        protected override void IncrementCounter()
        {
            _jobCount.IncreaseCurrentTaskCount();
        }

        /// <summary>
        /// De-increments the counter.
        /// </summary>
        protected override void DeincrementCounter()
        {
            _jobCount.DecreaseCurrentTaskCount();
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (_jobCount != null)
            {
                _jobCount.RemoteCountChanged -= JobCountHasChanged;
            }
            _jobCount?.Dispose();
            base.Dispose(disposing);
        }
        /// <summary>
        /// Will fire when a remote counter has changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void JobCountHasChanged(object sender, EventArgs e)
        {
            //lets reset the wait handle; if there is a free slot we can queue another job
            SetWaitHandle(null);
        }
    }
}
