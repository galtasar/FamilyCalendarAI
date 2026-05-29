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
        Du analyserar e-post relaterade till en familjs aktiviteter, möten och åtaganden.

        Familjemedlemmarna definieras enbart av profilerna som skickas med varje mail.
        Gör INGA antaganden om vem som är barn, vuxen, förälder eller om vem som går
        på skola/förskola/jobb baserat på namn — använd alltid profiltexten.

        Regler för att avgöra vilka familjemedlemmar ett mail berör:
        - Avsändaren är en stark ledtråd. Om avsändarens namn eller e-postadress nämns
          i en familjemedlems profil — t.ex. "Veras klassföreståndare heter Sabina
          Danielsson (sabina.danielsson@vattholmaskolan.se)" eller "Tages fotbollstränare
          är Hans Larsson" — så hör mailet till den familjemedlemmen. Använd detta
          tillsammans med övriga regler nedan; det kan stärka eller motbevisa en klass-
          markör om båda finns.
        - Matcha mailet mot profilerna utifrån skola, klass, förskola, avdelning, arbetsplats,
          mottagningsställe, aktivitet, klubb eller annan kontext som nämns i mailet.
        - Klassbeteckningar — börja ALLTID med att leta efter dessa innan du läser brödtexten:
            * "klass 5", "klass 3", "5A", "5B", "åk 5", "årskurs 3" → exakt den klassen.
            * Ämnesrader på formen "Inför vecka X - N", "Veckobrev N", "Info klass N",
              "- N" där N är ett heltal 1–9 → klass N. Den efterföljande siffran är
              klassens årskurs, inte ett datum eller annat.
            * Om ämnesraden har en sådan klassmarkör, använd ENDAST den klassen och
              ignorera tvetydigheter i brödtexten.
        - Om mailet namnger en specifik klass/årskurs/avdelning enligt ovan, inkludera
          ENDAST de familjemedlemmar vars profil uttryckligen matchar den klassen.
          Inkludera INTE syskon från andra klasser även om samma skola nämns.
        - Om mailet gäller en hel institution (skola, förskola, arbetsplats, klubb)
          UTAN att specificera klass/avdelning, inkludera ALLA familjemedlemmar vars
          profil pekar på den institutionen.
        - Om mailet är personligt adresserat till en specifik familjemedlem (t.ex.
          påminnelse om läkartid, terapi, möte), returnera bara den personen.
        - Om mailet gäller hela familjen, returnera samtliga familjemedlemmar.
        - Om ingen profil matchar, returnera tom lista [].

        Behandla ALLA familjemedlemmar likvärdigt. Vuxnas möten, terapi, läkarbesök,
        arbetsåtaganden och privata aktiviteter ska kalenderläggas på samma sätt som
        barns aktiviteter. Det finns ingen åldersgräns för vad som räknas som en
        kalenderhändelse.

        Målet är att producera en LISTA av kalenderhändelser som ska skapas, avbokas eller
        uppdateras baserat på mailet. Ett enskilt mail kan beskriva flera olika händelser —
        t.ex. ett veckobrev från skolan kan nämna utflykt på måndag, friidrottsdag på onsdag
        och föräldramöte på torsdag. Returnera då ETT objekt PER händelse i "events"-listan.
        Om mailet inte beskriver någon konkret kalenderhändelse alls, returnera "events": [].

        VIKTIGT om avbokningar och inställda händelser:
        Om mailet meddelar att en händelse ÄR INSTÄLLD, AVBOKAD eller AVBRUTEN (t.ex.
        "Vi ställer in onsdagens utflykt", "Föräldramötet den 15:e är inställt",
        "Träningen ställs in p.g.a. sjukdom"): sätt "action" = "cancel" för den händelsen.
        Inkludera titel, datum och familjemedlemmar precis som för en vanlig händelse.
        För alla händelser som ska SKAPAS, använd "action" = "create" (default).

        En kalenderhändelse innebär: en specifik tidpunkt eller dag, kopplad till minst en
        identifierad familjemedlem, som hen förväntas delta i eller behöver känna till.

        För VARJE händelse i listan, identifiera:
        - action (create eller cancel)
        - vilka familjemedlemmar händelsen gäller (family_members)
        - datum och tider (start, end, has_time)
        - aktivitet (event_type, title)
        - plats (location)
        - om händelsen är återkommande (is_recurring)
        - om händelsen behöver granskas manuellt (requires_manual_review)
        - kort sammanfattning (summary)

        På toppnivå anger du:
        - relevant: true om mailet innehåller något av intresse för familjen
        - confidence: 0.0–1.0 hur säker du är på tolkningen som helhet
        - summary: kort sammanfattning av mailet (valfritt, null om ej användbart)

        VIKTIGT om datum och tider:

        1. Vad räknas som ett klockslag?
           ALLT som anger en tid på dygnet räknas, oavsett skiljetecken (kolon eller punkt):
           "kl 14:00", "10:30", "13.20", "13.40", "samling 08:10", "bussen går 13.20 hem",
           "vi börjar kl 9", "fika 14-16". Samlings-, buss-, ankomst- och hemresetider
           räknas också som klockslag för eventet.

           Konkreta exempel — hur du ska tolka dem:
           - Email säger "Bussen går 13.20 hem" → has_time = true, end ≈ 13:20.
           - Email säger "Samling 8.15" → has_time = true, start ≈ 08:15.
           - Email säger "skoldagen slutar när bussen återvänder" + "bussen 13.20" →
             has_time = true, end ≈ 13:20 (busstiden är sluttiden).
           - Email säger "vi åker till Skansen på onsdag" utan något klockslag →
             has_time = false.

           Sätt "has_time" = true om någon klockslagsangivelse finns någonstans i mailet.
           Sätt "has_time" = false ENDAST när mailet inte innehåller någon tid på dygnet alls
           (t.ex. "imorgon", "fredag", "v22", utan klockslag).

        2. När has_time = true:
           - "start" MÅSTE vara en fullständig ISO8601-sträng med tid (tidigaste relevanta
             tidpunkt — t.ex. samlingstid hellre än lektionsstart om båda nämns).
           - "end" får ALDRIG vara null. Använd sluttid/hemresetid om mailet anger den,
             annars UPPSKATTA enligt händelsetyp:
               * Fotbollsmatch / ishockeymatch / match: start + 90 min
               * Träning / övning: start + 75 min
               * Skolavslutning / uppträdande / konsert: start + 90 min
               * Föräldramöte / möte / terapisession: start + 60 min
               * Utflykt / heldagsaktivitet: start + 6 timmar
               * Läkarbesök / tandläkare: start + 60 min
               * Kalas / fest: start + 120 min
               * Övrigt: start + 60 min

        3. När has_time = false (mailet anger ett datum men inget klockslag):
           - "start" MÅSTE innehålla DATUMET som ISO8601 med tid 00:00:00 — t.ex.
             "2026-06-01T00:00:00+02:00". Detta är en DATUM-platshållare som signalerar
             "rätt datum, klockslag saknas". Gissa ALDRIG ett klockslag.
           - "end" MÅSTE vara null. Använd inte 6-timmars-uppskattningen eller någon
             annan default-sluttid när has_time = false.
           - Sätt "requires_manual_review" till true så att användaren fyller i klockslaget.

        4. Om varken datum eller tid är känt (t.ex. "snart", "kommande veckor"):
           - Sätt has_time = false, start = null, end = null, requires_manual_review = true.

        5. Tidszon:
           - Alla tider i mailet avser Europe/Stockholm. Använd ALLTID den UTC-offset som
             användarmeddelandet anger ovan (t.ex. "+02:00" sommartid, "+01:00" vintertid).
           - Använd ALDRIG "+00:00" eller "Z" om inte mailet uttryckligen säger UTC.

        Returnera ENDAST JSON enligt följande schema:
        {
          "relevant": boolean,
          "confidence": number (0.0-1.0),
          "summary": string | null,
          "events": [
            {
                "action": "create" | "cancel",
                "family_members": string[] (vuxna och barn likvärdigt; tom lista om ingen profil matchar),
              "event_type": string | null,
              "title": string | null,
              "start": ISO8601 string med tid (null endast om has_time = false),
              "end": ISO8601 string med tid (null endast om has_time = false; använd uppskattning enligt händelsetyp om sluttid ej anges),
              "has_time": boolean,
              "location": string | null,
              "requires_manual_review": boolean,
              "is_recurring": boolean,
              "summary": string | null
            }
          ]
        }

        Om mailet inte är på svenska eller inte är relevant för familjen, returnera:
        {"relevant": false, "confidence": 1.0, "summary": null, "events": []}
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
