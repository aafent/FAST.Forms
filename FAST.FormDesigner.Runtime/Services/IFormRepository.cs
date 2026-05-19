using System.Text.Json;
using FAST.FormDesigner.Runtime.Models;

namespace FAST.FormDesigner.Runtime.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    //  FORM REPOSITORY ABSTRACTION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Abstraction for loading FormLayout definitions.
    /// Default implementation fetches JSON from wwwroot/layouts/.
    /// Override to load from a database, API, or any other source.
    /// </summary>
    public interface IFormLayoutRepository
    {
        Task<FormLayout?>       GetByIdAsync(string id, CancellationToken ct = default);
        Task<List<FormLayout>>  GetAllAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Abstraction for loading FormFragment definitions.
    /// Default implementation fetches JSON from wwwroot/fragments/.
    /// Override to load from a database, API, or any other source.
    /// </summary>
    public interface IFormFragmentRepository
    {
        Task<FormFragment?>          GetByIdAsync(string id, CancellationToken ct = default);
        Task<List<FragmentSummary>>  GetAllSummariesAsync(CancellationToken ct = default);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DEFAULT HTTP IMPLEMENTATIONS  (wwwroot JSON files)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Default layout repository — fetches from wwwroot/layouts/{id}.json
    /// and wwwroot/layouts/_index.json.
    /// </summary>
    public class HttpFormLayoutRepository : IFormLayoutRepository
    {
        private readonly HttpClient _http;
        private readonly System.Text.Json.JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public HttpFormLayoutRepository(HttpClient http) { _http = http; }

        public async Task<FormLayout?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync($"layouts/{id}.json", ct);
                return JsonSerializer.Deserialize<FormLayout>(json, _opts);
            }
            catch { return null; }
        }

        public async Task<List<FormLayout>> GetAllAsync(CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync("layouts/_index.json", ct);
                return JsonSerializer.Deserialize<List<FormLayout>>(json, _opts) ?? new();
            }
            catch { return new(); }
        }
    }

    /// <summary>
    /// Default fragment repository — fetches from wwwroot/fragments/{id}.json
    /// and wwwroot/fragments/_index.json.
    /// </summary>
    public class HttpFormFragmentRepository : IFormFragmentRepository
    {
        private readonly HttpClient _http;
        private readonly System.Text.Json.JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public HttpFormFragmentRepository(HttpClient http) { _http = http; }

        public async Task<FormFragment?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync($"fragments/{id}.json", ct);
                return JsonSerializer.Deserialize<FormFragment>(json, _opts);
            }
            catch { return null; }
        }

        public async Task<List<FragmentSummary>> GetAllSummariesAsync(CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync("fragments/_index.json", ct);
                return JsonSerializer.Deserialize<List<FragmentSummary>>(json, _opts) ?? new();
            }
            catch { return new(); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FRAGMENT CACHE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scoped in-memory cache for loaded fragments.
    /// Prevents repeated HTTP requests for the same fragment during one form session.
    /// </summary>
    public class FormFragmentCache
    {
        private readonly IFormFragmentRepository _repo;
        private readonly Dictionary<string, FormFragment> _cache = new();

        public FormFragmentCache(IFormFragmentRepository repo) { _repo = repo; }

        public async Task<FormFragment?> GetAsync(string fragmentId, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(fragmentId, out var cached)) return cached;
            var fragment = await _repo.GetByIdAsync(fragmentId, ct);
            if (fragment is not null) _cache[fragmentId] = fragment;
            return fragment;
        }

        /// <summary>Synchronous cache-only lookup. Returns null if not in cache.
        /// Used by FormRenderer.GetScopes() as a fallback when _fragments not yet loaded.</summary>
        public FormFragment? GetSync(string fragmentId) =>
            _cache.TryGetValue(fragmentId, out var f) ? f : null;

        public void Seed(FormFragment fragment) => _cache[fragment.Id] = fragment;

        public void Invalidate(string fragmentId) => _cache.Remove(fragmentId);
        public void InvalidateAll()               => _cache.Clear();
    }
}
