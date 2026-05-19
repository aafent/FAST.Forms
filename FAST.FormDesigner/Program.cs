using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using FAST.FormDesigner;
using FAST.FormDesigner.Runtime;
using FAST.FormDesigner.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// ── FAST.FormDesigner.Runtime ─────────────────────────────────────────────────
builder.Services.AddFastForms()
    .WithLayoutRepository<DesignerLayoutRepository>()
    .WithFragmentRepository<DesignerFragmentRepository>()
    .AddMetaFieldService<CustomerLookupService>()
    .AddMetaFieldService<ProductSearchService>();

// ── Designer-specific interfaces (Save/Delete) ────────────────────────────────
builder.Services.AddScoped<IDesignerLayoutRepository,  DesignerLayoutRepository>();
builder.Services.AddScoped<IDesignerFragmentRepository, DesignerFragmentRepository>();

await builder.Build().RunAsync();
