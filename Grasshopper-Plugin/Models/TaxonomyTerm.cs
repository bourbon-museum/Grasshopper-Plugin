using Newtonsoft.Json;

namespace GrasshopperPlugin.Models
{
    /// <summary>
    /// A single term from a WordPress taxonomy terms endpoint
    /// (e.g. /wp-json/wp/v2/categories, or a custom taxonomy's rest_base).
    /// </summary>
    public class TaxonomyTerm
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;
    }
}
