using System;
using System.Linq;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using GrasshopperPlugin.Services;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GrasshopperPlugin.Components
{
    /// <summary>
    /// Reads the collection item you click on in Rhino, for editing it. Move, rotate or scale
    /// the item with Rhino's gimbal and the outputs follow it; wire them into Place Collection
    /// Item (the Plane output keeps the exact pose) or use the ID with Remove.
    /// The reading is kept when the selection is cleared, so editing the item does not lose it.
    /// </summary>
    public class SelectedCollectionItem : GH_Component
    {
        private int _id;
        private string _title = string.Empty;
        private Plane _frame = Plane.Unset;
        private double _height;

        public SelectedCollectionItem()
            : base("Selected Collection Item", "SelWP",
                   "Reads the collection item selected in Rhino and follows it as you move it with the gimbal.",
                   "Museum", "Collection")
        {
        }

        public override Guid ComponentGuid => new("7C2F5B9E-18A4-4D63-B0E7-91A3D4F6C528");

        protected override System.Drawing.Bitmap Icon => null!;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("ID", "ID", "WordPress post ID of the selected item.", GH_ParamAccess.item);
            pManager.AddTextParameter("Title", "T", "Name of the selected item.", GH_ParamAccess.item);
            pManager.AddPointParameter("Location", "L", "Middle of the item's bottom edge.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Rotation", "R", "The item's turn about the Z axis, in degrees.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "The item's plane height, in model units.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "Pl", "The item's exact pose: origin at the middle of the bottom edge, X along the width, Y up.", GH_ParamAccess.item);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            RhinoDoc.SelectObjects += OnSelectObjects;
            RhinoDoc.ReplaceRhinoObject += OnReplaceRhinoObject;

            // Pick up an item that is already selected when the component is added.
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && _id == 0) Capture(doc, doc.Objects.GetSelectedObjects(false, false));
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            RhinoDoc.SelectObjects -= OnSelectObjects;
            RhinoDoc.ReplaceRhinoObject -= OnReplaceRhinoObject;
            base.RemovedFromDocument(document);
        }

        private void OnSelectObjects(object? sender, RhinoObjectSelectionEventArgs e)
        {
            if (!e.Selected || !Capture(e.Document, e.RhinoObjects)) return;

            RhinoApp.InvokeOnUiThread(new Action(() => ExpireSolution(true)));
        }

        /// <summary>
        /// Follows the picked item when it is transformed (e.g. with the gimbal). The replaced
        /// plane carries the new pose, so it is read directly from the event.
        /// </summary>
        private void OnReplaceRhinoObject(object? sender, RhinoReplaceObjectEventArgs e)
        {
            var replacement = e.NewRhinoObject;
            if (_id == 0 || replacement?.ObjectType != ObjectType.Mesh) return;
            if (replacement.Attributes.GetUserString("WP_Source") != "museum-item") return;
            if (replacement.Attributes.GetUserString("WP_ID") != _id.ToString()) return;
            if (!ReadPlane(replacement)) return;

            RhinoApp.InvokeOnUiThread(new Action(() => ExpireSolution(true)));
        }

        /// <summary>Reads the first plugin-placed object in <paramref name="objects"/>. Returns false if there is none.</summary>
        private bool Capture(RhinoDoc doc, System.Collections.Generic.IEnumerable<RhinoObject> objects)
        {
            var picked = objects.FirstOrDefault(o => o.Attributes.GetUserString("WP_Source") == "museum-item");
            if (picked == null) return false;

            if (!int.TryParse(picked.Attributes.GetUserString("WP_ID"), out var id)) return false;

            // The plane carries the geometry; a selected label stands in for its plane.
            var plane = picked.ObjectType == ObjectType.Mesh ? picked : CollectionPlacement.FindPlane(doc, id);
            return plane != null && ReadPlane(plane);
        }

        private bool ReadPlane(RhinoObject plane)
        {
            if (plane.Geometry is not Mesh mesh || !CollectionPlacement.TryReadFrame(mesh, out var frame, out var height)) return false;
            if (!int.TryParse(plane.Attributes.GetUserString("WP_ID"), out var id)) return false;

            _id = id;
            _title = plane.Attributes.GetUserString("WP_Title") ?? string.Empty;
            _frame = frame;
            _height = height;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (_id == 0)
            {
                Message = "Select an item";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Click a collection item's plane or label in Rhino.");
                return;
            }

            Message = _title.Length <= 24 ? _title : _title.Substring(0, 23) + "…";

            var rotation = RhinoMath.ToDegrees(Math.Atan2(_frame.XAxis.Y, _frame.XAxis.X));

            DA.SetData(0, _id);
            DA.SetData(1, _title);
            DA.SetData(2, _frame.Origin);
            DA.SetData(3, rotation);
            DA.SetData(4, _height);
            DA.SetData(5, _frame);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("Id", _id);
            writer.SetString("Title", _title);
            writer.SetDouble("Height", _height);
            writer.SetPlane("Frame", new GH_IO.Types.GH_Plane(
                _frame.OriginX, _frame.OriginY, _frame.OriginZ,
                _frame.XAxis.X, _frame.XAxis.Y, _frame.XAxis.Z,
                _frame.YAxis.X, _frame.YAxis.Y, _frame.YAxis.Z));
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("Id") && reader.ItemExists("Frame"))
            {
                _id = reader.GetInt32("Id");
                _title = reader.GetString("Title");
                _height = reader.GetDouble("Height");

                var p = reader.GetPlane("Frame");
                _frame = new Plane(
                    new Point3d(p.Origin.x, p.Origin.y, p.Origin.z),
                    new Vector3d(p.XAxis.x, p.XAxis.y, p.XAxis.z),
                    new Vector3d(p.YAxis.x, p.YAxis.y, p.YAxis.z));
            }

            return base.Read(reader);
        }
    }
}
