using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests
{
    /// <summary>
    /// Shared test infrastructure for NetMQ-based tests.
    /// Consolidates XunitLogger, port allocation, and beacon interface
    /// that were previously duplicated across 4 test files.
    /// </summary>
    internal static class TestPorts
    {
        private static int _nextPort = 40000 + Random.Shared.Next(0, 10000);
        public static int Next() => Interlocked.Increment(ref _nextPort);
    }

    internal static class BeaconInterfaces
    {
        public static readonly string Default =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "" : "loopback";
    }

    internal sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output) => _output = output;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try { _output.WriteLine($"[{logLevel}] {formatter(state, exception)}"); }
            catch { /* test may have ended */ }
        }
    }
}
