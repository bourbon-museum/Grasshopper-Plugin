using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GrasshopperPlugin.Models;
using Newtonsoft.Json;

namespace GrasshopperPlugin.Services
{
    /// <summary>
    /// Fetches museum object data from a WordPress REST API endpoint.
    /// </summary>
    public class WordPressRestClient
    {
        private static readonly HttpClient DefaultHttpClient = new();

        private readonly HttpClient _httpClient;

        public WordPressRestClient(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? DefaultHttpClient;
        }

        /// <summary>
        /// Fetches a WordPress REST collection endpoint (e.g. .../wp-json/wp/v2/distillery)
        /// and deserializes the response into a list of museum objects.
        /// </summary>
        public async Task<List<MuseumObject>> GetMuseumObjectsAsync(string url, CancellationToken cancellationToken = default)
        {
            var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<MuseumObject>>(json) ?? new List<MuseumObject>();
        }

        /// <summary>
        /// Fetches a single WordPress REST item endpoint (e.g. .../wp-json/wp/v2/distillery/42)
        /// and deserializes the response into a museum object.
        /// </summary>
        public async Task<MuseumObject> GetMuseumObjectAsync(string url, CancellationToken cancellationToken = default)
        {
            var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<MuseumObject>(json)
                   ?? throw new JsonSerializationException($"Received empty or invalid JSON from '{url}'.");
        }

        private async Task<string> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}
