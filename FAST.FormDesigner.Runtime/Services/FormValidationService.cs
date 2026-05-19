using FAST.FormDesigner.Runtime.Models;
using FAST.FormDesigner.Runtime.Validators;

namespace FAST.FormDesigner.Runtime.Services
{
        /// <summary>
    /// Orchestrates validation across ALL fragments in the form.
    /// Called on submit (or on demand for partial validation).
    /// Writes results directly into FormStateContainer.
    /// </summary>
    public class FormValidationService
    {
        private readonly FormStateContainer _state;

        public FormValidationService(FormStateContainer state)
        {
            _state = state;
        }

        /// <summary>
        /// Validates a single fragment. Writes errors to FormStateContainer.
        /// Returns true if valid.
        /// </summary>
        public async Task<bool> ValidateFragmentAsync(FormFragment fragment)
        {
            var values    = _state.GetFragmentValues(fragment.Id);
            var context   = new FragmentValidationContext(fragment.Id, values);
            var validator = new DynamicFragmentValidator(fragment, _state);
            var result    = await validator.ValidateAsync(context);

            // Clear previous errors for this fragment
            foreach (var field in fragment.Fields)
                _state.ClearErrors(fragment.Id, field.Key);

            // Write new errors
            foreach (var error in result.Errors)
            {
                // error.PropertyName == field.Key (set via WithName)
                _state.SetErrors(fragment.Id, error.PropertyName,
                                 new[] { error.ErrorMessage });
            }

            return result.IsValid;
        }

        /// <summary>
        /// Validates all provided fragments. Returns true if all pass.
        /// </summary>
        public async Task<bool> ValidateAllAsync(IEnumerable<FormFragment> fragments)
        {
            var results = await Task.WhenAll(
                fragments.Select(f => ValidateFragmentAsync(f)));
            return results.All(r => r);
        }
    }
}