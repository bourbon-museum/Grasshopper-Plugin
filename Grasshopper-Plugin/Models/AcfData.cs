using Newtonsoft.Json;

namespace GrasshopperPlugin.Models
{
    /// <summary>
    /// ACF Pro custom field group values exposed under the "acf" key of a
    /// WordPress REST API post response for a museum object.
    /// </summary>
    public class AcfData
    {
        [JsonProperty("material")]
        public string Material { get; set; } = string.Empty;

        [JsonProperty("era")]
        public string Era { get; set; } = string.Empty;

        [JsonProperty("dimensions")]
        public AcfDimensions Dimensions { get; set; } = new();

        [JsonProperty("condition")]
        public string Condition { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sub-fields of the ACF "dimensions" group field.
    /// </summary>
    public class AcfDimensions
    {
        [JsonProperty("height")]
        public double Height { get; set; }

        [JsonProperty("width")]
        public double Width { get; set; }

        [JsonProperty("depth")]
        public double Depth { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "cm";
    }
}
