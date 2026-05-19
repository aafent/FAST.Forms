# FAST.FormDesigner.Runtime

A Blazor runtime package for rendering JSON-driven dynamic forms designed with
[FAST.FormDesigner](https://github.com/fast/FormDesigner).

## Installation

```
dotnet add package FAST.FormDesigner.Runtime
```

## Quick Start

**1. Register services in `Program.cs`:**

```csharp
builder.Services.AddFastForms();
```

**2. Add CSS to `wwwroot/index.html`:**

```html
<link href="_content/FAST.FormDesigner.Runtime/css/fd-runtime.css" rel="stylesheet" />
```

**3. Use the component:**

```razor
<FastForm LayoutId="customer-management"
          OnSubmit="HandleSubmit" />

@code {
    private async Task HandleSubmit(Dictionary<string, object?> payload)
    {
        // payload keys: "fragment-masterdata:customerCode",
        //               "fragment-invoicelines:row:0:productCode", etc.
        await MyApi.SaveAsync(payload);
    }
}
```

---

## Configuration

### With Remote Metadata API

```csharp
builder.Services.AddFastForms(options => {
    options.MetadataApiUrl = "https://your-metadata-api.com";
    options.MetadataApiKey = "your-api-key"; // optional
});
```

The remote API must implement:
```
GET {MetadataApiUrl}/meta/{serviceKey}/search?q={query}
→ [ { value, label, subLabel, badgeText, badgeColor } ]

GET {MetadataApiUrl}/meta/{serviceKey}/details/{value}
→ { fieldKey: value, ... }
```

### With Local Meta-Field Services

```csharp
builder.Services.AddFastForms()
    .AddMetaFieldService<CustomerLookupService>()
    .AddMetaFieldService<ProductSearchService>();
```

Local services take **priority** over the remote API.
Implement `IMetaFieldService`:

```csharp
public class CustomerLookupService : IMetaFieldService
{
    public string ServiceKey => "CustomerLookup";  // matches MetaServiceKey on the field

    public async Task<List<MetaFieldOption>> SearchAsync(string query, CancellationToken ct)
    {
        // call your API/database
        return await _customerApi.SearchAsync(query, ct);
    }

    public async Task<Dictionary<string, object?>> GetDetailsAsync(string value, CancellationToken ct)
    {
        // return a dict of fieldKey → value to auto-fill sibling fields
        var customer = await _customerApi.GetAsync(value, ct);
        return new() {
            ["customerName"] = customer.Name,
            ["taxNumber"]    = customer.TaxNumber,
        };
    }
}
```

### With Custom Repository

Load layouts and fragments from your own backend:

```csharp
builder.Services.AddFastForms()
    .WithLayoutRepository<MyLayoutRepository>()
    .WithFragmentRepository<MyFragmentRepository>();
```

---

## Theming

Override CSS custom properties in your app's stylesheet:

```css
:root {
    --fd-primary:       #your-brand-color;
    --fd-primary-hover: #your-brand-color-dark;
    --fd-font:          'Your Font', sans-serif;
    --fd-radius:        4px;
}
```

All available variables are listed in `fd-runtime.css`.

---

## Payload Shape

On submit, the payload is a flat `Dictionary<string, object?>`:

```json
{
  "fragment-header:customerCode":       "C001",
  "fragment-header:customerName":       "Acme Corp",
  "fragment-lines:row:0:productCode":   "P001",
  "fragment-lines:row:0:quantity":      5,
  "fragment-lines:row:1:productCode":   "P002",
  "fragment-lines:row:1:quantity":      2
}
```

---

## License

MIT
