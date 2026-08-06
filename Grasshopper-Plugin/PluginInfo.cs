using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace GrasshopperPlugin
{
    /// <summary>
    /// Assembly-level registration metadata that Grasshopper reads when
    /// loading this plugin's .gha file.
    /// </summary>
    public class PluginInfo : GH_AssemblyInfo
    {
        public override string Name => "Grasshopper-Plugin";

        public override Bitmap Icon => null!;

        public override string Description =>
            "Streams structured metadata from a headless WordPress (ACF Pro) REST API into Rhino/Grasshopper 3D scenes.";

        public override Guid Id => new("1D15C9ED-8AB0-4EA0-B411-4B6B16D9F4D6");

        public override string AuthorName => "Collin Bishop";

        public override string AuthorContact => "collin@getzmuseum.com";
    }
}
