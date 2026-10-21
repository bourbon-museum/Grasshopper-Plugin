using System;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using GrasshopperPlugin.Models;
using GrasshopperPlugin.Services;
using Rhino;
using Rhino.Geometry;

namespace GrasshopperPlugin.Components
{
    /// <summary>
    /// Places one collection item in the Rhino document at a chosen point. Pick the item
    /// from Search Museum Items results (e.g. with a List Item component), then place the
    /// next one with another instance of this component. Placing the same item again
    /// moves it rather than duplicating it, and Remove takes it out of the scene.
    /// </summary>
    public class PlaceCollectionItem : GH_Component
    {
        private static readonly WordPressRestClient RestClient = new();

        public PlaceCollectionItem()
            : base("Place Collection Item", "PlaceWP1",
                   "Places one collection item in Rhino as a vertical plane textured with its featured image, labelled with its name.",
                   "Museum", "Collection")
        {
        }

        public override Guid ComponentGuid => new("4E8A1D57-92B3-4C06-A7F1-3D5B6E209C84");

        protected override System.Drawing.Bitmap Icon => null!;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "URL", "WordPress REST API collection endpoint.", GH_ParamAccess.item, CollectionPlacement.DefaultUrl);
            pManager.AddIntegerParameter("ID", "ID", "WordPress post ID of the item to place, e.g. an entry from Search Museum Items.", GH_ParamAccess.item);
            pManager.AddPointParameter("Location", "L", "Where to put the middle of the plane's bottom edge.", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddNumberParameter("Rotation", "R", "Turn about the Z axis, in degrees. 0 faces the Front view.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Height", "H", "Height of the plane, in model units. Width follows the image's aspect ratio.", GH_ParamAccess.item, 12.0);
            pManager.AddBooleanParameter("Place", "P", "Set to true to place the item in the Rhino document.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Remove", "X", "Set to true to take the item out of the Rhino document instead of placing it.", GH_ParamAccess.item, false);

            pManager.AddPlaneParameter("Plane", "Pl", "Optional. Places the item on this plane instead of using Location and Rotation: the origin is the middle of the bottom edge, X runs along the width and Y points up. Use the Plane output of Selected Collection Item to keep a gimbal edit.", GH_ParamAccess.item);

            pManager[6].Optional = true;
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Title", "T", "Name of the placed item.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Plane", "M", "The placed plane.", GH_ParamAccess.item);
        }

        private void RemoveFromScene(int id)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
                return;
            }

            var undo = doc.BeginUndoRecord("Remove Collection Item");
            try
            {
                CollectionPlacement.RemovePlaced(doc, o => o.Attributes.GetUserString("WP_ID") == id.ToString());
            }
            finally
            {
                doc.EndUndoRecord(undo);
            }

            doc.Views.Redraw();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var url = string.Empty;
            var id = 0;
            var location = Point3d.Origin;
            var rotation = 0.0;
            var height = 12.0;
            var place = false;
            var remove = false;

            if (!DA.GetData(0, ref url)) return;
            if (!DA.GetData(1, ref id)) return;
            DA.GetData(2, ref location);
            DA.GetData(3, ref rotation);
            DA.GetData(4, ref height);
            DA.GetData(5, ref place);
            DA.GetData(6, ref remove);
            var plane = Plane.Unset;
            var hasPlane = DA.GetData(7, ref plane) && plane.IsValid;

            if (remove)
            {
                RemoveFromScene(id);
                return;
            }

            if (!place) return;

            if (string.IsNullOrWhiteSpace(url))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "URL input is empty.");
                return;
            }

            if (height <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Height must be greater than zero.");
                return;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
                return;
            }

            CollectionItem item;
            MediaItem? media = null;
            try
            {
                item = Task.Run(() => RestClient.GetCollectionItemAsync(url, id)).GetAwaiter().GetResult();

                if (item.FeaturedMedia > 0)
                {
                    var siteRoot = CollectionPlacement.GetSiteRoot(url);
                    var featured = item.FeaturedMedia;
                    media = Task.Run(() => RestClient.GetMediaAsync(siteRoot, new[] { featured })).GetAwaiter().GetResult()
                        .FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to fetch item {id} from '{url}': {ex.Message}");
                return;
            }

            void Warn(string message) => AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);

            var undo = doc.BeginUndoRecord("Place Collection Item");
            try
            {
                var frame = hasPlane ? plane : CollectionPlacement.VerticalFrame(location, rotation);
                var mesh = CollectionPlacement.Place(doc, item, media, frame, height, Warn);

                DA.SetData(0, CollectionPlacement.DecodeTitle(item.Title.Rendered));
                DA.SetData(1, mesh);
            }
            finally
            {
                doc.EndUndoRecord(undo);
            }

            doc.Views.Redraw();
        }
    }
}
