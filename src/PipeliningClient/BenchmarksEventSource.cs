using System.Diagnostics.Tracing;

namespace PipeliningClient
{
    internal sealed class BenchmarksEventSource : EventSource
    {
        public static readonly BenchmarksEventSource Log = new BenchmarksEventSource();

        internal BenchmarksEventSource()
            : this("Benchmarks")
        {

        }

        // Used for testing
        internal BenchmarksEventSource(string eventSourceName)
            : base(eventSourceName)
        {
        }

        [Event(1, Level = EventLevel.Informational)]
        public void Statistic(string name, long value)
        {
            WriteEvent(1, name, value);
        }
    }
}
