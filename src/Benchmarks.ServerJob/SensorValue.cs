using System;

namespace Benchmarks.ServerJob
{
    public class Measurement
    {
        public DateTime TimeStamp { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }
    }
}
