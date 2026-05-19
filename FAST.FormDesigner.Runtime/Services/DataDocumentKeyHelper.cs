using System.Text.RegularExpressions;

namespace FAST.FormDesigner.Runtime.Services
{
    /// <summary>
    /// Single responsibility: parse and build Data Document key strings.
    ///
    /// Notation mirrors the JSON Data Document format exactly:
    ///
    ///   "customerCode"          → flat root field     (no DataKey)
    ///   "homeAddress.street"    → nested object field  (DataKey = "homeAddress")
    ///   "contacts[0].name"      → repeating row field  (DataKey = "contacts", rowIndex = 0)
    ///
    /// If the notation ever needs to change (e.g. "contacts.0.name"), change it here only.
    /// </summary>
    public static class DataDocumentKeyHelper
    {
        // Matches "contacts[0].name" → groups: dataKey="contacts", rowIndex=0, fieldKey="name"
        private static readonly Regex _repeatingPattern =
            new(@"^(?<dataKey>[^.\[]+)\[(?<row>\d+)\]\.(?<field>.+)$",
                RegexOptions.Compiled);

        // Matches "homeAddress.street" → groups: dataKey="homeAddress", fieldKey="street"
        private static readonly Regex _nestedPattern =
            new(@"^(?<dataKey>[^.\[]+)\.(?<field>.+)$",
                RegexOptions.Compiled);

        // ─────────────────────────────────────────────────────────────────────
        //  PARSED KEY RESULT
        // ─────────────────────────────────────────────────────────────────────

        public enum KeyKind { Flat, Nested, Repeating }

        public sealed class ParsedKey
        {
            /// <summary>How this key maps to the Data Document structure.</summary>
            public KeyKind Kind       { get; init; }

            /// <summary>
            /// For Flat:      null  (instanceKey must be resolved from layout scopes)
            /// For Nested:    the DataKey / instanceKey (e.g. "homeAddress")
            /// For Repeating: the DataKey / instanceKey (e.g. "contacts")
            /// </summary>
            public string? DataKey   { get; init; }

            /// <summary>The field's own key (e.g. "street", "name", "customerCode").</summary>
            public string  FieldKey  { get; init; } = string.Empty;

            /// <summary>Row index for Repeating keys. 0 for Flat and Nested.</summary>
            public int     RowIndex  { get; init; }

            public bool IsFlat      => Kind == KeyKind.Flat;
            public bool IsNested    => Kind == KeyKind.Nested;
            public bool IsRepeating => Kind == KeyKind.Repeating;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PARSE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a Data Document key string into its structural components.
        /// Returns null if the key is null or empty.
        /// </summary>
        public static ParsedKey? Parse(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            // Try repeating first: "contacts[0].name"
            var rm = _repeatingPattern.Match(key);
            if (rm.Success)
            {
                return new ParsedKey
                {
                    Kind     = KeyKind.Repeating,
                    DataKey  = rm.Groups["dataKey"].Value,
                    FieldKey = rm.Groups["field"].Value,
                    RowIndex = int.Parse(rm.Groups["row"].Value)
                };
            }

            // Try nested: "homeAddress.street"
            var nm = _nestedPattern.Match(key);
            if (nm.Success)
            {
                return new ParsedKey
                {
                    Kind     = KeyKind.Nested,
                    DataKey  = nm.Groups["dataKey"].Value,
                    FieldKey = nm.Groups["field"].Value,
                    RowIndex = 0
                };
            }

            // Flat root: "customerCode"
            return new ParsedKey
            {
                Kind     = KeyKind.Flat,
                DataKey  = null,
                FieldKey = key,
                RowIndex = 0
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BUILD
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Builds a flat root key: "customerCode"</summary>
        public static string BuildFlat(string fieldKey) => fieldKey;

        /// <summary>Builds a nested key: "homeAddress.street"</summary>
        public static string BuildNested(string dataKey, string fieldKey) =>
            $"{dataKey}.{fieldKey}";

        /// <summary>Builds a repeating row key: "contacts[0].name"</summary>
        public static string BuildRepeating(string dataKey, int rowIndex, string fieldKey) =>
            $"{dataKey}[{rowIndex}].{fieldKey}";

        /// <summary>
        /// Builds the correct key string from a DataDocumentScope + fieldKey + optional rowIndex.
        /// Mirrors exactly what GetDataDocument produces in FormStateContainer.
        /// </summary>
        public static string Build(string? dataKey, string fieldKey,
                                   bool isRepeating = false, int rowIndex = 0)
        {
            if (isRepeating && !string.IsNullOrWhiteSpace(dataKey))
                return BuildRepeating(dataKey!, rowIndex, fieldKey);

            if (!string.IsNullOrWhiteSpace(dataKey))
                return BuildNested(dataKey!, fieldKey);

            return BuildFlat(fieldKey);
        }
    }
}
