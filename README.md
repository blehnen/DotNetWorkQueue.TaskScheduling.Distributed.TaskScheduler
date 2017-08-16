DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
=========

[![License LGPLv2.1](https://img.shields.io/badge/license-LGPLv2.1-green.svg)](http://www.gnu.org/licenses/lgpl-2.1.html)

This module is a replacement task scheduler for the task scheduler built into [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue)

It doesn't seem likly that anyone else would ever use this; however, I've added it to Github just in case. I have not published this on Nuget. I will if requested.

This module solves a very specific problem I have; I have a legacy system that forks work into child processes when running. Some of these child processes need to run queued work. However, I want to have a soft limit on how many threads the entire collection can use as a group. This replacement task scheduler handles this, with some caveats.


For instance, if there are 4 child processes running, each with their own scheduler:

|Process | Current Worker Count | Max worker count
| -----------------------------------------------|
| Child A |1 | 8
| Child B |3 | 8
| Child C |4 | 8
| Child D |3 | 10

The task scheduler will attempt to keep itself below not only it's own max worker count, but also the group as a whole. You can see that Child D can use up to 10 workers; however, it's only using 3 because the other three schedulers are using a combined total of 8 already. That does mean there are 11 workers running. Note that there is no hard limit set; we are simply limiting the conncurrency based on our current instance count, and the last known status of instances running on the same UDP port.

Features
------
* Worker thread pool will be throttled across processes. This is loose and not exact or atomic.
* Little / no configuration needed other than injecting the new scheduler. UDP port number is only configuration setting
* Each instance of the scheduler can have it's own settings for maximum threads and internal queue size.
* Scheduler instances will be removed from the pool after they havn't been seen for a while (due to unclean shutdown)

Cons
------
* Loose throttling. May use more threads than you would want depending on timing.
* No support for work groups; they will still work fine, but workgroups won't be throttled between schedulers; only inside the same instance.
* Supports running on the same machine only. While it would be straight forward to support multiple machines, this requires the network to have UDP broadcasts enabled. A lot of cloud providers have this turned off.
* Since we already know we are limited to same machine, we could get better performance using a client/server model or a shared, memory mapped file. However, while the P2P model has flaws, it's good enough for my needs.
* When a new node is added, worker usage may spike until the new node is in sync with the existing nodes.
* This works best when all schedulers have similar limits; For instance, if one scheduler has a max of 20, but the other schedulers have a max of 2, you run the risk of the schedulers with smaller values being starved of workers.

Who should use this?
------
Hard to say; I needed this to solve a specific legacy issue. This may have no benefit for anyone else.

Usage
------

Using the replacement is pretty straightforward. When creating your scheduler container, add an override method.

```csharp
using (var schedulerContainer = new SchedulerContainer(RegisterService))
{
	//etc...
}
```

Inside that override method, call the extention method to register the new task scheduler. This will replace the default one. You can leave the default port at 9999 or explictly set one. This port is the only additional configuration needed.  You may have multiple groups of schedulers on the same machine by giving them different ports.

NOTE: All schedulers who should share thread counts should be given the SAME port number.

```csharp
private static void RegisterService(IContainer container)
{
	container.InjectDistributedTaskScheduler(1234);
}
```

License
--------
Copyright � 2017 Brian Lehnen

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 2.1 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with this program.  If not, see [http://www.gnu.org/licenses/](http://www.gnu.org/licenses).

3rd party Libraries
--------

This library uses multiple 3rd party libaries, listed below.

[**DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler**]

* [ILMerge ](http://research.microsoft.com/en-us/people/mbarnett/ILMerge.aspx)

* [DotNetWorkQueue ](https://github.com/blehnen/DotNetWorkQueue)

* [NetMQ ](https://github.com/zeromq/netmq)

* [AsyncIO ](https://github.com/somdoron/AsyncIO)

##### Developed with:

[![Resharper](http://neventstore.org/images/logo_resharper_small.gif)](http://www.jetbrains.com/resharper/)[![dotCover](http://neventstore.org/images/logo_dotcover_small.gif)](http://www.jetbrains.com/dotcover/)[![dotTrace](http://neventstore.org/images/logo_dottrace_small.gif)](http://www.jetbrains.com/dottrace/)
