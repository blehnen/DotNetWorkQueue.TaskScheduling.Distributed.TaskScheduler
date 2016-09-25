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
    /// Registers <see cref="TaskSchedulerMultiple"/> with <see cref="DotNetWorkQueue"/>
    /// </summary>
    public static class TaskSchedulerSetup
    {
        /// <summary>
        /// Injects the distributed task scheduler as <see cref="ATaskScheduler"/>; this will replace the built in scheduler
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="broadCastPort">The broad cast port to use.</param>
        /// <remarks>Each scheduler that shares a port will attempt to limit threads by the shared pool</remarks>
        public static void InjectDistributedTaskScheduler(this IContainer container, int broadCastPort = 9999)
        {
            container.Register<ITaskSchedulerBus, TaskSchedulerBus>(LifeStyles.Singleton);
            container.Register<ITaskSchedulerJobCountSync, TaskSchedulerJobCountSync>(LifeStyles.Singleton);
            container.Register<ATaskScheduler, TaskSchedulerMultiple>(LifeStyles.Singleton);

            var configuration = new TaskSchedulerMultipleConfiguration(broadCastPort);
            container.Register(() => configuration, LifeStyles.Singleton);
        }
    }
}
