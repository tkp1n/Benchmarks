using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BenchmarksDriver
{
    internal static class HttpClientExtensions
    {
        internal static async Task<string> DownloadFileContentAsync(this HttpClient httpClient, string uri)
        {
            using var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var downloadStream = await response.Content.ReadAsStreamAsync();
            using var stringReader = new StreamReader(downloadStream);
            return await stringReader.ReadToEndAsync();
        }

        internal static async Task DownloadFileAsync(this HttpClient httpClient, string uri, string serverJobUri, string destinationFileName)
        {
            using var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var downloadStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(destinationFileName, 1, FileOptions.Asynchronous);
            await downloadStream.CopyToAsync(fileStream);
        }
    }
}
