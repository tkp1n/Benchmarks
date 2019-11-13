using System;

namespace Benchmarks.ServerJob
{
    public class SensorValue
    {
        public DateTime OccuredUtc { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }
    }
}
