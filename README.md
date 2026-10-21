# Grasshopper-Plugin

<img width="2250" height="830" alt="github-grasshopper-banner" src="https://github.com/user-attachments/assets/128ca52b-57b6-40b3-ab1a-ee3eba767280" />

---

## 🚀 Overview

This custom C# compiled Grasshopper assembly (.gha) establishes a real-time data bridge between a headless WordPress CMS—powered by Advanced Custom Fields (ACF) Pro—and the Rhino/Grasshopper CAD environment. By exposing structured metadata through WordPress REST API endpoints, the plugin enables content managers to control, enrich, and parametrically drive spatial geometry, material properties, and architectural attributes directly within 3D CAD scenes.

### 1. Primary Use Cases Dynamic Architectural Dashboards
Displaying real-time leasing data, square footage calculations, or construction costs visually mapped onto 3D building massing models.

### 2. Automated Spatial Signage & Wayfinding
Generating dynamic 3D typography, room numbers, and tenant logos derived directly from web directory databases.

### 3. Parametric Product Configurator Sync
Updating 3D product variations, material swatches, and dimensions based on e-commerce catalog specifications hosted in WordPress.

---

## 📐 Data Pipeline & Architecture

The system operates across four primary operational layers, ensuring low-latency data fetching and smooth geometry execution:

```
┌─────────────────────────────────┐
│     WordPress + ACF Pro         │
│     (Headless REST API)         │
└────────────────┬────────────────┘
                 │
                 │ JSON Payload
                 ▼
┌─────────────────────────────────┐
│       Grasshopper-Plugin        │
│   ┌─────────────────────────┐   │
│   │ Network / Data Layer    │   │  <-- Asynchronous HTTP & Data Models
│   └────────────┬────────────┘   │
│                │                │
│   ┌────────────▼────────────┐   │
│   │ GH_Component Wrapper    │   │  <-- Canvas Interface & Parametric Outputs
│   └────────────┬────────────┘   │
└────────────────┼────────────────┘
                 │
                 │ Parsed Metadata Lists
                 ▼
┌─────────────────────────────────┐
│       Rhino 3D Scene            │
│ (Geometry bound by WP_ID text)  │
└─────────────────────────────────┘

```

---

### 1. Content & Metadata Layer (WordPress + ACF Pro)
* **Data Structuring:** Custom Post Types (CPTs) combined with ACF Pro field groups define spatial attributes (e.g., dimensions, material specs, IDs, occupancy rates, pricing, visibility toggles).
* **API Delivery:** WP REST API endpoints export formatted JSON payloads, stripped of redundant HTML markup, optimized for headless consumption.

### 2. Integration & Processing Layer (C# Grasshopper Plugin)
* **Asynchronous Data Ingestion:** Built using .NET (`HttpClient`), the custom C# components query API endpoints asynchronously without freezing the Rhino UI thread.
* **Payload Parsing & Mapping:** JSON response payloads are parsed using `Newtonsoft.Json` into strong-typed C# objects, which are then output as native Grasshopper data structures (Data Trees, Lists, and Primitive Types).
* **Caching & Polling Management:** Implements local caching and rate-limiting logic to prevent API throttling and allow offline/offline-fallback scene generation.

### 3. Parametric Geometry Layer (Grasshopper + RhinoCommon)
* **Attribute-Driven Geometry:** Ingested metadata feeds directly into Grasshopper definitions to dictate parametric transforms (scale, array counts, extrusion heights, colors, or structural profiles).
* **BIM & GIS Attribute Assignment:** Metadata is attached directly to geometry as user text attributes (`RhinoObject.Attributes.SetUserString`), enabling downstream export to IFC, Speckle, or visual rendering engines.

---

## 🔑 Key Functional Capabilities

* **Decoupled Workflow:** Design teams manage parametric logic inside Rhino/Grasshopper while non-technical stakeholders (project managers, interior designers, clients) manipulate spatial variables via the familiar WordPress dashboard.
* **Real-time & Batch Synchronization:** Supports both manual refresh triggers and automated polling to sync live site updates directly into active CAD sessions.
* **Robust Error Handling:** Features built-in status indicators, API response logging, and null-check defaults within the custom component UI to ensure Grasshopper definitions do not fail if internet connectivity drops or field data is missing.
* **Scalable Data Tree Mapping:** Structured JSON arrays automatically map into multidimensional Grasshopper Data Trees, preserving complex relationships (e.g., Parent Building → Floor Level → Room Metadata).

---

## 🖼️ Building a Scene from the Collection

The **Museum → Collection** components pull collection items (the `museum-item` post type) from WordPress and place them in Rhino. Each item becomes a vertical plane textured with its featured image, with its name as a wrapped label centred beneath it. Every placed object carries user text (`WP_ID`, `WP_Number`, `WP_Title`, `WP_Subtitle`, `WP_Location`, `WP_Proof`, `WP_Era`, `WP_Link`) on a `Collection Items` layer, so Bind Geometry Data can join it back to its record.

### 1. Pick an item
**Select Museum Item** lists the collection by name. Double-click the component to open a window with a filter box and the numbered list, then pick one. It outputs the item's `ID`, `Title` and `Number`. An optional `Search` input narrows the list on the server, and `Refresh` fetches it again.

*(Search Museum Items does the same lookup as plain lists, if you would rather work with the results directly.)*

### 2. Place it in the scene
Wire the `ID` into **Place Collection Item** and set:

| Input | Meaning |
|-------|---------|
| `Location` | Middle of the plane's bottom edge |
| `Rotation` | Turn about the Z axis, in degrees (0 faces the Front view) |
| `Height` | Plane height in model units. Width follows the image's aspect ratio |
| `Place` | `true` places the item |
| `Remove` | `true` takes the item out of the scene instead |
| `Plane` | Optional. Places the item on an exact plane, overriding `Location` and `Rotation` |

Use one Place Collection Item per item to fill the scene one at a time. Placing the same item again moves it rather than duplicating it, and if the item already sits where the inputs say, nothing changes. **Place All Collection Items** lays every item out in a single row ordered by collection number, which is handy for a quick overview.

Textures need a PNG or JPG. The site serves WebP, which Rhino cannot read, so the plugin downloads the original upload next to the WebP and falls back to converting the WebP with macOS's built-in `sips`. Switch the viewport to **Rendered** to see the images.

### 3. Edit or remove an item
Each plane and its label are grouped, so clicking either selects both. Drag the item with Rhino's gumball to move, rotate or scale it. **Selected Collection Item** follows the selected item and outputs its `ID`, `Title`, `Location`, `Rotation`, `Height` and exact `Plane`. It keeps its reading when you deselect, so editing does not lose the item.

* **Keep a gumball edit in Grasshopper:** wire its `Plane` and `Height` into Place Collection Item.
* **Remove an item:** wire its `ID` into Place Collection Item and set `Remove` to `true`.

---

## 📄 File Structure

* **`Grasshopper-Plugin/`**
  * `.gitignore` — Visual Studio / .NET gitignore template
  * `README.md` — Project overview and architectural design
  * `Grasshopper-Plugin.sln` — Solution file
  * **`Grasshopper-Plugin/`** (Main C# Project)
    * `Grasshopper-Plugin.csproj` — .NET project dependencies & build targets
    * `PluginInfo.cs` — Grasshopper assembly registration metadata
    * **`Models/`** — Data transfer objects for JSON deserialization
      * `MuseumObject.cs` — Core WordPress Post attributes
      * `AcfData.cs` — ACF custom field payload definitions (distillery posts)
      * `CollectionItem.cs` — Collection item (`museum-item`) posts, their ACF fields and media records
      * `TaxonomyTerm.cs` — Taxonomy terms used for search filtering
    * **`Services/`** — External communication and Rhino document logic
      * `WordPressRestClient.cs` — Async HTTP client for endpoint querying, paging and media download
      * `CollectionPlacement.cs` — Places, replaces, groups and removes collection items in the Rhino document
    * **`Components/`** — Grasshopper canvas components
      * `FetchMuseumData.cs` — Fetches a REST endpoint and exposes IDs, titles and ACF data
      * `SearchMuseumItems.cs` — Searches the collection by name and taxonomy term
      * `SelectMuseumItem.cs` — Pick a collection item by name from a filterable list
      * `PlaceCollectionItem.cs` — Places, moves or removes one collection item in the scene
      * `PlaceCollectionItems.cs` — Places every collection item in a row
      * `SelectedCollectionItem.cs` — Reads the item selected in Rhino and follows gumball edits
      * `BindGeometryData.cs` — Helper node to link geometry to data by ID

---

## 🛠️ Overview of Key Layers

### Models
C# classes representing the expected JSON schema from the WordPress REST API and ACF Pro custom field groups.

### Services
Network handling logic responsible for asynchronously fetching JSON data from specified URLs.

### Components
Custom Grasshopper nodes (GH_Component) that expose URL inputs and trigger parameters on the canvas, delivering structured data parameters to downstream Grasshopper wires.

### Geometry Binding
The design logic that uses native Rhino Attribute User Text (WP_ID) on 3D objects to join spatial elements with corresponding CMS metadata records.
