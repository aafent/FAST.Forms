using Microsoft.Extensions.DependencyInjection;
using FAST.FormDesigner.Runtime.Services;
using FAST.FormDesigner.Runtime.Validators;

namespace FAST.FormDesigner.Runtime
{
    // ─────────────────────────────────────────────────────────────────────────
    //  SERVICE COLLECTION EXTENSIONS
    // ─────────────────────────────────────────────────────────────────────────

    public static class FastFormsServiceExtensions
    {
        /// <summary>
        /// Registers all FAST.FormDesigner.Runtime services.
        ///
        /// Usage in Program.cs:
        ///
        ///   // Minimal — loads layouts/fragments from wwwroot JSON files
        ///   builder.Services.AddFastForms();
        ///
        ///   // With remote metadata API
        ///   builder.Services.AddFastForms(options => {
        ///       options.MetadataApiUrl = "https://your-api.com/forms";
        ///       options.MetadataApiKey = "your-api-key";
        ///   });
        ///
        ///   // With custom repository (database, API, etc.)
        ///   builder.Services.AddFastForms()
        ///       .WithLayoutRepository<MyLayoutRepository>()
        ///       .WithFragmentRepository<MyFragmentRepository>();
        ///
        ///   // With local meta-field service implementations
        ///   builder.Services.AddFastForms()
        ///       .AddMetaFieldService<CustomerLookupService>()
        ///       .AddMetaFieldService<ProductSearchService>();
        /// </summary>
        public static FastFormsBuilder AddFastForms(
            this IServiceCollection services,
            Action<FastFormsOptions>? configure = null)
        {
            // Options
            var options = new FastFormsOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            // Default repositories (HTTP / wwwroot JSON)
            services.AddScoped<IFormLayoutRepository,   HttpFormLayoutRepository>();
            services.AddScoped<IFormFragmentRepository, HttpFormFragmentRepository>();

            // Fragment cache (scoped per form session)
            services.AddScoped<FormFragmentCache>();

            // Form state (scoped per form session)
            services.AddScoped<FormStateContainer>();

            // Validation
            services.AddScoped<FormValidationService>();

            // Meta resolver (scoped — collects all registered IMetaFieldService)
            services.AddScoped<MetaFieldResolver>(sp =>
            {
                var localServices = sp.GetServices<IMetaFieldService>();
                var opts          = sp.GetRequiredService<FastFormsOptions>();

                // Use the registered HttpClient for remote metadata API
                HttpClient? http = null;
                if (opts.HasMetadataApi)
                    http = sp.GetService<HttpClient>();

                return new MetaFieldResolver(localServices, opts, http);
            });

            return new FastFormsBuilder(services);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BUILDER  (fluent API for chaining registrations)
    // ─────────────────────────────────────────────────────────────────────────

    public class FastFormsBuilder
    {
        private readonly IServiceCollection _services;

        internal FastFormsBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Replaces the default HTTP layout repository with a custom implementation.
        /// Use this to load layouts from a database, API, or any other source.
        /// </summary>
        public FastFormsBuilder WithLayoutRepository<TRepository>()
            where TRepository : class, IFormLayoutRepository
        {
            // Remove default and register custom
            var descriptor = _services.FirstOrDefault(d =>
                d.ServiceType == typeof(IFormLayoutRepository));
            if (descriptor is not null) _services.Remove(descriptor);
            _services.AddScoped<IFormLayoutRepository, TRepository>();
            return this;
        }

        /// <summary>
        /// Replaces the default HTTP fragment repository with a custom implementation.
        /// </summary>
        public FastFormsBuilder WithFragmentRepository<TRepository>()
            where TRepository : class, IFormFragmentRepository
        {
            var descriptor = _services.FirstOrDefault(d =>
                d.ServiceType == typeof(IFormFragmentRepository));
            if (descriptor is not null) _services.Remove(descriptor);
            _services.AddScoped<IFormFragmentRepository, TRepository>();
            return this;
        }

        /// <summary>
        /// Registers a local IMetaFieldService implementation.
        /// Local services take priority over the remote metadata API.
        /// Multiple services can be registered for different MetaServiceKey values.
        /// </summary>
        public FastFormsBuilder AddMetaFieldService<TService>()
            where TService : class, IMetaFieldService
        {
            _services.AddScoped<IMetaFieldService, TService>();
            return this;
        }

        /// <summary>
        /// Registers a local IMetaFieldService using a factory function.
        /// </summary>
        public FastFormsBuilder AddMetaFieldService<TService>(
            Func<IServiceProvider, TService> factory)
            where TService : class, IMetaFieldService
        {
            _services.AddScoped<IMetaFieldService, TService>(factory);
            return this;
        }
    }
}
