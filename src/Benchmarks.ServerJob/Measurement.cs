using System;
using Newtonsoft.Json;

namespace Benchmarks.ServerJob
{
    public class Measurement
    {
        public DateTime Timestamp { get; set; }
        public string Name { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Dimension { get; set; }
        public object Value { get; set; }
    }
}
