using System.Net.Http.Json;
using FAST.FormDesigner.Runtime.Models;

namespace FAST.FormDesigner.Runtime.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    //  REMOTE METADATA SERVICE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Internal fallback IMetaFieldService implementation that calls the
    /// remote metadata API configured via FastFormsOptions.MetadataApiUrl.
    ///
    /// This is NOT registered directly — it is used by MetaFieldResolver
    /// when no local service is found for a given MetaServiceKey.
    ///
    /// Remote API contract:
    ///   GET {MetadataApiUrl}/meta/{serviceKey}/search?q={query}
    ///   → [ { value, label, subLabel, badgeText, badgeColor } ]
    ///
    ///   GET {MetadataApiUrl}/meta/{serviceKey}/details/{value}
    ///   → { fieldKey: value, ... }
    /// </summary>
    internal class RemoteMetaFieldService : IMetaFieldService
    {
        private readonly HttpClient     _http;
        private readonly FastFormsOptions _options;
        private readonly string         _serviceKey;

        public string ServiceKey => _serviceKey;

        public RemoteMetaFieldService(HttpClient http, FastFormsOptions options, string serviceKey)
        {
            _http       = http;
            _options    = options;
            _serviceKey = serviceKey;
        }

        public async Task<List<MetaFieldOption>> SearchAsync(string query, CancellationToken ct = default)
        {
            try
            {
                var url      = BuildUrl($"meta/{_serviceKey}/search", $"q={Uri.EscapeDataString(query)}");
                var request  = BuildRequest(HttpMethod.Get, url);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.MetadataApiTimeoutSeconds));
                var response = await _http.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<List<MetaFieldOption>>(cancellationToken: cts.Token);
                return result ?? new();
            }
            catch (OperationCanceledException) { return new(); }
            catch { return new(); }
        }

        public async Task<Dictionary<string, object?>> GetDetailsAsync(string value, CancellationToken ct = default)
        {
            try
            {
                var url      = BuildUrl($"meta/{_serviceKey}/details/{Uri.EscapeDataString(value)}");
                var request  = BuildRequest(HttpMethod.Get, url);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.MetadataApiTimeoutSeconds));
                var response = await _http.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                var result = await response.Content
                    .ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: cts.Token);
                return result ?? new();
            }
            catch { return new(); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string BuildUrl(string path, string? query = null)
        {
            var baseUrl = _options.MetadataApiUrl!.TrimEnd('/');
            var url = $"{baseUrl}/{path}";
            if (!string.IsNullOrWhiteSpace(query)) url += $"?{query}";
            return url;
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            // Bearer token
            if (!string.IsNullOrWhiteSpace(_options.MetadataApiKey))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.MetadataApiKey);

            // Additional headers
            foreach (var (key, val) in _options.MetadataApiHeaders)
                request.Headers.TryAddWithoutValidation(key, val);

            return request;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UPDATED META FIELD RESOLVER
    //  (with remote API fallback)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves IMetaFieldService by ServiceKey.
    /// Resolution order:
    ///   1. Local registered IMetaFieldService implementations (highest priority)
    ///   2. Remote metadata API (if MetadataApiUrl is configured)
    ///   3. null — field renders as plain text input
    /// </summary>
    public class MetaFieldResolver
    {
        private readonly Dictionary<string, IMetaFieldService> _local;
        private readonly HttpClient?      _http;
        private readonly FastFormsOptions _options;

        public MetaFieldResolver(
            IEnumerable<IMetaFieldService> localServices,
            FastFormsOptions               options,
            HttpClient?                    http = null)
        {
            _local   = localServices.ToDictionary(s => s.ServiceKey, StringComparer.OrdinalIgnoreCase);
            _options = options;
            _http    = http;
        }

        /// <summary>
        /// Returns the service for the given key, or null if not found anywhere.
        /// Creates a RemoteMetaFieldService on-the-fly when the key is not local
        /// but MetadataApiUrl is configured.
        /// </summary>
        public IMetaFieldService? Resolve(string serviceKey)
        {
            // 1. Local first
            if (_local.TryGetValue(serviceKey, out var local)) return local;

            // 2. Remote API fallback
            if (_options.HasMetadataApi && _http is not null)
                return new RemoteMetaFieldService(_http, _options, serviceKey);

            // 3. Not found
            return null;
        }

        public bool HasLocal(string serviceKey) => _local.ContainsKey(serviceKey);
    }
}
