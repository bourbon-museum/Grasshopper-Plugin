using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;

namespace GrasshopperPlugin.Components
{
    /// <summary>
    /// Binds Rhino geometry to fetched museum object data by matching each
    /// referenced object's "WP_ID" user text attribute against a list of post IDs.
    /// </summary>
    public class BindGeometryData : GH_Component
    {
        private const string UserTextKey = "WP_ID";

        public BindGeometryData()
            : base("Bind Geometry Data", "BindWP",
                   "Links Rhino geometry to museum object data using the WP_ID user text attribute.",
                   "Museum", "Data")
        {
        }

        public override Guid ComponentGuid => new("5151A275-E4D6-453C-BAA7-58FD35998FB6");

        protected override System.Drawing.Bitmap Icon => null!;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Referenced Rhino geometry carrying a WP_ID user text attribute.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("IDs", "ID", "Post IDs to match against, e.g. from Fetch Museum Data.", GH_ParamAccess.list);
            pManager.AddTextParameter("Data", "D", "Data to bind, parallel to IDs, e.g. titles or ACF JSON.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Pass-through geometry.", GH_ParamAccess.list);
            pManager.AddTextParameter("Data", "D", "Data bound to each geometry item, or empty if unmatched.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Matched", "M", "Whether each geometry item matched a post ID.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geometry = new List<IGH_GeometricGoo>();
            var ids = new List<int>();
            var data = new List<string>();

            if (!DA.GetDataList(0, geometry)) return;
            if (!DA.GetDataList(1, ids)) return;
            if (!DA.GetDataList(2, data)) return;

            if (ids.Count != data.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "IDs and Data lists must be the same length.");
                return;
            }

            var lookup = new Dictionary<int, string>();
            for (var i = 0; i < ids.Count; i++)
            {
                lookup[ids[i]] = data[i];
            }

            var outData = new List<string>(geometry.Count);
            var outMatched = new List<bool>(geometry.Count);

            foreach (var goo in geometry)
            {
                var userText = GetUserText(goo);
                if (userText != null && int.TryParse(userText, out var wpId) && lookup.TryGetValue(wpId, out var boundData))
                {
                    outData.Add(boundData);
                    outMatched.Add(true);
                }
                else
                {
                    outData.Add(string.Empty);
                    outMatched.Add(false);
                }
            }

            DA.SetDataList(0, geometry);
            DA.SetDataList(1, outData);
            DA.SetDataList(2, outMatched);
        }

        private static string? GetUserText(IGH_GeometricGoo goo)
        {
            if (goo.ReferenceID == Guid.Empty) return null;

            var rhinoObject = RhinoDoc.ActiveDoc?.Objects.FindId(goo.ReferenceID);
            return rhinoObject?.Attributes.GetUserString(UserTextKey);
        }
    }
}
