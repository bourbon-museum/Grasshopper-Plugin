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
    /// Searches a WordPress REST API collection endpoint by title text and/or a
    /// taxonomy term, without needing to know a specific item's URL up front.
    /// Wire the Titles output into a stock Value List to pick a single item.
    /// </summary>
    public class SearchMuseumItems : GH_Component
    {
        private static readonly WordPressRestClient RestClient = new();

        public SearchMuseumItems()
            : base("Search Museum Items", "SearchWP",
                   "Searches a WordPress REST API collection endpoint by name and/or taxonomy term.",
                   "Museum", "Data")
        {
        }

        public override Guid ComponentGuid => new("08E0BF89-74F8-405E-A453-762E7F4AEE64");

        protected override System.Drawing.Bitmap Icon => null!;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "URL", "WordPress REST API collection endpoint to search.", GH_ParamAccess.item);
            pManager.AddTextParameter("Search", "S", "Filter items whose title/content contains this text.", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Taxonomy", "Tax", "REST base of a taxonomy to filter by (e.g. \"category\" or a custom taxonomy slug).", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Term", "Term", "Taxonomy term name or slug to filter by.", GH_ParamAccess.item, string.Empty);
            pManager.AddBooleanParameter("Fetch", "F", "Set to true to run the search.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("IDs", "ID", "WordPress post IDs matching the search.", GH_ParamAccess.list);
            pManager.AddTextParameter("Titles", "T", "Post titles matching the search.", GH_ParamAccess.list);
            pManager.AddTextParameter("ACF", "ACF", "ACF Pro field data for each matching post, as JSON.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var url = string.Empty;
            var search = string.Empty;
            var taxonomy = string.Empty;
            var term = string.Empty;
            var fetch = false;

            if (!DA.GetData(0, ref url)) return;
            DA.GetData(1, ref search);
            DA.GetData(2, ref taxonomy);
            DA.GetData(3, ref term);
            DA.GetData(4, ref fetch);

            if (!fetch) return;

            if (string.IsNullOrWhiteSpace(url))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "URL input is empty.");
                return;
            }

            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParams.Add($"search={Uri.EscapeDataString(search)}");
            }

            if (!string.IsNullOrWhiteSpace(taxonomy) && !string.IsNullOrWhiteSpace(term))
            {
                int? termId;
                try
                {
                    termId = Task.Run(() => ResolveTaxonomyTermIdAsync(url, taxonomy, term)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to resolve term '{term}' in taxonomy '{taxonomy}': {ex.Message}");
                    return;
                }

                if (termId is null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"No term matching '{term}' found in taxonomy '{taxonomy}'.");
                    DA.SetDataList(0, Array.Empty<int>());
                    DA.SetDataList(1, Array.Empty<string>());
                    DA.SetDataList(2, Array.Empty<string>());
                    return;
                }

                queryParams.Add($"{Uri.EscapeDataString(taxonomy)}={termId}");
            }

            var queryUrl = queryParams.Count > 0 ? $"{url}?{string.Join("&", queryParams)}" : url;

            List<MuseumObject> museumObjects;
            try
            {
                museumObjects = Task.Run(() => RestClient.GetMuseumObjectsAsync(queryUrl)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to fetch data from '{queryUrl}': {ex.Message}");
                return;
            }

            DA.SetDataList(0, museumObjects.Select(o => o.Id));
            DA.SetDataList(1, museumObjects.Select(o => o.Title.Rendered));
            DA.SetDataList(2, museumObjects.Select(o => JsonConvert.SerializeObject(o.Acf)));
        }

        /// <summary>Resolves a taxonomy term name/slug to its numeric ID via that taxonomy's terms endpoint.</summary>
        private static async Task<int?> ResolveTaxonomyTermIdAsync(string collectionUrl, string taxonomyRestBase, string term)
        {
            var siteRoot = GetSiteRoot(collectionUrl);
            var termsUrl = $"{siteRoot}/wp-json/wp/v2/{taxonomyRestBase}?search={Uri.EscapeDataString(term)}";
            var terms = await RestClient.GetTaxonomyTermsAsync(termsUrl).ConfigureAwait(false);

            var exactMatch = terms.FirstOrDefault(t =>
                string.Equals(t.Slug, term, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Name, term, StringComparison.OrdinalIgnoreCase));

            return (exactMatch ?? terms.FirstOrDefault())?.Id;
        }

        private static string GetSiteRoot(string collectionUrl)
        {
            var index = collectionUrl.IndexOf("/wp-json/", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? collectionUrl.Substring(0, index) : collectionUrl.TrimEnd('/');
        }
    }
}
