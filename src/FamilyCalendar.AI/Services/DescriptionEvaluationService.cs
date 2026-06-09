using FamilyCalendar.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FamilyCalendar.AI.Services;

public class DescriptionEvaluationService(IOptions<OpenAiOptions> options, ILogger<DescriptionEvaluationService> logger) : IDescriptionEvaluationService
{
    private static readonly string SystemPrompt = """
        Du uppdaterar beskrivningsfältet för en kalenderhändelse.

        Du får:
        1. Befintlig beskrivning (kan vara tom)
        2. Ny information att utvärdera

        Ditt uppdrag:
        - Om den nya informationen INTE tillför något som inte redan finns i den befintliga
          beskrivningen (dvs. det är semantiskt sett samma information), returnera EXAKT
          strängen "NO_CHANGE" och inget annat.
        - Om den nya informationen tillför något nytt (t.ex. ny tid, ny plats, nya instruktioner,
          nya detaljer), producera en ny, uppdaterad beskrivning som smälter samman all
          information på ett naturligt, läsbart sätt på svenska.
        - Bevara ALLA rader som börjar med "Källa:" exakt som de är, och placera dem i slutet.
        - Skriv kompakt och informativt. Undvik upprepningar.

        Returnera ANTINGEN:
        - Exakt "NO_CHANGE" (om inget nytt tillförs)
        - Den fullständiga uppdaterade beskrivningstexten (om det finns ny information)
        """;

    public async Task<string?> MergeAsync(string? existingDescription, string newSummary, CancellationToken ct = default)
    {
        var client = new OpenAIClient(options.Value.ApiKey);
        var chatClient = client.GetChatClient(options.Value.Model);

        var userMessage = $"""
            Befintlig beskrivning:
            {(string.IsNullOrWhiteSpace(existingDescription) ? "(tom)" : existingDescription)}

            Ny information:
            {newSummary}
            """;

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(userMessage)
            };

            var response = await chatClient.CompleteChatAsync(messages, null, ct);
            var result = response.Value.Content[0].Text.Trim();

            if (result == "NO_CHANGE")
            {
                logger.LogDebug("Description evaluation: no new information detected");
                return null;
            }

            logger.LogDebug("Description evaluation: merged description produced");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Description evaluation failed, falling back to append logic");
            // Fallback: treat as new information if not already contained in the description.
            if (string.IsNullOrWhiteSpace(existingDescription))
                return newSummary;
            if (existingDescription.Contains(newSummary, StringComparison.OrdinalIgnoreCase))
                return null;
            return $"{existingDescription}\n\n{newSummary}";
        }
    }
}
