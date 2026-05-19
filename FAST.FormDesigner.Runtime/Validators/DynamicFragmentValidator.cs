using FluentValidation;
using FluentValidation.Results;
using FAST.FormDesigner.Runtime.Models;
using FAST.FormDesigner.Runtime.Services;

namespace FAST.FormDesigner.Runtime.Validators
{
    // ─────────────────────────────────────────────────────────────────────────
    //  DYNAMIC FRAGMENT VALIDATOR
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds FluentValidation rules dynamically from a FormFragment's
    /// FieldDefinition.ValidationRules at runtime.
    ///
    /// Usage:
    ///   var validator = new DynamicFragmentValidator(fragment, stateContainer);
    ///   var result    = await validator.ValidateAsync(new FragmentValidationContext(...));
    /// </summary>
    public class DynamicFragmentValidator : AbstractValidator<FragmentValidationContext>
    {
        public DynamicFragmentValidator(FormFragment fragment, FormStateContainer state)
        {
            foreach (var field in fragment.Fields)
            {
                foreach (var rule in field.ValidationRules)
                {
                    switch (rule.Type)
                    {
                        // ── Required ──────────────────────────────────────────
                        case ValidationRuleType.Required:
                            RuleFor(ctx => ctx.GetValue(field.Key))
                                .Must(v => v is not null &&
                                           v.ToString() is { } s &&
                                           !string.IsNullOrWhiteSpace(s))
                                .WithName(field.Label)
                                .WithMessage(string.IsNullOrWhiteSpace(rule.Message)
                                    ? $"{field.Label} is required."
                                    : rule.Message);
                            break;

                        // ── Min/Max Length ────────────────────────────────────
                        case ValidationRuleType.MinLength:
                            if (int.TryParse(rule.Value, out var minLen))
                                RuleFor(ctx => ctx.GetValue(field.Key))
                                    .Must(v => v?.ToString()?.Length >= minLen)
                                    .WithName(field.Label)
                                    .WithMessage(string.IsNullOrWhiteSpace(rule.Message)
                                        ? $"{field.Label} must be at least {minLen} characters."
                                        : rule.Message);
                            break;

                        case ValidationRuleType.MaxLength:
                            if (int.TryParse(rule.Value, out var maxLen))
                                RuleFor(ctx => ctx.GetValue(field.Key))
                                    .Must(v => v?.ToString()?.Length <= maxLen)
                                    .WithName(field.Label)
                                    .WithMessage(string.IsNullOrWhiteSpace(rule.Message)
                                        ? $"{field.Label} must be at most {maxLen} characters."
                                        : rule.Message);
                            break;

                        // ── Min/Max Value ─────────────────────────────────────
                        case ValidationRuleType.Min:
                            if (decimal.TryParse(rule.Value, out var minVal))
                                RuleFor(ctx => ctx.GetValue(field.Key))
                                    .Must(v => decimal.TryParse(v?.ToString(), out var d) && d >= minVal)
                                    .WithName(field.Label)
                                    .WithMessage(string.IsNullOrWhiteSpace(rule.Message)
                                        ? $"{field.Label} must be at least {minVal}."
                                        : rule.Message);
                            break;

                        case ValidationRuleType.Max:
                            if (decimal.TryParse(rule.Value, out var maxVal))
                                RuleFor(ctx => ctx.GetValue(field.Key))
                                    .Must(v => decimal.TryParse(v?.ToString(), out var d) && d <= maxVal)
                                    .WithName(field.Label)
                                    .WithMessage(string.IsNullOrWhiteSpace(rule.Message)
                                        ? $"{field.Label} must be at most {maxVal}."
                                        : rule.Message);
                            break;

                        // ── Regex ─────────────────────────────────────────────
                        case ValidationRuleType.Regex:
                            if (!string.IsNullOrWhiteSpace(rule.Value))
                                RuleFor(ctx => ctx.GetValue(field.Key))
                                    .Must(v => v is null || System.Text.RegularExpressions.Regex.IsMatch(
                                                  v.ToString() ?? "", rule.Value))
                                    .WithName(field.Label)
                                    .WithMessage(string.IsNullOrWhiteSpace(rule.Message)
                                        ? $"{field.Label} has an invalid format."
                                        : rule.Message);
                            break;

                        // ── Cross-Fragment ────────────────────────────────────
                        // Example CrossFragmentRef:   "fragment-masterdata:category"
                        // Example CrossFragmentCondition: "Equals:GOLD"
                        //                              "NotEmpty"
                        //                              "GreaterThan:5000"
                        case ValidationRuleType.CrossFragment:
                            if (!string.IsNullOrWhiteSpace(rule.CrossFragmentRef) &&
                                !string.IsNullOrWhiteSpace(rule.CrossFragmentCondition))
                            {
                                var parts      = rule.CrossFragmentRef.Split(':', 2);
                                var refFrag    = parts[0];
                                var refField   = parts.Length > 1 ? parts[1] : string.Empty;
                                var condition  = rule.CrossFragmentCondition;
                                var fieldLabel = field.Label;
                                var message    = rule.Message;

                                RuleFor(ctx => ctx.GetValue(field.Key))
                                    .Must((ctx, value) =>
                                        EvaluateCrossFragmentCondition(
                                            value,
                                            state.GetValue(refFrag, refField),
                                            condition))
                                    .WithName(fieldLabel)
                                    .WithMessage(string.IsNullOrWhiteSpace(message)
                                        ? $"{fieldLabel} failed cross-fragment validation."
                                        : message);
                            }
                            break;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CROSS-FRAGMENT CONDITION EVALUATOR
        // ─────────────────────────────────────────────────────────────────────

        private static bool EvaluateCrossFragmentCondition(
            object? thisValue, object? otherValue, string condition)
        {
            // condition format: "Operator" or "Operator:Operand"
            var colonIdx  = condition.IndexOf(':');
            var op        = colonIdx >= 0 ? condition[..colonIdx] : condition;
            var operand   = colonIdx >= 0 ? condition[(colonIdx + 1)..] : null;

            return op switch
            {
                // The OTHER field must not be empty
                "NotEmpty" =>
                    otherValue is not null &&
                    !string.IsNullOrWhiteSpace(otherValue.ToString()),

                // The OTHER field must equal the operand
                "Equals" =>
                    string.Equals(otherValue?.ToString(), operand,
                                  StringComparison.OrdinalIgnoreCase),

                // This field is only required when the OTHER field equals operand
                // i.e. if other == operand then this must not be empty
                "RequiredWhen" =>
                    !string.Equals(otherValue?.ToString(), operand,
                                   StringComparison.OrdinalIgnoreCase) ||
                    (thisValue is not null &&
                     !string.IsNullOrWhiteSpace(thisValue.ToString())),

                // This value must be <= the OTHER field's numeric value
                "LessThanOther" =>
                    decimal.TryParse(thisValue?.ToString(),  out var tv) &&
                    decimal.TryParse(otherValue?.ToString(), out var ov) &&
                    tv <= ov,

                // This value must be > operand only when other field equals condition
                "GreaterThan" =>
                    decimal.TryParse(thisValue?.ToString(), out var gv) &&
                    decimal.TryParse(operand,               out var gp) &&
                    gv > gp,

                _ => true  // unknown operator = pass
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  VALIDATION CONTEXT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Passed to DynamicFragmentValidator.ValidateAsync().
    /// Carries the fragment ID and a live snapshot of the state.
    /// </summary>
    public class FragmentValidationContext
    {
        private readonly Dictionary<string, object?> _values;

        public string FragmentId { get; }

        public FragmentValidationContext(string fragmentId,
                                         Dictionary<string, object?> values)
        {
            FragmentId = fragmentId;
            _values    = values;
        }

        public object? GetValue(string fieldKey) =>
            _values.TryGetValue(fieldKey, out var v) ? v : null;
    }
}
