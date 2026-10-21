using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrasshopperPlugin.Models;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GrasshopperPlugin.Services
{
    /// <summary>
    /// Puts collection items into the Rhino document: a vertical plane textured with
    /// the item's featured image, with the item's name as a wrapped label beneath it.
    /// </summary>
    public static class CollectionPlacement
    {
        public const string DefaultUrl = "https://getzmuseum.com/wp-json/wp/v2/museum-item";

        private const string LayerName = "Collection Items";
        private const string SourceKey = "WP_Source";
        private const string SourceValue = "museum-item";

        private static readonly WordPressRestClient RestClient = new();

        /// <summary>
        /// A vertical frame whose origin is the middle of the plane's bottom edge:
        /// X runs along the plane's width, Y points up, and with no rotation it faces -Y
        /// so it reads from the Front view.
        /// </summary>
        public static Plane VerticalFrame(Point3d bottomCenter, double rotationDegrees)
        {
            var angle = RhinoMath.ToRadians(rotationDegrees);
            var xAxis = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
            return new Plane(bottomCenter, xAxis, Vector3d.ZAxis);
        }

        public static string GetSiteRoot(string collectionUrl)
        {
            var index = collectionUrl.IndexOf("/wp-json/", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? collectionUrl.Substring(0, index) : collectionUrl.TrimEnd('/');
        }

        /// <summary>WordPress titles arrive HTML-encoded (e.g. "&amp;", "&#8217;").</summary>
        public static string DecodeTitle(string html) => System.Net.WebUtility.HtmlDecode(html);

        /// <summary>Plane width for an image, from its pixel size. Falls back to a portrait ratio.</summary>
        public static double PlaneWidth(MediaItem? media, double height)
        {
            var aspect = media != null && media.Details.Height > 0
                ? (double)media.Details.Width / media.Details.Height
                : 0.75;
            return height * aspect;
        }

        /// <summary>
        /// Adds the plane and its label to the document as a group, so a Rhino gimbal moves
        /// them together. Any earlier placement of the same item is replaced, so placing an item
        /// again moves it instead of duplicating it. If the item is already there with the same
        /// shape, nothing is touched, which keeps a selected item selected.
        /// </summary>
        public static Mesh Place(RhinoDoc doc, CollectionItem item, MediaItem? media, Plane frame, double height, Action<string> warn)
        {
            var title = DecodeTitle(item.Title.Rendered);
            var width = PlaneWidth(media, height);

            var mesh = BuildPlane(frame, width, height);

            var existing = FindPlane(doc, item.Id);
            if (existing?.Geometry is Mesh existingMesh && SameShape(existingMesh, mesh)) return existingMesh;

            RemovePlaced(doc, o => o.Attributes.GetUserString("WP_ID") == item.Id.ToString());
            var layerIndex = EnsureLayer(doc);

            string? texturePath = null;
            if (media == null)
            {
                warn($"'{title}' has no featured image.");
            }
            else
            {
                texturePath = TryGetTexture(media, title, warn);
            }

            var attributes = new ObjectAttributes { LayerIndex = layerIndex, Name = title };
            Tag(attributes, item, title);

            if (texturePath != null)
            {
                var material = new Material { Name = title, DiffuseColor = System.Drawing.Color.White };
                if (material.SetBitmapTexture(texturePath))
                {
                    attributes.MaterialIndex = doc.Materials.Add(material);
                    attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                }
            }

            var meshId = doc.Objects.AddMesh(mesh, attributes);

            // Label wraps to the plane's width and hangs just beneath its bottom edge. With
            // centre alignment the wrap box is centred on the plane origin, which is the
            // middle of the bottom edge.
            var textHeight = height * 0.06;
            var labelOrigin = frame.Origin - frame.YAxis * (textHeight * 0.5);
            var labelPlane = new Plane(labelOrigin, frame.XAxis, frame.YAxis);

            var text = TextEntity.Create(title, labelPlane, doc.DimStyles.Current, true, width, 0);
            text.TextHeight = textHeight;
            text.TextHorizontalAlignment = TextHorizontalAlignment.Center;
            text.TextVerticalAlignment = TextVerticalAlignment.Top;

            // Fixed colour so the label stays readable whatever the layer colour is.
            var labelAttributes = new ObjectAttributes
            {
                LayerIndex = layerIndex,
                Name = $"{title} (label)",
                ObjectColor = System.Drawing.Color.Black,
                ColorSource = ObjectColorSource.ColorFromObject,
            };
            Tag(labelAttributes, item, title);
            var textId = doc.Objects.Add(text, labelAttributes);

            doc.Groups.Add(new[] { meshId, textId });

            return mesh;
        }

        /// <summary>The plane object a collection item was placed as, or null.</summary>
        public static RhinoObject? FindPlane(RhinoDoc doc, int id)
        {
            var settings = new ObjectEnumeratorSettings { HiddenObjects = true, LockedObjects = true };
            return doc.Objects.GetObjectList(settings).FirstOrDefault(o =>
                o.ObjectType == ObjectType.Mesh &&
                o.Attributes.GetUserString(SourceKey) == SourceValue &&
                o.Attributes.GetUserString("WP_ID") == id.ToString());
        }

        /// <summary>
        /// Reads a placed plane's frame back from its corners: the middle of the bottom edge,
        /// the direction along the width, and the direction up. The height is the plane's left edge.
        /// </summary>
        public static bool TryReadFrame(Mesh mesh, out Plane frame, out double height)
        {
            frame = Plane.Unset;
            height = 0;
            if (mesh.Vertices.Count < 4) return false;

            var bottomLeft = (Point3d)mesh.Vertices[0];
            var bottomRight = (Point3d)mesh.Vertices[1];
            var topLeft = (Point3d)mesh.Vertices[3];

            var across = bottomRight - bottomLeft;
            var up = topLeft - bottomLeft;
            if (!across.Unitize() || !up.Unitize()) return false;

            height = bottomLeft.DistanceTo(topLeft);
            frame = new Plane((bottomLeft + bottomRight) / 2, across, up);
            return true;
        }

        private static bool SameShape(Mesh a, Mesh b)
        {
            if (a.Vertices.Count != b.Vertices.Count) return false;

            for (var i = 0; i < a.Vertices.Count; i++)
            {
                if (((Point3d)a.Vertices[i]).DistanceTo(b.Vertices[i]) > 1e-4) return false;
            }

            return true;
        }

        /// <summary>Removes everything this plugin has placed, or only the objects matching <paramref name="filter"/>.</summary>
        public static void RemovePlaced(RhinoDoc doc, Func<RhinoObject, bool>? filter = null)
        {
            var settings = new ObjectEnumeratorSettings { HiddenObjects = true, LockedObjects = true };
            var previous = doc.Objects.GetObjectList(settings)
                .Where(o => o.Attributes.GetUserString(SourceKey) == SourceValue)
                .Where(o => filter == null || filter(o))
                .ToList();

            foreach (var obj in previous)
            {
                doc.Objects.Delete(obj, true);
            }
        }

        /// <summary>A vertical quad with texture coordinates running from its bottom-left corner.</summary>
        private static Mesh BuildPlane(Plane frame, double width, double height)
        {
            var bottomLeft = frame.Origin - frame.XAxis * (width / 2);
            var across = frame.XAxis * width;
            var up = frame.YAxis * height;

            var mesh = new Mesh();
            mesh.Vertices.Add(bottomLeft);
            mesh.Vertices.Add(bottomLeft + across);
            mesh.Vertices.Add(bottomLeft + across + up);
            mesh.Vertices.Add(bottomLeft + up);
            mesh.Faces.AddFace(0, 1, 2, 3);
            mesh.TextureCoordinates.Add(0f, 0f);
            mesh.TextureCoordinates.Add(1f, 0f);
            mesh.TextureCoordinates.Add(1f, 1f);
            mesh.TextureCoordinates.Add(0f, 1f);
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        private static void Tag(ObjectAttributes attributes, CollectionItem item, string title)
        {
            attributes.SetUserString(SourceKey, SourceValue);
            attributes.SetUserString("WP_ID", item.Id.ToString());
            attributes.SetUserString("WP_Number", (item.Acf.Number ?? 0).ToString());
            attributes.SetUserString("WP_Title", title);
            attributes.SetUserString("WP_Subtitle", item.Acf.Subtitle);
            attributes.SetUserString("WP_Location", item.Acf.Location);
            attributes.SetUserString("WP_Proof", item.Acf.Proof);
            attributes.SetUserString("WP_Era", item.Acf.Era);
            attributes.SetUserString("WP_Link", item.Link);
        }

        private static int EnsureLayer(RhinoDoc doc)
        {
            var existing = doc.Layers.FindName(LayerName);
            if (existing != null) return existing.Index;

            return doc.Layers.Add(new Layer { Name = LayerName, Color = System.Drawing.Color.White });
        }

        /// <summary>
        /// Returns a local path to an image Rhino can load as a texture, or null.
        /// The site serves WebP, which Rhino cannot read, so the original upload
        /// (same path with a .png or .jpg extension) is used instead.
        /// </summary>
        private static string? TryGetTexture(MediaItem media, string title, Action<string> warn)
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "GrasshopperPlugin", "images");
            Directory.CreateDirectory(cacheDir);

            var candidates = new List<string>();
            var extension = Path.GetExtension(media.SourceUrl);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(media.SourceUrl);
            }
            else
            {
                var stem = media.SourceUrl.Substring(0, media.SourceUrl.Length - extension.Length);
                candidates.Add(stem + ".png");
                candidates.Add(stem + ".jpg");
            }

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(cacheDir, $"{media.Id}{Path.GetExtension(candidate).ToLowerInvariant()}");
                if (File.Exists(path)) return path;

                try
                {
                    if (Task.Run(() => RestClient.TryDownloadAsync(candidate, path)).GetAwaiter().GetResult()) return path;
                }
                catch (Exception ex)
                {
                    warn($"Could not download image for '{title}': {ex.Message}");
                    return null;
                }
            }

            var converted = TryConvertWebP(media, cacheDir);
            if (converted != null) return converted;

            warn($"No PNG/JPG original found for '{title}'; its image is {media.MimeType}, which Rhino cannot texture.");
            return null;
        }

        /// <summary>
        /// Downloads a WebP image and converts it to PNG with macOS's built-in sips tool.
        /// Returns null on other platforms or if the conversion fails.
        /// </summary>
        private static string? TryConvertWebP(MediaItem media, string cacheDir)
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)) return null;
            if (!media.MimeType.Equals("image/webp", StringComparison.OrdinalIgnoreCase)) return null;

            var pngPath = Path.Combine(cacheDir, $"{media.Id}.png");
            if (File.Exists(pngPath)) return pngPath;

            var webpPath = Path.Combine(cacheDir, $"{media.Id}.webp");
            try
            {
                if (!Task.Run(() => RestClient.TryDownloadAsync(media.SourceUrl, webpPath)).GetAwaiter().GetResult()) return null;

                var info = new System.Diagnostics.ProcessStartInfo("/usr/bin/sips")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = $"-s format png \"{webpPath}\" --out \"{pngPath}\"",
                };

                using var process = System.Diagnostics.Process.Start(info);
                process?.WaitForExit(30000);
                return File.Exists(pngPath) ? pngPath : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
