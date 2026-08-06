using System.Collections.Generic;
using Newtonsoft.Json;

namespace GrasshopperPlugin.Models
{
    /// <summary>
    /// A single WordPress post from the museum objects endpoint, combining the
    /// standard REST API post schema with its ACF Pro custom field group.
    /// </summary>
    public class MuseumObject
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("date")]
        public string Date { get; set; } = string.Empty;

        [JsonProperty("modified")]
        public string Modified { get; set; } = string.Empty;

        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("link")]
        public string Link { get; set; } = string.Empty;

        [JsonProperty("title")]
        public RenderedField Title { get; set; } = new();

        [JsonProperty("content")]
        public RenderedField Content { get; set; } = new();

        [JsonProperty("excerpt")]
        public RenderedField Excerpt { get; set; } = new();

        [JsonProperty("featured_media")]
        public int FeaturedMedia { get; set; }

        [JsonProperty("meta")]
        public Dictionary<string, object> Meta { get; set; } = new();

        [JsonProperty("acf")]
        public AcfData Acf { get; set; } = new();
    }

    /// <summary>
    /// WordPress "rendered" field wrapper used for title/content/excerpt.
    /// </summary>
    public class RenderedField
    {
        [JsonProperty("rendered")]
        public string Rendered { get; set; } = string.Empty;
    }
}
