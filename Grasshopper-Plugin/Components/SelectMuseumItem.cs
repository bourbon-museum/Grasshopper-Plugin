using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using GrasshopperPlugin.Models;
using GrasshopperPlugin.Services;
using GH_IO.Serialization;

namespace GrasshopperPlugin.Components
{
    /// <summary>
    /// Lists the collection's items by name and lets the user pick one by double-clicking
    /// the component. The picked item's ID, title and number are output, ready for
    /// Place Collection Item.
    /// </summary>
    public class SelectMuseumItem : GH_Component
    {
        private static readonly WordPressRestClient RestClient = new();

        private List<CollectionItem> _items = new();
        private string _loadedKey = string.Empty;
        private int _selectedId;

        public SelectMuseumItem()
            : base("Select Museum Item", "PickWP",
                   "Lists collection items by name. Double-click the component to pick one.",
                   "Museum", "Collection")
        {
        }

        public override Guid ComponentGuid => new("A6D93F18-5B72-4E0C-8C41-7B20E5D1F3A9");

        protected override System.Drawing.Bitmap Icon => null!;

        public override void CreateAttributes()
        {
            m_attributes = new SelectMuseumItemAttributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "URL", "WordPress REST API collection endpoint.", GH_ParamAccess.item, CollectionPlacement.DefaultUrl);
            pManager.AddTextParameter("Search", "S", "Only list items whose title/content contains this text.", GH_ParamAccess.item, string.Empty);
            pManager.AddBooleanParameter("Refresh", "R", "Set to true to fetch the list again from the site.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("ID", "ID", "WordPress post ID of the picked item.", GH_ParamAccess.item);
            pManager.AddTextParameter("Title", "T", "Name of the picked item.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Number", "N", "Collection number of the picked item.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var url = string.Empty;
            var search = string.Empty;
            var refresh = false;

            if (!DA.GetData(0, ref url)) return;
            DA.GetData(1, ref search);
            DA.GetData(2, ref refresh);

            if (string.IsNullOrWhiteSpace(url))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "URL input is empty.");
                return;
            }

            var key = $"{url}\n{search}";
            if (refresh || key != _loadedKey)
            {
                if (!Load(url, search)) return;
                _loadedKey = key;
            }

            if (_items.Count == 0)
            {
                Message = "No items";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No items matched the search.");
                return;
            }

            // Keep the earlier pick while it is still in the list; otherwise start on the first item.
            var selected = _items.FirstOrDefault(i => i.Id == _selectedId) ?? _items[0];
            _selectedId = selected.Id;

            var title = CollectionPlacement.DecodeTitle(selected.Title.Rendered);
            Message = Shorten($"{selected.Acf.Number}. {title}", 24);

            DA.SetData(0, selected.Id);
            DA.SetData(1, title);
            DA.SetData(2, selected.Acf.Number ?? 0);
        }

        private bool Load(string url, string search)
        {
            var queryUrl = string.IsNullOrWhiteSpace(search)
                ? url
                : $"{url}{(url.Contains("?") ? "&" : "?")}search={Uri.EscapeDataString(search)}";

            try
            {
                _items = Task.Run(() => RestClient.GetCollectionItemsAsync(queryUrl)).GetAwaiter().GetResult()
                    .OrderBy(i => i.Acf.Number ?? int.MaxValue)
                    .ThenBy(i => i.Id)
                    .ToList();
                return true;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to fetch data from '{queryUrl}': {ex.Message}");
                return false;
            }
        }

        /// <summary>Shows a filterable list of the loaded items and applies the user's pick.</summary>
        internal void PickItem()
        {
            if (_items.Count == 0)
            {
                Rhino.UI.Dialogs.ShowMessage("There are no items to pick from yet. Connect a URL and let the component solve first.", "Select Museum Item");
                return;
            }

            var picked = PickerDialog.Show(_items, _selectedId);
            if (picked == null || picked == _selectedId) return;

            RecordUndoEvent("Select Museum Item");
            _selectedId = picked.Value;
            ExpireSolution(true);
        }

        private static string Shorten(string text, int max)
            => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("SelectedId", _selectedId);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedId")) _selectedId = reader.GetInt32("SelectedId");
            return base.Read(reader);
        }
    }

    /// <summary>Opens the picker when the component is double-clicked.</summary>
    internal class SelectMuseumItemAttributes : GH_ComponentAttributes
    {
        public SelectMuseumItemAttributes(SelectMuseumItem owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (Bounds.Contains(e.CanvasLocation))
            {
                ((SelectMuseumItem)Owner).PickItem();
                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDoubleClick(sender, e);
        }
    }

    /// <summary>A small modal window with a filter box above a list of "number. name" rows.</summary>
    internal static class PickerDialog
    {
        public static int? Show(IReadOnlyList<CollectionItem> items, int selectedId)
        {
            var rows = items
                .Select(i => (Id: i.Id, Label: $"{i.Acf.Number}. {CollectionPlacement.DecodeTitle(i.Title.Rendered)}"))
                .ToList();

            int? result = null;
            var filter = new TextBox { PlaceholderText = "Filter by name…" };
            var list = new ListBox { Height = 320 };
            var ok = new Button { Text = "Pick" };
            var cancel = new Button { Text = "Cancel" };
            var shown = new List<(int Id, string Label)>();

            void Refill()
            {
                var text = filter.Text?.Trim() ?? string.Empty;
                shown = rows
                    .Where(r => text.Length == 0 || r.Label.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                list.Items.Clear();
                foreach (var row in shown) list.Items.Add(new ListItem { Text = row.Label });

                var index = shown.FindIndex(r => r.Id == selectedId);
                list.SelectedIndex = index >= 0 ? index : (shown.Count > 0 ? 0 : -1);
            }

            var dialog = new Dialog<bool>
            {
                Title = "Select Museum Item",
                MinimumSize = new Size(480, 440),
                Padding = 10,
                DefaultButton = ok,
                AbortButton = cancel,
            };

            ok.Click += (_, _) => dialog.Close(true);
            cancel.Click += (_, _) => dialog.Close(false);
            list.MouseDoubleClick += (_, _) => { if (list.SelectedIndex >= 0) dialog.Close(true); };
            filter.TextChanged += (_, _) => Refill();

            dialog.Content = new StackLayout
            {
                Spacing = 8,
                Items =
                {
                    filter,
                    new StackLayoutItem(list, HorizontalAlignment.Stretch, true),
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Items = { cancel, ok },
                    },
                },
            };

            Refill();

            if (dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow) && list.SelectedIndex >= 0)
            {
                result = shown[list.SelectedIndex].Id;
            }

            return result;
        }
    }
}
