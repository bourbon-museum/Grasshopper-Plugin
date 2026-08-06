using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using GrasshopperPlugin.Models;
using GrasshopperPlugin.Services;
using Newtonsoft.Json;

namespace GrasshopperPlugin.Components
{
    /// <summary>
    /// Fetches museum object posts from a WordPress REST API endpoint and
    /// exposes their IDs, titles, and ACF Pro field data on the canvas.
    /// </summary>
    public class FetchMuseumData : GH_Component
    {
        private static readonly WordPressRestClient RestClient = new();

        public FetchMuseumData()
            : base("Fetch Museum Data", "FetchWP",
                   "Fetches museum object posts from a WordPress REST API endpoint.",
                   "Museum", "Data")
        {
        }

        public override Guid ComponentGuid => new("74263313-2A65-4DB3-8290-83AD98A1E5F5");

        protected override System.Drawing.Bitmap Icon => null!;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "URL", "WordPress REST API collection endpoint to fetch.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Fetch", "F", "Set to true to fetch data from the URL.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("IDs", "ID", "WordPress post IDs.", GH_ParamAccess.list);
            pManager.AddTextParameter("Titles", "T", "Post titles.", GH_ParamAccess.list);
            pManager.AddTextParameter("ACF", "ACF", "ACF Pro field data for each post, as JSON.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var url = string.Empty;
            var fetch = false;

            if (!DA.GetData(0, ref url)) return;
            DA.GetData(1, ref fetch);

            if (!fetch) return;

            if (string.IsNullOrWhiteSpace(url))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "URL input is empty.");
                return;
            }

            List<MuseumObject> museumObjects;
            try
            {
                museumObjects = Task.Run(() => RestClient.GetMuseumObjectsAsync(url)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to fetch data from '{url}': {ex.Message}");
                return;
            }

            DA.SetDataList(0, museumObjects.Select(o => o.Id));
            DA.SetDataList(1, museumObjects.Select(o => o.Title.Rendered));
            DA.SetDataList(2, museumObjects.Select(o => JsonConvert.SerializeObject(o.Acf)));
        }
    }
}
