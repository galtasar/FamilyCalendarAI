namespace FamilyCalendar.Core.Interfaces;

public interface IDescriptionEvaluationService
{
    /// <summary>
    /// Evaluates an existing event description against new summary information from a follow-up email.
    /// Returns an AI-rewritten description that coherently merges both if the new summary adds
    /// something not already present, or null if the existing description already covers the new info.
    /// </summary>
    Task<string?> MergeAsync(string? existingDescription, string newSummary, CancellationToken ct = default);
}
