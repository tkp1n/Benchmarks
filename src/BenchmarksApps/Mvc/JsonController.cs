// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Benchmarks.Controllers
{
    [ApiController]
    public class JsonController : Controller
    {
        private static readonly DateTimeOffset BaseDateTime = new DateTimeOffset(new DateTime(2019, 04, 23));

        private static readonly List<Entry> _entries2MB = Enumerable.Range(1, 5250).Select(i => new Entry
        {
            Attributes = new Attributes
            {
                Created = BaseDateTime.AddDays(i),
                Enabled = true,
                Expires = BaseDateTime.AddDays(i).AddYears(1),
                NotBefore = BaseDateTime,
                RecoveryLevel = "Purgeable",
                Updated = BaseDateTime.AddSeconds(i),
            },
            ContentType = "application/xml",
            Id = "https://benchmarktest.id/item/value" + i,
            Tags = new[] { "test", "perf", "json" },
        }).ToList();

        private static readonly List<Entry> _entries4k = _entries2MB.Take(8).ToList();
        private static readonly List<Entry> _entries100k = _entries2MB.Take(300).ToList();

        [HttpGet("/json-helloworld")]
        [Produces("application/json")]
        public object Json()
        {
            return new { message = "Hello, World!" };
        }

        [HttpGet("/json2k")]
        [Produces("application/json")]
        public List<Entry> Json2k() => _entries4k;

        [HttpGet("/json100k")]
        [Produces("application/json")]
        public List<Entry> Json100k() => _entries100k;

        [HttpGet("/json2M")]
        [Produces("application/json")]
        public List<Entry> Json2M() => _entries2MB;

        [HttpPost("/jsoninput")]
        [Consumes("application/json")]
        public ActionResult JsonInput([FromBody] List<Entry> entry) => Ok();
    }

    public partial class Entry
    {
        public Attributes Attributes { get; set; }
        public string ContentType { get; set; }
        public string Id { get; set; }
        public bool Managed { get; set; }
        public string[] Tags { get; set; }
    }

    public partial class Attributes
    {
        public DateTimeOffset Created { get; set; }
        public bool Enabled { get; set; }
        public DateTimeOffset Expires { get; set; }
        public DateTimeOffset NotBefore { get; set; }
        public string RecoveryLevel { get; set; }
        public DateTimeOffset Updated { get; set; }
    }
}
