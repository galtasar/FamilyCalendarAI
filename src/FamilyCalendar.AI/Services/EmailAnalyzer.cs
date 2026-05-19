using FamilyCalendar.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace FamilyCalendar.AI.Services;

public class OpenAiOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1";
}

public class EmailAnalyzer(IOptions<OpenAiOptions> options, ILogger<EmailAnalyzer> logger)
{
    private static readonly string SystemPrompt = """
        Du analyserar e-post relaterade till familjemedlemmars skolor, förskolor och aktiviteter.

        Familjen består av:
        - Vera och Tage går på skolan (grundskola)
        - Sixten och Folke går på förskolan
        - Micke och Emelie är föräldrar
        Om ett mail gäller "skolan" eller "klassen" UTAN att namnge specifik klass/årskurs, identifiera ALLA som går på den skolan.
        Om ett mail namnger en specifik klass eller årskurs (t.ex. "klass 5", "årskurs 3", "5A"), MÅSTE du använda familjemedlemsprofilerna nedan för att avgöra vilken/vilka familjemedlemmar som tillhör just den klassen — inkludera INTE familjemedlemmar från andra klasser.
        Om ett mail gäller "förskolan" identifiera ALLA som går på förskolan.
        Om ett mail gäller samtliga familjemedlemmar, returnera ["Vera", "Tage", "Sixten", "Folke"].
        Om det är oklart vem/vilka som berörs, returnera tom lista [].

        Målet är att avgöra om ett kalenderevent behöver skapas.

        Identifiera:
        - vilka familjemedlemmar mailet gäller — kan vara en, flera eller alla
        - datum och tider
        - aktivitet
        - plats
        - om kalenderhändelse krävs
        - hur säker du är (confidence 0.0-1.0)
        - om aktiviteten är återkommande (is_recurring)

        VIKTIGT om datum och tider:
        - Om mailet innehåller en specifik starttid (t.ex. "kl 14:00", "10:30"), MÅSTE du inkludera den i "start" som fullständig ISO8601 med tid (t.ex. "2026-05-20T14:00:00").
        - Sätt "has_time" till true om ett specifikt klockslag finns, annars false.
        - Om "has_time" är false (bara datum känt), sätt "requires_manual_review" till true.
        - Om "end" saknas i mailet, UPPSKATTA sluttid baserat på händelsetyp:
            * Fotbollsmatch/ishockeymatch/match: starttid + 90 minuter
            * Träning/övning: starttid + 75 minuter
            * Skolavslutning/uppträdande/konsert: starttid + 90 minuter
            * Föräldramöte/möte: starttid + 60 minuter
            * Utflykt/heldagsaktivitet: starttid + 6 timmar
            * Läkarbesök/tandläkare: starttid + 60 minuter
            * Kalas/fest: starttid + 120 minuter
            * Övrigt: starttid + 60 minuter

        Returnera ENDAST JSON enligt följande schema:
        {
          "relevant": boolean,
          "confidence": number (0.0-1.0),
          "children": string[] (familjemedlemmar, tom lista om oklart),
          "event_type": string | null,
          "title": string | null,
          "start": ISO8601 string med tid | null,
          "end": ISO8601 string med uppskattad tid om ej angiven | null,
          "has_time": boolean,
          "location": string | null,
          "requires_calendar_event": boolean,
          "requires_manual_review": boolean,
          "is_recurring": boolean,
          "summary": string | null
        }

        Om mailet inte är på svenska, returnera: {"relevant": false, "confidence": 1.0, "children": [], "requires_calendar_event": false, "requires_manual_review": false, "is_recurring": false, "has_time": false}
        """;

    public async Task<AiAnalysisResult?> AnalyzeAsync(Email email, IReadOnlyList<FamilyMember> familyMembers, CancellationToken ct = default)
    {
        var client = new OpenAIClient(options.Value.ApiKey);
        var chatClient = client.GetChatClient(options.Value.Model);

        var stockholmTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var utcOffset = stockholmTz.GetUtcOffset(email.ReceivedAt);
        var offsetStr = utcOffset >= TimeSpan.Zero
            ? $"+{utcOffset:hh\\:mm}"
            : $"-{utcOffset:hh\\:mm}";

        var userMessage = $"""
            Avsändare: {email.Sender}
            Ämne: {email.Subject}
            Mottaget: {email.ReceivedAt:yyyy-MM-dd HH:mm}
            Tidszon för tider i mailet: Europe/Stockholm (UTC{offsetStr})

            VIKTIGT: Inkludera alltid tidzon i ISO8601-strängar, t.ex. "2026-05-31T14:00:00{offsetStr}"

            Familjemedlemsprofiler:
            {BuildFamilyMembersContext(familyMembers)}

            {email.Body}
            """;

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(userMessage)
            };

            var completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await chatClient.CompleteChatAsync(messages, completionOptions, ct);
            var json = response.Value.Content[0].Text;

            logger.LogDebug("AI response for message {MessageId}: {Json}", email.MessageId, json);

            return JsonSerializer.Deserialize<AiAnalysisResult>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI analysis failed for email {MessageId}", email.MessageId);
            return null;
        }
    }

    private static string BuildFamilyMembersContext(IReadOnlyList<FamilyMember> familyMembers)
    {
        var preferredOrder = new[] { "Vera", "Tage", "Sixten", "Folke", "Micke", "Emelie" };
        var familyMemberLookup = familyMembers.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var orderedFamilyMembers = preferredOrder
            .Where(familyMemberLookup.ContainsKey)
            .Select(name => familyMemberLookup[name])
            .Concat(familyMembers.Where(c => !preferredOrder.Contains(c.Name, StringComparer.OrdinalIgnoreCase)));

        return string.Join(Environment.NewLine, orderedFamilyMembers.Select(familyMember =>
        {
            var description = string.IsNullOrWhiteSpace(familyMember.Description) ? "Ingen beskrivning" : familyMember.Description;
            return $"- {familyMember.Name}: {description}";
        }));
    }
}
