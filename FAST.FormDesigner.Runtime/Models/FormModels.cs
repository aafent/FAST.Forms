using System.Text.Json.Serialization;

namespace FAST.FormDesigner.Runtime.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    //  ENUMERATIONS
    // ─────────────────────────────────────────────────────────────────────────

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FieldType
    {
        // Standard fields
        Text,
        Number,
        Date,
        DateTime,
        Checkbox,
        ListOfValues,
        TextArea,
        // Advanced / Meta fields
        CustomerLookup,
        ProductSearch,
        UserPicker,
        AddressLookup,
        CurrencyInput,
        Grid,
        FileUpload
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnSpan { One = 1, Two = 2, Three = 3, Four = 4 }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContainerType { Area, Group, TabControl, TabPage }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValidationRuleType
    {
        Required,
        MinLength,
        MaxLength,
        Min,
        Max,
        Regex,
        Custom,
        CrossFragment   // reads a value from another fragment via FormStateContainer
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FieldCategory { Standard, Advanced }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RepeatingViewMode { Grid, Card }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORM LAYOUT  (Editor A output)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root document saved by Editor A.
    /// Serialises to / from  wwwroot/layouts/{id}.json
    /// </summary>
    public class FormLayout
    {
        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public string Name        { get; set; } = "New Layout";
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Top-level containers (Areas, TabControls, Groups).</summary>
        public List<LayoutNode> Containers { get; set; } = new();
    }

    /// <summary>
    /// A node in the layout tree.  Can nest other containers recursively.
    /// </summary>
    public class LayoutNode
    {
        public string          Id            { get; set; } = Guid.NewGuid().ToString();
        public string          Label         { get; set; } = string.Empty;
        public ContainerType   Type          { get; set; } = ContainerType.Area;
        public int             Order         { get; set; }
        public ColumnSpan      ColumnSpan    { get; set; } = ColumnSpan.Four;

        /// <summary>
        /// Recursive child containers.
        /// A TabControl contains TabPages; a Group contains Areas.
        /// </summary>
        public List<LayoutNode> Children { get; set; } = new();

        /// <summary>
        /// Only populated when Type == Area.
        /// Points to a FormFragment defined in Editor B.
        /// </summary>
        public LayoutArea? Area { get; set; }
    }

    /// <summary>
    /// The leaf node of the layout tree.
    /// Holds exactly one fragment placeholder.
    /// </summary>
    public class LayoutArea
    {
        public string  Id                 { get; set; } = Guid.NewGuid().ToString();
        public string  Label              { get; set; } = string.Empty;
        public ColumnSpan ColumnSpan      { get; set; } = ColumnSpan.Four;

        /// <summary>
        /// The key integration point.
        /// Set in Editor A when the designer assigns a Fragment to this Area.
        /// Null = placeholder (unassigned).
        /// </summary>
        public string? AssignedFragmentId   { get; set; }

        /// <summary>Stored alongside the ID so the name is available even when the fragment file is missing.</summary>
        public string? AssignedFragmentName { get; set; }

        /// <summary>
        /// The key used in the Data Document for this area's fragment data.
        /// If set: fields nest under this key → { "homeAddress": { "street": ... } }
        /// If empty: fields are flat at the root → { "street": ... }
        /// For repeating fragments: produces an array → { "contacts": [ {...}, {...} ] }
        /// Required for repeating fragments to avoid key collisions.
        /// </summary>
        public string? DataKey { get; set; }

        /// <summary>Display hint shown in the designer when no fragment is assigned.</summary>
        public string  PlaceholderHint    { get; set; } = "Drop a Fragment here";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORM FRAGMENT  (Editor B output)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root document saved by Editor B.
    /// Serialises to / from  wwwroot/fragments/{id}.json
    /// </summary>
    public class FormFragment
    {
        public string   Id          { get; set; } = Guid.NewGuid().ToString();
        public string   Name        { get; set; } = "New Fragment";
        public string   Description { get; set; } = string.Empty;
        public string   Category    { get; set; } = string.Empty; // e.g. "Master Data", "Address"
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;

        public List<FieldDefinition> Fields { get; set; } = new();

        // ── Repeating fragment properties ──────────────────────────────────
        /// <summary>When true, renders as a multi-row repeating grid or card list.</summary>
        public bool               IsRepeating { get; set; } = false;
        /// <summary>Grid = compact table rows, Card = full field cards stacked.</summary>
        public RepeatingViewMode  ViewMode    { get; set; } = RepeatingViewMode.Grid;
        /// <summary>Minimum number of rows. 0 = empty start.</summary>
        public int                MinRows     { get; set; } = 1;
    }

    /// <summary>
    /// A single field inside a Fragment.
    /// </summary>
    public class FieldDefinition
    {
        public string        Id           { get; set; } = Guid.NewGuid().ToString();
        public string        Key          { get; set; } = string.Empty;   // data key, e.g. "customerCode"
        public string        Label        { get; set; } = string.Empty;
        public FieldType     Type         { get; set; } = FieldType.Text;
        public FieldCategory Category     { get; set; } = FieldCategory.Standard;
        public int           Order        { get; set; }
        public ColumnSpan    ColumnSpan   { get; set; } = ColumnSpan.Two;
        public bool          IsReadOnly   { get; set; }
        public bool          IsVisible    { get; set; } = true;
        public string        Placeholder  { get; set; } = string.Empty;
        public string        HintText     { get; set; } = string.Empty;   // guideline shown below field
        public string?       DefaultValue { get; set; }

        /// <summary>For ListOfValues: pipe-separated options, e.g. "A|B|C"</summary>
        public string?       ListOptions  { get; set; }

        /// <summary>
        /// For meta-fields: the service key that IMetaFieldService resolves.
        /// E.g. "CustomerLookup", "ProductSearch"
        /// </summary>
        public string?       MetaServiceKey { get; set; }

        /// <summary>
        /// For Grid fields: the column definitions of the embedded grid.
        /// </summary>
        public List<GridColumnDefinition>? GridColumns { get; set; }

        public List<ValidationRule> ValidationRules { get; set; } = new();
    }

    /// <summary>
    /// Column definition for Grid-type fields.
    /// </summary>
    public class GridColumnDefinition
    {
        public string    Key       { get; set; } = string.Empty;
        public string    Header    { get; set; } = string.Empty;
        public FieldType Type      { get; set; } = FieldType.Text;
        public int       Width     { get; set; } = 150;  // px
        public bool      IsReadOnly { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  VALIDATION RULES
    // ─────────────────────────────────────────────────────────────────────────

    public class ValidationRule
    {
        public string             Id          { get; set; } = Guid.NewGuid().ToString();
        public ValidationRuleType Type        { get; set; }
        public string?            Value       { get; set; }   // e.g. "5" for MinLength
        public string             Message     { get; set; } = string.Empty;

        /// <summary>
        /// For CrossFragment rules: the other fragment's ID and field key.
        /// Format: "{fragmentId}:{fieldKey}"
        /// </summary>
        public string?            CrossFragmentRef { get; set; }

        /// <summary>
        /// For CrossFragment rules: the condition operator.
        /// E.g. "NotEmpty", "Equals:GOLD", "GreaterThan:5000"
        /// </summary>
        public string?            CrossFragmentCondition { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RUNTIME VALUE MODEL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents one field's runtime value in FormStateContainer.
    /// Key pattern: "{fragmentId}:{fieldKey}"
    /// </summary>
    public class FieldValue
    {
        public string  FragmentId { get; set; } = string.Empty;
        public string  FieldKey   { get; set; } = string.Empty;
        public object? Value      { get; set; }
        public bool    IsDirty    { get; set; }

        public string StateKey => $"{FragmentId}:{FieldKey}";
    }

    /// <summary>
    /// A single row in a Grid field.
    /// Dictionary key = column Key from GridColumnDefinition.
    /// </summary>
    public class GridRow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Dictionary<string, object?> Values { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DESIGNER HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Lightweight catalogue entry shown in the Fragment picker.</summary>
    public class FragmentSummary
    {
        public string Id          { get; set; } = string.Empty;
        public string Name        { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category    { get; set; } = string.Empty;
        public int    FieldCount  { get; set; }
    }

    /// <summary>Stencil toolbox item for the Fragment Designer.</summary>
    public class FieldToolboxItem
    {
        public FieldType     Type        { get; set; }
        public string        DisplayName { get; set; } = string.Empty;
        public string        Icon        { get; set; } = string.Empty;  // SVG path data
        public FieldCategory Category    { get; set; } = FieldCategory.Standard;
        public string        Description { get; set; } = string.Empty;
    }

    /// <summary>Validation error surfaced to the UI.</summary>
    public class FieldValidationError
    {
        public string FragmentId { get; set; } = string.Empty;
        public string FieldKey   { get; set; } = string.Empty;
        public string Message    { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FIELD CHANGE INTERCEPTOR
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The "coordinates" of a user-initiated field change.
    /// Passed to OnFieldChanging on FastForm.
    /// </summary>
    public class FieldChangingArgs
    {
        /// <summary>State scope key — DataKey if set, otherwise FragmentId.</summary>
        public string  InstanceKey { get; init; } = string.Empty;

        /// <summary>The DataKey from AreaDefinition, if set. Null for flat/root areas.</summary>
        public string? DataKey     { get; init; }

        /// <summary>The field's data key, e.g. "customerCode".</summary>
        public string  FieldKey    { get; init; } = string.Empty;

        /// <summary>Row index within a repeating fragment. 0 for non-repeating fields.</summary>
        public int     RowIndex    { get; init; }

        /// <summary>The value the field held before the user changed it.</summary>
        public object? OldValue    { get; init; }

        /// <summary>The new value the user has entered.</summary>
        public object? NewValue    { get; init; }
    }

    /// <summary>
    /// How a Rejected field change should behave in the UI.
    /// </summary>
    public enum RejectMode
    {
        /// <summary>Revert the field to its old value. User can retype freely.</summary>
        SnapBack,

        /// <summary>
        /// Keep focus on the field and prevent the user from leaving until an
        /// acceptable value is entered. Falls back to SnapBack for field types
        /// where focus-lock makes no UX sense (Checkbox, ListOfValues, Date, DateTime).
        /// </summary>
        LockFocus
    }

    /// <summary>
    /// The outcome returned by the OnFieldChanging interceptor.
    /// Use the static factory methods: Accepted(), Error(message), Rejected(mode).
    /// </summary>
    public class FieldChangingResult
    {
        public enum Outcome { Accepted, Error, Rejected }

        public Outcome    Result       { get; private init; }
        public string?    ErrorMessage { get; private init; }
        public RejectMode RejectMode   { get; private init; }

        private FieldChangingResult() { }

        /// <summary>Accept the new value with no feedback.</summary>
        public static FieldChangingResult Accepted() =>
            new() { Result = Outcome.Accepted };

        /// <summary>Accept the new value but display an error message on the field.</summary>
        public static FieldChangingResult Error(string message) =>
            new() { Result = Outcome.Error, ErrorMessage = message };

        /// <summary>Reject the change. SnapBack reverts silently; LockFocus keeps focus until fixed.</summary>
        public static FieldChangingResult Rejected(RejectMode mode = RejectMode.SnapBack) =>
            new() { Result = Outcome.Rejected, RejectMode = mode };
    }
}
