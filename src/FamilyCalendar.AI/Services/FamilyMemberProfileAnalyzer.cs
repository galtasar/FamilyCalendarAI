using FamilyCalendar.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FamilyCalendar.AI.Services;

public class FamilyMemberProfileAnalyzer(IOptions<OpenAiOptions> options, ILogger<FamilyMemberProfileAnalyzer> logger)
{
    public async Task<FamilyMemberProfileAnalysisResult?> AnalyzeAsync(
        string emailSender,
        string emailSubject,
        string emailBody,
        IReadOnlyList<FamilyMember> familyMembers,
        CancellationToken ct = default)
    {
        var client = new OpenAIClient(options.Value.ApiKey);
        var chatClient = client.GetChatClient(options.Value.Model);
        var systemPrompt = $$"""
            Du analyserar e-post om familjemedlemmars aktiviteter och skola för att förbättra deras profiler.

            Familjemedlemmar:
            {{BuildFamilyMembersWithProfiles(familyMembers)}}

            Dina uppgifter:
            1. PROFIL-UPPDATERINGAR: Identifiera STABIL, LÅNGSIKTIG information om familjemedlemmarna
               som inte redan finns i deras profil. Returnera bara genuint ny information.

               Bra exempel på vad som ska sparas:
               - Tillhörighet (skola, klass, förskola, avdelning, jobb)
               - Föreningar och lag (klubbnamn, lagnamn, träningstider)
               - Återkommande aktiviteter (klättring, ridning, simskola)
               - Kontaktpersoner — klassföreståndare, tränare, läkare, terapeut.
                 Inkludera namn OCH e-postadress om båda är kända. Detta är särskilt
                 värdefullt eftersom avsändaren av framtida mail kan identifieras därigenom.
                 Exempel: "Klassföreståndare: Sabina Danielsson (sabina@vattholmaskolan.se)".

               SPARA ALDRIG engångshändelser (specifika datum, möten, utflykter, läkartider) —
               de hör hemma i kalendern, inte i profilen. Skriv t.ex. INTE "Deltar i
               friidrottsdagen 26/5" eller "Har läkartid 14 mars". Skriv ENBART det som
               förblir sant om månader och år.

            2. FRÅGOR: Om mailet innehåller information som tyder på en aktivitet men det är oklart vem det gäller, generera en tydlig fråga till användaren.

            Returnera ENDAST JSON:
            {
              "profile_updates": [
                {"family_member_name": "Tage", "new_info": "Spelar fotboll i Vattholma IF, P2016-laget"}
              ],
              "questions": [
                {"question": "Vilken familjemedlem rider häst?", "context": "Mailet är från en ridskola"}
              ]
            }

            Returnera tomma listor om inget relevant hittades. Generera INTE frågor om informationen redan finns i en familjemedlems profil.
            """;

        var userMessage = $"""
            Avsändare: {emailSender}
            Ämne: {emailSubject}

            E-post:
            {emailBody}
            """;

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            var completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await chatClient.CompleteChatAsync(messages, completionOptions, ct);
            var json = response.Value.Content[0].Text;

            logger.LogDebug("Family member profile analysis response: {Json}", json);

            return JsonSerializer.Deserialize<FamilyMemberProfileAnalysisResult>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Family member profile analysis failed for subject {Subject}", emailSubject);
            return null;
        }
    }

    private static string BuildFamilyMembersWithProfiles(IReadOnlyList<FamilyMember> familyMembers)
    {
        var builder = new StringBuilder();

        foreach (var familyMember in familyMembers.OrderBy(c => c.Name))
        {
            builder.Append("- ")
                .Append(familyMember.Name)
                .Append(": ")
                .Append(string.IsNullOrWhiteSpace(familyMember.Description) ? "Ingen beskrivning" : familyMember.Description)
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}

public class FamilyMemberProfileAnalysisResult
{
    [JsonPropertyName("profile_updates")] public List<FamilyMemberProfileUpdate> ProfileUpdates { get; set; } = [];
    [JsonPropertyName("questions")] public List<ReviewQuestion> Questions { get; set; } = [];
}

public class FamilyMemberProfileUpdate
{
    [JsonPropertyName("family_member_name")] public string FamilyMemberName { get; set; } = string.Empty;
    [JsonPropertyName("new_info")] public string NewInfo { get; set; } = string.Empty;
}

public class ReviewQuestion
{
    [JsonPropertyName("question")] public string Question { get; set; } = string.Empty;
    [JsonPropertyName("context")] public string Context { get; set; } = string.Empty;
}
