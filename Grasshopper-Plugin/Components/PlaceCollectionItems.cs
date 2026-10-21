using System;
using System.Collections.Generic;
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
    /// Fetches every collection item (museum-item post) and places them all in the Rhino
    /// document in a single row along X, ordered by their ACF "number". For placing items
    /// one at a time, use Place Collection Item instead.
    /// </summary>
    public class PlaceCollectionItems : GH_Component
    {
        private static readonly WordPressRestClient RestClient = new();

        public PlaceCollectionItems()
            : base("Place All Collection Items", "PlaceAllWP",
                   "Places every collection item in Rhino in a row, as vertical planes textured with their featured image and labelled with their name.",
                   "Museum", "Collection")
        {
        }

        public override Guid ComponentGuid => new("B3C7E2A4-6D1F-4A8B-9E35-2F4D7C1A8E60");

        protected override System.Drawing.Bitmap Icon => null!;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "URL", "WordPress REST API collection endpoint.", GH_ParamAccess.item, CollectionPlacement.DefaultUrl);
            pManager.AddBooleanParameter("Place", "P", "Set to true to fetch the items and place them in the Rhino document. Replaces items placed earlier.", GH_ParamAccess.item, false);
            pManager.AddPointParameter("Origin", "O", "Bottom-left corner of the first item.", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddNumberParameter("Height", "H", "Height of each plane, in model units. Width follows the image's aspect ratio.", GH_ParamAccess.item, 12.0);
            pManager.AddNumberParameter("Spacing", "S", "Gap between neighbouring planes, in model units.", GH_ParamAccess.item, 4.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("IDs", "ID", "WordPress post IDs, in placement order.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Numbers", "N", "Collection numbers, in placement order.", GH_ParamAccess.list);
            pManager.AddTextParameter("Titles", "T", "Item names, in placement order.", GH_ParamAccess.list);
            pManager.AddMeshParameter("Planes", "M", "The placed planes.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var url = string.Empty;
            var place = false;
            var origin = Point3d.Origin;
            var height = 12.0;
            var spacing = 4.0;

            if (!DA.GetData(0, ref url)) return;
            DA.GetData(1, ref place);
            DA.GetData(2, ref origin);
            DA.GetData(3, ref height);
            DA.GetData(4, ref spacing);

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

            List<CollectionItem> items;
            Dictionary<int, MediaItem> media;
            try
            {
                var siteRoot = CollectionPlacement.GetSiteRoot(url);
                items = Task.Run(() => RestClient.GetCollectionItemsAsync(url)).GetAwaiter().GetResult();
                items = items
                    .OrderBy(i => i.Acf.Number ?? int.MaxValue)
                    .ThenBy(i => i.Id)
                    .ToList();

                var mediaIds = items.Where(i => i.FeaturedMedia > 0).Select(i => i.FeaturedMedia).Distinct().ToList();
                media = Task.Run(() => RestClient.GetMediaAsync(siteRoot, mediaIds)).GetAwaiter().GetResult()
                    .ToDictionary(m => m.Id);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to fetch data from '{url}': {ex.Message}");
                return;
            }

            void Warn(string message) => AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);

            var undo = doc.BeginUndoRecord("Place Collection Items");
            try
            {
                CollectionPlacement.RemovePlaced(doc);

                var meshes = new List<Mesh>();
                var cursorX = origin.X;

                foreach (var item in items)
                {
                    media.TryGetValue(item.FeaturedMedia, out var mediaItem);
                    var width = CollectionPlacement.PlaneWidth(mediaItem, height);

                    var bottomCenter = new Point3d(cursorX + width / 2, origin.Y, origin.Z);
                    var frame = CollectionPlacement.VerticalFrame(bottomCenter, 0);
                    meshes.Add(CollectionPlacement.Place(doc, item, mediaItem, frame, height, Warn));

                    cursorX += width + spacing;
                }

                DA.SetDataList(0, items.Select(i => i.Id));
                DA.SetDataList(1, items.Select(i => i.Acf.Number ?? 0));
                DA.SetDataList(2, items.Select(i => CollectionPlacement.DecodeTitle(i.Title.Rendered)));
                DA.SetDataList(3, meshes);
            }
            finally
            {
                doc.EndUndoRecord(undo);
            }

            doc.Views.Redraw();
        }
    }
}
