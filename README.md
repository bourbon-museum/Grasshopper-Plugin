# Grasshopper-Plugin

<img width="2250" height="830" alt="github-grasshopper-banner" src="https://github.com/user-attachments/assets/128ca52b-57b6-40b3-ab1a-ee3eba767280" />

A custom Grasshopper 3D plugin (`.gha`) written in C# that streams structured metadata from a headless WordPress (ACF Pro) REST API directly into Rhino/Grasshopper 3D scenes. 

It allows spatial geometry in a 3D CAD environment to be dynamically enriched and driven by web-managed CMS data.

---

## 📐 System Architecture

The plugin acts as a bridge between the web CMS layer and the 3D visual programming environment:

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

### Alternative: Markdown List Format (Never Collapses)

If your environment still wraps code blocks, you can replace the `📁 Repository File Structure` section with standard Markdown lists:

* **`Grasshopper-Plugin/`**
  * `.gitignore` — Visual Studio / .NET gitignore template
  * `README.md` — Project overview and architectural design
  * `Grasshopper-Plugin.sln` — Solution file
  * **`Grasshopper-Plugin/`** (Main C# Project)
    * `Grasshopper-Plugin.csproj` — .NET project dependencies & build targets
    * `PluginInfo.cs` — Grasshopper assembly registration metadata
    * **`Models/`** — Data transfer objects for JSON deserialization
      * `MuseumObject.cs` — Core WordPress Post attributes
      * `AcfData.cs` — ACF custom field payload definitions
    * **`Services/`** — External communication logic
      * `WordPressRestClient.cs` — Async HTTP client for endpoint querying
    * **`Components/`** — Grasshopper canvas components
      * `FetchMuseumData.cs` — Main node to fetch and expose REST data
      * `BindGeometryData.cs` — Helper node to link geometry to data by ID

## 🛠️ Overview of Key Layers
Models: C# classes representing the expected JSON schema from the WordPress REST API and ACF Pro custom field groups.

Services: Network handling logic responsible for asynchronously fetching JSON data from specified URLs.

Components: Custom Grasshopper nodes (GH_Component) that expose URL inputs and trigger parameters on the canvas, delivering structured data parameters to downstream Grasshopper wires.

Geometry Binding: The design logic that uses native Rhino Attribute User Text (WP_ID) on 3D objects to join spatial elements with corresponding CMS metadata records.
