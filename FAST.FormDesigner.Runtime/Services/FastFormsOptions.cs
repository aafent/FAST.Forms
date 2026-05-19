namespace FAST.FormDesigner.Runtime.Services
{
    /// <summary>
    /// Configuration options for FAST.FormDesigner.Runtime.
    /// Passed to AddFastForms(options => { ... }).
    /// </summary>
    public class FastFormsOptions
    {
        // ── Repository source ─────────────────────────────────────────────────

        /// <summary>
        /// Base URL for loading layouts and fragments via HTTP.
        /// Defaults to the app's base address (wwwroot files).
        /// Override to point to a remote API:
        ///   options.BaseUrl = "https://my-api.com/forms";
        /// </summary>
        public string? BaseUrl { get; set; }

        // ── Remote Metadata API ───────────────────────────────────────────────

        /// <summary>
        /// Base URL of the remote metadata service API.
        /// When set, fields whose MetaServiceKey is not registered locally
        /// will fall back to calling this API.
        ///
        /// Expected endpoints:
        ///   GET {MetadataApiUrl}/meta/{serviceKey}/search?q={query}
        ///   GET {MetadataApiUrl}/meta/{serviceKey}/details/{value}
        /// </summary>
        public string? MetadataApiUrl { get; set; }

        /// <summary>
        /// Optional API key sent as Authorization: Bearer {MetadataApiKey}
        /// when calling the remote metadata service.
        /// </summary>
        public string? MetadataApiKey { get; set; }

        /// <summary>
        /// Optional additional headers to include in remote metadata API calls.
        /// </summary>
        public Dictionary<string, string> MetadataApiHeaders { get; set; } = new();

        /// <summary>
        /// Timeout in seconds for remote metadata API calls. Default: 10.
        /// </summary>
        public int MetadataApiTimeoutSeconds { get; set; } = 10;

        // ── Behaviour ─────────────────────────────────────────────────────────

        /// <summary>
        /// When true, logs warnings to the browser console when a MetaServiceKey
        /// is not found locally or remotely. Useful during development.
        /// Default: true in Development, false in Production.
        /// </summary>
        public bool WarnOnMissingMetaService { get; set; } = true;

        /// <summary>
        /// When true, shows a visible error placeholder when a fragment JSON
        /// cannot be loaded. When false, renders nothing silently.
        /// Default: true.
        /// </summary>
        public bool ShowFragmentLoadErrors { get; set; } = true;

        // ── Internal ──────────────────────────────────────────────────────────

        internal bool HasMetadataApi => !string.IsNullOrWhiteSpace(MetadataApiUrl);
    }
}
