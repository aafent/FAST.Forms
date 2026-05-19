using FAST.FormDesigner.Runtime.Models;

namespace FAST.FormDesigner.Runtime.Services
{
    /// <summary>
    /// Describes one area's contribution to the Data Document.
    /// Built by FormRenderer when walking the layout tree.
    /// </summary>
    public class DataDocumentScope
    {
        /// <summary>The state key scope — DataKey if set, otherwise FragmentId.</summary>
        public string       InstanceKey { get; init; } = string.Empty;

        /// <summary>The DataKey from AreaDefinition — determines nesting in the output.</summary>
        public string?      DataKey     { get; init; }

        /// <summary>All field keys belonging to this fragment.</summary>
        public List<string> FieldKeys   { get; init; } = new();

        /// <summary>True if this is a repeating fragment — output is an array.</summary>
        public bool         IsRepeating { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORM STATE CONTAINER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scoped service that acts as the single source of truth for all field
    /// values across all fragments in the currently active form.
    ///
    /// Key pattern: "{fragmentId}:{fieldKey}"
    /// Example:     "fragment-masterdata:customerCode"
    ///
    /// Registered as Scoped in Program.cs so it lives for the lifetime of
    /// the form session (one navigation = one form fill).
    /// </summary>
    public class FormStateContainer
    {
        // ── Internal store ────────────────────────────────────────────────────
        private readonly Dictionary<string, object?> _values  = new();
        private readonly Dictionary<string, List<FieldValidationError>> _errors = new();

        // ── Active layout / fragment tracking ────────────────────────────────
        public string? ActiveLayoutId  { get; private set; }
        public bool    IsSubmitting    { get; private set; }
        public bool    HasBeenSubmitted { get; private set; }

        // ── Change notifications ──────────────────────────────────────────────

        /// <summary>Fires whenever any field value changes. Payload = state key.</summary>
        public event Action<string>? OnFieldChanged;

        /// <summary>Fires when validation state changes.</summary>
        public event Action? OnValidationChanged;

        /// <summary>Fires when the whole form state is reset.</summary>
        public event Action? OnFormReset;

        // ─────────────────────────────────────────────────────────────────────
        //  INITIALISATION
        // ─────────────────────────────────────────────────────────────────────

        public void InitialiseForm(string layoutId)
        {
            ActiveLayoutId   = layoutId;
            HasBeenSubmitted = false;
            _values.Clear();
            _errors.Clear();
            OnFormReset?.Invoke();
        }

        /// <summary>Re-initialises without clearing existing values. Used by the Simulator
        /// to preserve generated/loaded data when the form component recreates.</summary>
        public void InitialiseFormKeepValues(string layoutId)
        {
            ActiveLayoutId   = layoutId;
            HasBeenSubmitted = false;
            _errors.Clear();
            // _values intentionally NOT cleared
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALUE ACCESS
        // ─────────────────────────────────────────────────────────────────────

        public static string MakeKey(string fragmentId, string fieldKey) =>
            $"{fragmentId}:{fieldKey}";

        /// <summary>Makes a state key using instanceKey (DataKey or fragmentId) as scope.</summary>
        public static string MakeInstanceKey(string instanceKey, string fieldKey) =>
            $"{instanceKey}:{fieldKey}";

        // ── Instance-key aware methods (use DataKey as scope) ────────────────────

        public void SetInstanceValue(string instanceKey, string fieldKey, object? value)
        {
            var key = MakeInstanceKey(instanceKey, fieldKey);
            _values[key] = value;
            OnFieldChanged?.Invoke(key);
        }

        public object? GetInstanceValue(string instanceKey, string fieldKey)
        {
            var key = MakeInstanceKey(instanceKey, fieldKey);
            return _values.TryGetValue(key, out var val) ? val : null;
        }

        public T? GetInstanceValue<T>(string instanceKey, string fieldKey)
        {
            var raw = GetInstanceValue(instanceKey, fieldKey);
            if (raw is T typed) return typed;
            if (raw is string s) { try { return (T)Convert.ChangeType(s, typeof(T)); } catch { } }
            return default;
        }

        public bool HasInstanceErrors(string instanceKey, string fieldKey) =>
            _errors.ContainsKey(MakeInstanceKey(instanceKey, fieldKey));

        public List<FieldValidationError> GetInstanceErrors(string instanceKey, string fieldKey)
        {
            var key = MakeInstanceKey(instanceKey, fieldKey);
            return _errors.TryGetValue(key, out var errs) ? errs : new();
        }

        // ── Row-aware instance methods ────────────────────────────────────────────

        private static string RowInstanceKey(string instanceKey, int rowIndex, string fieldKey) =>
            $"{instanceKey}:row:{rowIndex}:{fieldKey}";

        private static string RowInstanceCountKey(string instanceKey) =>
            $"{instanceKey}:__rowcount";

        public int GetInstanceRowCount(string instanceKey)
        {
            var key = RowInstanceCountKey(instanceKey);
            return _values.TryGetValue(key, out var v) && v is int i ? i : 0;
        }

        public void InitialiseInstanceRows(string instanceKey, int minRows)
        {
            var key = RowInstanceCountKey(instanceKey);
            if (!_values.ContainsKey(key))
            {
                _values[key] = minRows;
                OnFieldChanged?.Invoke(key);
            }
        }

        public int AddInstanceRow(string instanceKey)
        {
            var count = GetInstanceRowCount(instanceKey);
            _values[RowInstanceCountKey(instanceKey)] = count + 1;
            OnFieldChanged?.Invoke(RowInstanceCountKey(instanceKey));
            return count;
        }

        public void RemoveInstanceRow(string instanceKey, int rowIndex)
        {
            var count = GetInstanceRowCount(instanceKey);
            if (count <= 0) return;
            for (int i = rowIndex; i < count - 1; i++)
            {
                var keysToShift = _values.Keys
                    .Where(k => k.StartsWith($"{instanceKey}:row:{i + 1}:"))
                    .ToList();
                foreach (var k in keysToShift)
                {
                    var fieldKey = k[$"{instanceKey}:row:{i + 1}:".Length..];
                    _values[RowInstanceKey(instanceKey, i, fieldKey)] = _values[k];
                    _values.Remove(k);
                }
            }
            var lastRowKeys = _values.Keys
                .Where(k => k.StartsWith($"{instanceKey}:row:{count - 1}:"))
                .ToList();
            foreach (var k in lastRowKeys) _values.Remove(k);
            _values[RowInstanceCountKey(instanceKey)] = count - 1;
            OnFieldChanged?.Invoke(RowInstanceCountKey(instanceKey));
        }

        public void SetInstanceRowValue(string instanceKey, int rowIndex, string fieldKey, object? value)
        {
            var key = RowInstanceKey(instanceKey, rowIndex, fieldKey);
            _values[key] = value;
            OnFieldChanged?.Invoke(key);
        }

        public object? GetInstanceRowValue(string instanceKey, int rowIndex, string fieldKey)
        {
            var key = RowInstanceKey(instanceKey, rowIndex, fieldKey);
            return _values.TryGetValue(key, out var v) ? v : null;
        }

        public void SetValue(string fragmentId, string fieldKey, object? value)
        {
            var key = MakeKey(fragmentId, fieldKey);
            _values[key] = value;
            OnFieldChanged?.Invoke(key);
        }

        public object? GetValue(string fragmentId, string fieldKey)
        {
            var key = MakeKey(fragmentId, fieldKey);
            return _values.TryGetValue(key, out var val) ? val : null;
        }

        public T? GetValue<T>(string fragmentId, string fieldKey)
        {
            var raw = GetValue(fragmentId, fieldKey);
            if (raw is T typed) return typed;
            if (raw is string s && typeof(T) != typeof(string))
            {
                try { return (T)Convert.ChangeType(s, typeof(T)); } catch { }
            }
            return default;
        }

        public bool HasValue(string fragmentId, string fieldKey)
        {
            var key = MakeKey(fragmentId, fieldKey);
            return _values.TryGetValue(key, out var val) && val is not null &&
                   !(val is string str && string.IsNullOrWhiteSpace(str));
        }

        /// <summary>
        /// Returns the entire state as a flat dictionary.
        /// This is the payload sent on form submit.
        /// </summary>
        public Dictionary<string, object?> GetSubmitPayload() =>
            new Dictionary<string, object?>(_values);

        /// <summary>
        /// Builds a structured Data Document from the current state.
        /// Areas with DataKey → nested object or array (repeating).
        /// Areas without DataKey → flat fields at root.
        /// </summary>
        public Dictionary<string, object?> GetDataDocument(IEnumerable<DataDocumentScope> scopes)
        {
            var root = new Dictionary<string, object?>();

            foreach (var scope in scopes)
            {
                if (scope.IsRepeating)
                {
                    // Build array of row objects
                    var instanceKey = scope.InstanceKey;
                    var rowCount    = GetInstanceRowCount(instanceKey);
                    var rows        = new List<Dictionary<string, object?>>();

                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = new Dictionary<string, object?>();
                        foreach (var fieldKey in scope.FieldKeys)
                        {
                            var val = GetInstanceRowValue(instanceKey, i, fieldKey);
                            if (val is not null) row[fieldKey] = val;
                        }
                        rows.Add(row);
                    }

                    if (!string.IsNullOrWhiteSpace(scope.DataKey))
                        root[scope.DataKey] = rows;
                }
                else
                {
                    var instanceKey = scope.InstanceKey;
                    var obj = new Dictionary<string, object?>();
                    foreach (var fieldKey in scope.FieldKeys)
                    {
                        var val = GetInstanceValue(instanceKey, fieldKey);
                        if (val is not null) obj[fieldKey] = val;
                    }

                    if (!string.IsNullOrWhiteSpace(scope.DataKey))
                    {
                        // Nested under DataKey
                        root[scope.DataKey] = obj;
                    }
                    else
                    {
                        // Flat at root
                        foreach (var kv in obj) root[kv.Key] = kv.Value;
                    }
                }
            }

            return root;
        }

        /// <summary>
        /// Returns values for one fragment only, with just the field keys
        /// (without the fragment prefix). Useful for binding to a typed DTO.
        /// </summary>
        public Dictionary<string, object?> GetFragmentValues(string fragmentId)
        {
            var prefix = fragmentId + ":";
            return _values
                .Where(kv => kv.Key.StartsWith(prefix))
                .ToDictionary(kv => kv.Key[prefix.Length..], kv => kv.Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION STATE
        // ─────────────────────────────────────────────────────────────────────

        public void SetErrors(string fragmentId, string fieldKey,
                              IEnumerable<string> messages)
        {
            var key = MakeKey(fragmentId, fieldKey);
            var errors = messages
                .Select(m => new FieldValidationError
                {
                    FragmentId = fragmentId,
                    FieldKey   = fieldKey,
                    Message    = m
                })
                .ToList();

            if (errors.Any())
                _errors[key] = errors;
            else
                _errors.Remove(key);

            OnValidationChanged?.Invoke();
        }

        public void ClearErrors(string fragmentId, string fieldKey)
        {
            var key = MakeKey(fragmentId, fieldKey);
            _errors.Remove(key);
            OnValidationChanged?.Invoke();
        }

        public void ClearAllErrors()
        {
            _errors.Clear();
            OnValidationChanged?.Invoke();
        }

        public List<FieldValidationError> GetErrors(string fragmentId, string fieldKey)
        {
            var key = MakeKey(fragmentId, fieldKey);
            return _errors.TryGetValue(key, out var errs) ? errs : new();
        }

        public List<FieldValidationError> GetAllErrors() =>
            _errors.Values.SelectMany(e => e).ToList();

        public bool HasErrors(string fragmentId, string fieldKey) =>
            GetErrors(fragmentId, fieldKey).Any();

        public bool HasAnyErrors() => _errors.Any();

        // ─────────────────────────────────────────────────────────────────────
        //  REPEATING FRAGMENT ROW HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static string RowKey(string fragmentId, int rowIndex, string fieldKey) =>
            $"{fragmentId}:row:{rowIndex}:{fieldKey}";

        private static string RowCountKey(string fragmentId) =>
            $"{fragmentId}:__rowcount";

        public int GetRowCount(string fragmentId)
        {
            var key = RowCountKey(fragmentId);
            return _values.TryGetValue(key, out var v) && v is int i ? i : 0;
        }

        public void InitialiseRows(string fragmentId, int minRows)
        {
            var key = RowCountKey(fragmentId);
            if (!_values.ContainsKey(key))
            {
                _values[key] = minRows;
                OnFieldChanged?.Invoke(key);
            }
        }

        public int AddRow(string fragmentId)
        {
            var count = GetRowCount(fragmentId);
            _values[RowCountKey(fragmentId)] = count + 1;
            OnFieldChanged?.Invoke(RowCountKey(fragmentId));
            return count; // returns new row index
        }

        public void RemoveRow(string fragmentId, int rowIndex)
        {
            var count = GetRowCount(fragmentId);
            if (count <= 0) return;

            // Shift all values from rowIndex+1 downward
            for (int i = rowIndex; i < count - 1; i++)
            {
                var keysToShift = _values.Keys
                    .Where(k => k.StartsWith($"{fragmentId}:row:{i + 1}:"))
                    .ToList();
                foreach (var k in keysToShift)
                {
                    var fieldKey = k[$"{fragmentId}:row:{i + 1}:".Length..];
                    var newKey   = RowKey(fragmentId, i, fieldKey);
                    _values[newKey] = _values[k];
                    _values.Remove(k);
                }
            }

            // Remove last row's values
            var lastRowKeys = _values.Keys
                .Where(k => k.StartsWith($"{fragmentId}:row:{count - 1}:"))
                .ToList();
            foreach (var k in lastRowKeys) _values.Remove(k);

            _values[RowCountKey(fragmentId)] = count - 1;
            OnFieldChanged?.Invoke(RowCountKey(fragmentId));
        }

        public void SetRowValue(string fragmentId, int rowIndex, string fieldKey, object? value)
        {
            var key = RowKey(fragmentId, rowIndex, fieldKey);
            _values[key] = value;
            OnFieldChanged?.Invoke(key);
        }

        public object? GetRowValue(string fragmentId, int rowIndex, string fieldKey)
        {
            var key = RowKey(fragmentId, rowIndex, fieldKey);
            return _values.TryGetValue(key, out var v) ? v : null;
        }

        public T? GetRowValue<T>(string fragmentId, int rowIndex, string fieldKey)
        {
            var raw = GetRowValue(fragmentId, rowIndex, fieldKey);
            if (raw is T typed) return typed;
            if (raw is string s)
            {
                try { return (T)Convert.ChangeType(s, typeof(T)); } catch { }
            }
            return default;
        }

        public bool HasRowErrors(string fragmentId, int rowIndex, string fieldKey)
        {
            var key = RowKey(fragmentId, rowIndex, fieldKey);
            return _errors.ContainsKey(key);
        }

        public List<FieldValidationError> GetRowErrors(string fragmentId, int rowIndex, string fieldKey)
        {
            var key = RowKey(fragmentId, rowIndex, fieldKey);
            return _errors.TryGetValue(key, out var errs) ? errs : new();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SUBMIT HELPERS
        // ─────────────────────────────────────────────────────────────────────

        public void MarkSubmitting(bool value)
        {
            IsSubmitting     = value;
            HasBeenSubmitted = true;
        }

        public void Reset()
        {
            _values.Clear();
            _errors.Clear();
            IsSubmitting     = false;
            HasBeenSubmitted = false;
            OnFormReset?.Invoke();
        }

        /// <summary>
        /// Clears values and errors without firing OnFormReset.
        /// Used by FastForm.SetValues to avoid triggering FormRenderer re-init.
        /// </summary>
        public void ResetSilent()
        {
            _values.Clear();
            _errors.Clear();
            IsSubmitting     = false;
            HasBeenSubmitted = false;
        }
    }
}
