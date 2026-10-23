using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CCOS.Web.Extentions;

public static class ModelStateExtensions
{
    /// <summary>
    /// Reads validation errors from an API response and adds them to the ModelStateDictionary, prefixing the keys as necessary.
    /// If the response does not contain a valid ValidationProblemDetails object or if there are no errors, a generic error message is added instead.
    /// </summary>
    public static async Task AddApiValidationErrorsAsync(this ModelStateDictionary modelState, HttpResponseMessage response, string prefix, string fallbackMessage, CancellationToken cancellationToken)
    {
        ValidationProblemDetails? problem;

        // Attempt to read the response content as a ValidationProblemDetails object. If this fails, set problem to null.
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        }
        catch (JsonException)
        {
            problem = null;
        }

        // If the response does not contain a valid ValidationProblemDetails object, or if there are no errors, add a generic error message.
        if (problem is null || problem.Errors.Count == 0)
        {
            modelState.AddModelError(string.Empty, fallbackMessage);
            return;
        }

        var keyPrefix = $"{prefix}.";

        // Add each error from the API response to the ModelState, prefixing the keys as necessary.
        foreach (var (key, messages) in problem.Errors)
        {
            // If the key already starts with the prefix, use it as-is; otherwise, prepend the prefix.
            var modelStateKey = key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase) ? key : $"{keyPrefix}{key}";

            foreach (var message in messages)
            {
                modelState.AddModelError(modelStateKey, message);
            }
        }
    }
}
