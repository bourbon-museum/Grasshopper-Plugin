using System.Collections.Generic;
using Newtonsoft.Json;

namespace GrasshopperPlugin.Models
{
    /// <summary>
    /// A single post from the "museum-item" (collection) endpoint, combining the
    /// standard REST API post schema with its ACF Pro field group.
    /// </summary>
    public class CollectionItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty("link")]
        public string Link { get; set; } = string.Empty;

        [JsonProperty("title")]
        public RenderedField Title { get; set; } = new();

        [JsonProperty("featured_media")]
        public int FeaturedMedia { get; set; }

        [JsonProperty("acf")]
        public CollectionItemAcf Acf { get; set; } = new();
    }

    /// <summary>
    /// ACF Pro field group on a collection item.
    /// </summary>
    public class CollectionItemAcf
    {
        /// <summary>Display-case number, 1-based.</summary>
        [JsonProperty("number")]
        public int? Number { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>IDs of the linked distillery posts.</summary>
        [JsonProperty("distillery")]
        public List<int> Distillery { get; set; } = new();

        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("proof")]
        public string Proof { get; set; } = string.Empty;

        [JsonProperty("bottled-year")]
        public string BottledYear { get; set; } = string.Empty;

        [JsonProperty("era")]
        public string Era { get; set; } = string.Empty;

        [JsonProperty("tasting_ratings")]
        public Dictionary<string, string> TastingRatings { get; set; } = new();
    }

    /// <summary>
    /// The parts of a WordPress media library record needed to texture a plane.
    /// </summary>
    public class MediaItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("source_url")]
        public string SourceUrl { get; set; } = string.Empty;

        [JsonProperty("mime_type")]
        public string MimeType { get; set; } = string.Empty;

        [JsonProperty("media_details")]
        public MediaDetails Details { get; set; } = new();
    }

    public class MediaDetails
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}
