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
        public Task<List<MuseumObject>> GetMuseumObjectsAsync(string url, CancellationToken cancellationToken = default)
            => GetListAsync<MuseumObject>(url, cancellationToken);

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

        /// <summary>
        /// Fetches a WordPress REST taxonomy terms endpoint
        /// (e.g. .../wp-json/wp/v2/categories, or a custom taxonomy's rest_base).
        /// </summary>
        public Task<List<TaxonomyTerm>> GetTaxonomyTermsAsync(string url, CancellationToken cancellationToken = default)
            => GetListAsync<TaxonomyTerm>(url, cancellationToken);

        /// <summary>
        /// Fetches any WordPress REST collection endpoint and deserializes the response into a list.
        /// </summary>
        public async Task<List<T>> GetListAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        /// <summary>
        /// Fetches every item from a WordPress REST collection endpoint
        /// (e.g. .../wp-json/wp/v2/museum-item), following pagination.
        /// </summary>
        public async Task<List<CollectionItem>> GetCollectionItemsAsync(string url, CancellationToken cancellationToken = default)
        {
            const int perPage = 100;
            var separator = url.Contains("?") ? "&" : "?";
            var items = new List<CollectionItem>();

            for (var page = 1; ; page++)
            {
                var batch = await GetListAsync<CollectionItem>($"{url}{separator}per_page={perPage}&page={page}", cancellationToken).ConfigureAwait(false);
                items.AddRange(batch);
                if (batch.Count < perPage) return items;
            }
        }

        /// <summary>
        /// Fetches one collection item by post ID from a collection endpoint
        /// (e.g. .../wp-json/wp/v2/museum-item).
        /// </summary>
        public async Task<CollectionItem> GetCollectionItemAsync(string collectionUrl, int id, CancellationToken cancellationToken = default)
        {
            var url = $"{collectionUrl.TrimEnd('/')}/{id}";
            var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<CollectionItem>(json)
                   ?? throw new JsonSerializationException($"Received empty or invalid JSON from '{url}'.");
        }

        /// <summary>
        /// Fetches media library records (source URL and pixel size) for the given attachment IDs.
        /// </summary>
        public async Task<List<MediaItem>> GetMediaAsync(string siteRoot, IEnumerable<int> ids, CancellationToken cancellationToken = default)
        {
            var include = string.Join(",", ids);
            if (include.Length == 0) return new List<MediaItem>();

            var url = $"{siteRoot}/wp-json/wp/v2/media?include={include}&per_page=100&_fields=id,source_url,mime_type,media_details";
            return await GetListAsync<MediaItem>(url, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to <paramref name="path"/>. Returns false if the server does not have it.
        /// </summary>
        public async Task<bool> TryDownloadAsync(string url, string path, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            System.IO.File.WriteAllBytes(path, bytes);
            return true;
        }

        private async Task<string> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}
