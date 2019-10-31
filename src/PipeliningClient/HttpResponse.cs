namespace PipeliningClient
{
    public enum HttpResponseState
    {
        StartLine,
        Headers,
        Body,
        ChunkedBody,
        Completed,
        Error
    }

    public class HttpResponse
    {
        public HttpResponseState State { get; set; } = HttpResponseState.StartLine;
        public int StatusCode { get; set; }
        public int ContentLength { get; set; }
        public long ContentLengthRemaining { get; set; }
        public bool HasContentLengthHeader { get; set; }
        public int LastChunkRemaining { get; set; }
    }
}
