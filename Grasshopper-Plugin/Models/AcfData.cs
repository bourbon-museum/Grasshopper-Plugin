using System.Collections.Generic;
using Newtonsoft.Json;

namespace GrasshopperPlugin.Models
{
    /// <summary>
    /// ACF Pro "Distillery Profile Data" field group (group_distillery_profile),
    /// exposed under the "acf" key of a WordPress REST API post response.
    /// </summary>
    public class AcfData
    {
        [JsonProperty("guid")]
        public string Guid { get; set; } = string.Empty;

        [JsonProperty("tagline")]
        public string Tagline { get; set; } = string.Empty;

        /// <summary>One of: active, historic, closed.</summary>
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("location")]
        public LocationGroup Location { get; set; } = new();

        [JsonProperty("facility_amenities")]
        public FacilityAmenities FacilityAmenities { get; set; } = new();

        [JsonProperty("available_tours")]
        public AvailableToursGroup AvailableTours { get; set; } = new();

        [JsonProperty("founded")]
        public int? Founded { get; set; }

        [JsonProperty("producing")]
        public bool Producing { get; set; }

        /// <summary>One of: Small, Medium, Large.</summary>
        [JsonProperty("distillery_size")]
        public string DistillerySize { get; set; } = string.Empty;

        [JsonProperty("production_capacity")]
        public double? ProductionCapacity { get; set; }

        [JsonProperty("signature")]
        public List<SignatureExpression> Signature { get; set; } = new();

        /// <summary>Title of the linked wiki post, resolved server-side for SEO.</summary>
        [JsonProperty("wiki_article")]
        public string WikiArticle { get; set; } = string.Empty;
    }

    /// <summary>Sub-fields of the ACF "location" group field.</summary>
    public class LocationGroup
    {
        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("website")]
        public string Website { get; set; } = string.Empty;

        [JsonProperty("social_media")]
        public List<SocialMediaItem> SocialMedia { get; set; } = new();

        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("city")]
        public string City { get; set; } = string.Empty;

        [JsonProperty("state")]
        public string State { get; set; } = string.Empty;

        [JsonProperty("zipCode")]
        public string ZipCode { get; set; } = string.Empty;

        [JsonProperty("google_location")]
        public GoogleMapLocation GoogleLocation { get; set; } = new();
    }

    /// <summary>Row of the ACF "social_media" repeater field.</summary>
    public class SocialMediaItem
    {
        [JsonProperty("social_platform")]
        public string SocialPlatform { get; set; } = string.Empty;

        [JsonProperty("social_url")]
        public LinkField SocialUrl { get; set; } = new();
    }

    /// <summary>ACF "link" field value shape (return_format: array).</summary>
    public class LinkField
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("target")]
        public string Target { get; set; } = string.Empty;
    }

    /// <summary>ACF "google_map" field value shape.</summary>
    public class GoogleMapLocation
    {
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lng")]
        public double Lng { get; set; }

        [JsonProperty("zoom")]
        public int Zoom { get; set; }

        [JsonProperty("place_id")]
        public string PlaceId { get; set; } = string.Empty;
    }

    /// <summary>Sub-fields of the ACF "facility_amenities" group field.</summary>
    public class FacilityAmenities
    {
        [JsonProperty("gift_shop")]
        public bool GiftShop { get; set; }

        [JsonProperty("tasting_room")]
        public bool TastingRoom { get; set; }

        [JsonProperty("event_space")]
        public bool EventSpace { get; set; }

        [JsonProperty("wheelchair_accessible")]
        public bool WheelchairAccessible { get; set; }
    }

    /// <summary>
    /// Sub-fields of the ACF "available_tours" group field, which wraps a
    /// repeater field of the same name.
    /// </summary>
    public class AvailableToursGroup
    {
        [JsonProperty("available_tours")]
        public List<TourItem> Tours { get; set; } = new();
    }

    /// <summary>Row of the "available_tours" repeater field.</summary>
    public class TourItem
    {
        [JsonProperty("tour_name")]
        public string TourName { get; set; } = string.Empty;

        [JsonProperty("duration")]
        public int? Duration { get; set; }

        [JsonProperty("price_amount")]
        public double? PriceAmount { get; set; }

        [JsonProperty("tour_description")]
        public string TourDescription { get; set; } = string.Empty;
    }

    /// <summary>Row of the ACF "signature" repeater field.</summary>
    public class SignatureExpression
    {
        [JsonProperty("expression_name")]
        public string ExpressionName { get; set; } = string.Empty;
    }
}
