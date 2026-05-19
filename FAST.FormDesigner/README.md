# FAST.FormDesigner — Architecture Overview

## System Summary

A **Blazor WebAssembly** form design system that separates **Spatial Layout**
from **Data Content** through two independent editors and a shared runtime renderer.

---

## Project Structure

```
FAST.FormDesigner/
├── Models/
│   └── FormModels.cs              ← All C# metadata classes
│
├── Services/
│   ├── IMetaFieldService.cs       ← Meta-field interface + CustomerLookup + ProductSearch
│   ├── FormStateContainer.cs      ← Scoped state service (cross-fragment data bus)
│   └── RepositoryServices.cs      ← IFragmentRepository, ILayoutRepository, FragmentCache, FieldToolboxFactory
│
├── Validators/
│   └── DynamicFragmentValidator.cs ← FluentValidation + cross-fragment rules + FormValidationService
│
├── Components/
│   ├── Renderer/
│   │   ├── FormRenderer.razor     ← Top-level runtime engine
│   │   ├── LayoutContainer.razor  ← Recursive layout tree (Area/Group/TabControl/TabPage)
│   │   ├── FragmentRenderer.razor ← Loads + renders one FormFragment
│   │   └── FieldRenderer.razor    ← Dynamic per-field rendering (RenderTreeBuilder)
│   │
│   ├── LayoutDesigner/            ← EDITOR A
│   │   ├── LayoutDesigner.razor   ← Three-panel shell + toolbar + fragment assignment logic
│   │   ├── DesignerNode.razor     ← Recursive canvas node with drag-drop
│   │   └── LayoutPropertiesPanel.razor
│   │
│   └── FragmentDesigner/          ← EDITOR B
│       ├── FragmentDesigner.razor ← Three-panel shell + field toolbox
│       ├── FieldPropertiesPanel.razor ← General + Validation tab + Grid columns
│       └── (FieldPropertiesPanel.razor contains the validation rule builder)
│
├── wwwroot/
│   ├── css/fd-editor.css          ← Full design system (fd- prefix, same tokens as fc-)
│   ├── layouts/
│   │   └── layout-customer-management.json
│   └── fragments/
│       └── _fragments-data.json   ← All 4 sample fragments
│
└── Program.cs                     ← DI registrations
```

---

## The Two Editors

### Editor A — Layout Designer (`LayoutDesigner.razor`)
- Manages the **shell**: containers, groups, tab controls, areas
- Left panel: **Fragment Library** (click to activate)
- Canvas: **Recursive DesignerNode tree** with HTML5 drag-drop reordering
- Right panel: **LayoutPropertiesPanel** (column span, label, fragment assignment dropdown)
- **Key integration**: clicking an active fragment onto an Area calls `HandleAssignFragment(areaId)` which sets `area.AssignedFragmentId`

### Editor B — Fragment Designer (`FragmentDesigner.razor`)
- Manages **content**: field definitions per fragment
- Left panel: **Field Toolbox** (Standard / Advanced groups)
- Canvas: **Field cards** with drag-drop reordering
- Right panel: **FieldPropertiesPanel** with 3 tabs:
  - **General**: label, key, column span, placeholder, hint, list options, meta service key
  - **Validation**: rule builder (Required, MinLength, MaxLength, Min, Max, Regex, CrossFragment)
  - **Columns**: grid column editor (for Grid-type fields)

---

## Rendering Engine

```
FormRenderer
  └── loads FormLayout by ID
  └── initialises FormStateContainer
  └── for each root container → LayoutContainer (recursive)
        ├── Area → FragmentRenderer → FieldRenderer (per field)
        ├── Group → LayoutContainer children
        ├── TabControl → tab bar + TabPage children
        └── TabPage → LayoutContainer children
```

`FieldRenderer` dispatches by `FieldType`:
- **Standard fields**: Text, Number, Date, DateTime, Checkbox, ListOfValues, TextArea → native HTML inputs via `RenderTreeBuilder`
- **Grid fields**: inline editable grid with drag-drop row reordering
- **Meta fields**: debounced search input → dropdown → `IMetaFieldService.SearchAsync()` → `GetDetailsAsync()` auto-fills sibling fields

---

## State & Data Flow

```
FieldRenderer.SetValue("fragment-masterdata", "customerCode", "C001")
  └── FormStateContainer._values["fragment-masterdata:customerCode"] = "C001"
  └── fires OnFieldChanged("fragment-masterdata:customerCode")

FormRenderer.HandleSubmit()
  └── FormValidationService.ValidateAllAsync(fragments)
        └── DynamicFragmentValidator per fragment (FluentValidation)
              └── CrossFragment rules read from FormStateContainer
  └── State.GetSubmitPayload() → Dictionary<string, object?>
  └── OnSubmit.InvokeAsync(payload)
```

### Cross-Fragment Validation Example

In `fragment-salespolicy`, the `creditLimit` field has this rule:
```json
{
  "type": "CrossFragment",
  "crossFragmentRef": "fragment-masterdata:category",
  "crossFragmentCondition": "RequiredWhen:GOLD",
  "message": "Credit Limit is required for GOLD category customers."
}
```

At validation time, `DynamicFragmentValidator` reads `State.GetValue("fragment-masterdata", "category")` and checks whether `creditLimit` has a value when category == "GOLD".

---

## Meta-Field Pattern

```csharp
// 1. Implement IMetaFieldService
public class CustomerLookupService : IMetaFieldService
{
    public string ServiceKey => "CustomerLookup";
    public Task<List<MetaFieldOption>> SearchAsync(string query, ...) { ... }
    public Task<Dictionary<string, object?>> GetDetailsAsync(string value, ...) { ... }
}

// 2. Register in Program.cs
builder.Services.AddScoped<IMetaFieldService, CustomerLookupService>();

// 3. Set on FieldDefinition
{ "metaServiceKey": "CustomerLookup", "type": "CustomerLookup" }
```

When the user selects a customer, `GetDetailsAsync("C001")` returns:
```json
{ "customerName": "Acme Corp", "taxNumber": "EL123456789", "category": "GOLD" }
```
These are written into `FormStateContainer` as sibling fields, auto-filling the read-only Name and Tax Number inputs.

---

## Drag & Drop

Uses **HTML5 native drag-drop** (same pattern as FAST.FlowChart):
- `draggable="true"` on each node/card/row
- `@ondragstart` stores the source index
- `@ondragover:preventDefault="true"` allows dropping
- `@ondrop` calls the swap helper

No SortableJS, no third-party library.

---

## Adding a New Meta-Field Service

1. Create a class implementing `IMetaFieldService`
2. Set `ServiceKey` to match the `metaServiceKey` in your `FieldDefinition`
3. Register with `builder.Services.AddScoped<IMetaFieldService, YourService>()`
4. `MetaFieldResolver` discovers it automatically via `IEnumerable<IMetaFieldService>`

---

## Usage: Runtime Form

```razor
<FormRenderer LayoutId="layout-customer-management"
              OnSubmit="HandleFormSubmit" />

@code {
    private async Task HandleFormSubmit(Dictionary<string, object?> payload)
    {
        // payload keys: "fragment-masterdata:customerCode",
        //               "fragment-address:city", etc.
        await MyApi.SaveCustomerAsync(payload);
    }
}
```
