using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FAST.FormDesigner.Runtime.Models;
using FAST.FormDesigner.Runtime.Services;

namespace FAST.FormDesigner.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    //  DESIGNER REPOSITORY IMPLEMENTATIONS
    //  Implement the Runtime IFormLayoutRepository / IFormFragmentRepository
    //  interfaces using wwwroot JSON files + API endpoints for save/delete.
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    //  DESIGNER-SPECIFIC REPOSITORY INTERFACES
    //  Extend the Runtime read-only interfaces with Save/Delete for the designer.
    // ─────────────────────────────────────────────────────────────────────────

    public interface IDesignerFragmentRepository : IFormFragmentRepository
    {
        Task SaveAsync(FormFragment fragment);
        Task DeleteAsync(string id);
    }

    public interface IDesignerLayoutRepository : IFormLayoutRepository
    {
        Task SaveAsync(FormLayout layout);
        Task DeleteAsync(string id);
    }

    public class DesignerFragmentRepository : IDesignerFragmentRepository
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented               = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public DesignerFragmentRepository(HttpClient http) { _http = http; }

        public async Task<List<FragmentSummary>> GetAllSummariesAsync(CancellationToken ct = default)
        {
            try
            {
                var index = await _http.GetFromJsonAsync<List<FragmentSummary>>(
                    "fragments/_index.json", _opts, ct);
                return index ?? new();
            }
            catch { return new(); }
        }

        public async Task<FormFragment?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            try
            {
                return await _http.GetFromJsonAsync<FormFragment>(
                    $"fragments/{id}.json", _opts, ct);
            }
            catch { return null; }
        }

        public async Task SaveAsync(FormFragment fragment)
        {
            fragment.UpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(fragment, _opts);
            await _http.PostAsync(
                $"api/fragments/{fragment.Id}",
                new StringContent(json, Encoding.UTF8, "application/json"));
        }

        public async Task DeleteAsync(string id)
        {
            await _http.DeleteAsync($"api/fragments/{id}");
        }
    }

    public class DesignerLayoutRepository : IDesignerLayoutRepository
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented               = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public DesignerLayoutRepository(HttpClient http) { _http = http; }

        public async Task<List<FormLayout>> GetAllAsync(CancellationToken ct = default)
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<FormLayout>>(
                    "layouts/_index.json", _opts, ct);
                return list ?? new();
            }
            catch { return new(); }
        }

        public async Task<FormLayout?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            try
            {
                return await _http.GetFromJsonAsync<FormLayout>(
                    $"layouts/{id}.json", _opts, ct);
            }
            catch { return null; }
        }

        public async Task SaveAsync(FormLayout layout)
        {
            layout.UpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(layout, _opts);
            await _http.PostAsync(
                $"api/layouts/{layout.Id}",
                new StringContent(json, Encoding.UTF8, "application/json"));
        }

        public async Task DeleteAsync(string id)
        {
            await _http.DeleteAsync($"api/layouts/{id}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  EXAMPLE META-FIELD SERVICE IMPLEMENTATIONS
    //  These ship with the designer as examples.
    //  Consumer apps provide their own implementations.
    // ─────────────────────────────────────────────────────────────────────────

    public class CustomerLookupService : IMetaFieldService
    {
        private readonly HttpClient _http;
        public string ServiceKey => "CustomerLookup";

        public CustomerLookupService(HttpClient http) { _http = http; }

        public async Task<List<MetaFieldOption>> SearchAsync(string query, CancellationToken ct = default)
        {
            await Task.Delay(120, ct);
            var customers = new[]
            {
                new { Code="C001", Name="Acme Corp",        TaxNo="EL123456789", Status="ACTIVE"   },
                new { Code="C002", Name="Beta Industries",  TaxNo="EL987654321", Status="ACTIVE"   },
                new { Code="C003", Name="Gamma Ltd",        TaxNo="EL555000111", Status="INACTIVE" },
                new { Code="C004", Name="Delta Solutions",  TaxNo="EL444333222", Status="ACTIVE"   },
                new { Code="C005", Name="Epsilon Services", TaxNo="EL111222333", Status="ACTIVE"   },
            };
            return customers
                .Where(c => c.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(c => new MetaFieldOption
                {
                    Value      = c.Code,
                    Label      = c.Name,
                    SubLabel   = c.TaxNo,
                    BadgeText  = c.Status,
                    BadgeColor = c.Status == "ACTIVE" ? "#22c55e" : "#ef4444"
                })
                .ToList();
        }

        public async Task<Dictionary<string, object?>> GetDetailsAsync(string value, CancellationToken ct = default)
        {
            await Task.Delay(80, ct);
            return value switch
            {
                "C001" => new() { ["customerName"] = "Acme Corp",        ["taxNumber"] = "EL123456789", ["category"] = "GOLD"   },
                "C002" => new() { ["customerName"] = "Beta Industries",  ["taxNumber"] = "EL987654321", ["category"] = "SILVER" },
                "C003" => new() { ["customerName"] = "Gamma Ltd",        ["taxNumber"] = "EL555000111", ["category"] = "BRONZE" },
                "C004" => new() { ["customerName"] = "Delta Solutions",  ["taxNumber"] = "EL444333222", ["category"] = "GOLD"   },
                "C005" => new() { ["customerName"] = "Epsilon Services", ["taxNumber"] = "EL111222333", ["category"] = "SILVER" },
                _      => new()
            };
        }
    }

    public class ProductSearchService : IMetaFieldService
    {
        private readonly HttpClient _http;
        public string ServiceKey => "ProductSearch";

        public ProductSearchService(HttpClient http) { _http = http; }

        public async Task<List<MetaFieldOption>> SearchAsync(string query, CancellationToken ct = default)
        {
            await Task.Delay(100, ct);
            var products = new[]
            {
                new { Code="P001", Name="Widget A",  Unit="pcs", Price=10.50m },
                new { Code="P002", Name="Gadget B",  Unit="pcs", Price=25.00m },
                new { Code="P003", Name="Service C", Unit="hrs", Price=75.00m },
            };
            return products
                .Where(p => p.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(p => new MetaFieldOption
                {
                    Value    = p.Code,
                    Label    = p.Name,
                    SubLabel = $"{p.Unit} · €{p.Price:F2}"
                })
                .ToList();
        }

        public async Task<Dictionary<string, object?>> GetDetailsAsync(string value, CancellationToken ct = default)
        {
            await Task.Delay(60, ct);
            return value switch
            {
                "P001" => new() { ["productName"] = "Widget A",  ["unit"] = "pcs", ["unitPrice"] = 10.50m },
                "P002" => new() { ["productName"] = "Gadget B",  ["unit"] = "pcs", ["unitPrice"] = 25.00m },
                "P003" => new() { ["productName"] = "Service C", ["unit"] = "hrs", ["unitPrice"] = 75.00m },
                _      => new()
            };
        }
    }

    // ── Toolbox factory (designer only) ──────────────────────────────────────
    public static class FieldToolboxFactory
    {
        public static List<FieldToolboxItem> GetStandardFields() => new()
        {
            new() { Type=FieldType.Text,         DisplayName="Text",           Category=FieldCategory.Standard, Description="Single-line text input" },
            new() { Type=FieldType.Number,        DisplayName="Number",         Category=FieldCategory.Standard, Description="Numeric input" },
            new() { Type=FieldType.Date,          DisplayName="Date",           Category=FieldCategory.Standard, Description="Date picker" },
            new() { Type=FieldType.DateTime,      DisplayName="Date & Time",    Category=FieldCategory.Standard, Description="Date and time picker" },
            new() { Type=FieldType.Checkbox,      DisplayName="Checkbox",       Category=FieldCategory.Standard, Description="Boolean toggle" },
            new() { Type=FieldType.ListOfValues,  DisplayName="List of Values", Category=FieldCategory.Standard, Description="Dropdown from fixed options" },
            new() { Type=FieldType.TextArea,      DisplayName="Text Area",      Category=FieldCategory.Standard, Description="Multi-line text" },
            new() { Type=FieldType.Grid,          DisplayName="Grid",           Category=FieldCategory.Standard, Description="Editable data grid" },
        };

        public static List<FieldToolboxItem> GetAdvancedFields() => new()
        {
            new() { Type=FieldType.CustomerLookup, DisplayName="Customer Lookup", Category=FieldCategory.Advanced, Description="Search and select a customer" },
            new() { Type=FieldType.ProductSearch,  DisplayName="Product Search",  Category=FieldCategory.Advanced, Description="Search and select a product" },
            new() { Type=FieldType.UserPicker,     DisplayName="User Picker",     Category=FieldCategory.Advanced, Description="Pick a system user" },
            new() { Type=FieldType.AddressLookup,  DisplayName="Address Lookup",  Category=FieldCategory.Advanced, Description="Address autocomplete" },
            new() { Type=FieldType.CurrencyInput,  DisplayName="Currency Input",  Category=FieldCategory.Advanced, Description="Amount with currency" },
            new() { Type=FieldType.FileUpload,     DisplayName="File Upload",     Category=FieldCategory.Advanced, Description="Attach files" },
        };
    }
}
