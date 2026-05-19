namespace FAST.FormDesigner.Runtime.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    //  META-FIELD SERVICE INTERFACE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Contract for advanced / API-backed fields.
    /// Each implementation handles one MetaServiceKey value.
    /// Register implementations via AddFastForms().AddMetaFieldService<T>().
    /// Unregistered keys fall back to the remote metadata API if configured.
    /// </summary>
    public interface IMetaFieldService
    {
        /// <summary>Matches FieldDefinition.MetaServiceKey. E.g. "CustomerLookup"</summary>
        string ServiceKey { get; }

        /// <summary>Returns search suggestions as the user types.</summary>
        Task<List<MetaFieldOption>> SearchAsync(string query, CancellationToken ct = default);

        /// <summary>
        /// Returns full details for a selected value.
        /// Keys in the returned dictionary match FieldDefinition.Key values
        /// in the same fragment — used to auto-fill sibling fields.
        /// </summary>
        Task<Dictionary<string, object?>> GetDetailsAsync(string value, CancellationToken ct = default);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  META FIELD OPTION
    // ─────────────────────────────────────────────────────────────────────────

    public class MetaFieldOption
    {
        public string  Value       { get; set; } = string.Empty;
        public string  Label       { get; set; } = string.Empty;
        public string? SubLabel    { get; set; }
        public string? BadgeText   { get; set; }
        public string? BadgeColor  { get; set; }
    }
}
