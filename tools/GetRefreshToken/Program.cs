using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

// Runs the Google OAuth "Desktop app" flow locally, forces consent (so a fresh
// refresh_token is always returned), and prints the result. Paste the printed
// line into your .env file, then `docker compose up -d --force-recreate api`.
//
// Usage:
//   dotnet run --project tools/GetRefreshToken -- <clientId> <clientSecret>
// Or set env vars GMAIL_CLIENT_ID / GMAIL_CLIENT_SECRET and run without args.

var clientId = args.ElementAtOrDefault(0) ?? Environment.GetEnvironmentVariable("GMAIL_CLIENT_ID");
var clientSecret = args.ElementAtOrDefault(1) ?? Environment.GetEnvironmentVariable("GMAIL_CLIENT_SECRET");

if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    Console.Error.WriteLine("Usage: GetRefreshToken <clientId> <clientSecret>");
    Console.Error.WriteLine("   or: set GMAIL_CLIENT_ID and GMAIL_CLIENT_SECRET env vars.");
    return 1;
}

string[] scopes =
[
    "https://www.googleapis.com/auth/gmail.readonly",
    "https://www.googleapis.com/auth/gmail.modify",
    "https://www.googleapis.com/auth/calendar",
];

// Pick a free localhost port for the OAuth redirect.
var listener = new HttpListener();
int port;
while (true)
{
    port = Random.Shared.Next(49152, 65535);
    listener.Prefixes.Clear();
    listener.Prefixes.Add($"http://localhost:{port}/");
    try { listener.Start(); break; }
    catch (HttpListenerException) { /* port in use, try another */ }
}

var redirectUri = $"http://localhost:{port}/";
var authUrl =
    "https://accounts.google.com/o/oauth2/v2/auth" +
    $"?client_id={Uri.EscapeDataString(clientId)}" +
    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
    "&response_type=code" +
    $"&scope={Uri.EscapeDataString(string.Join(" ", scopes))}" +
    "&access_type=offline" +
    "&prompt=consent";

Console.WriteLine("Opening browser for Google consent...");
Console.WriteLine("If it doesn't open, visit this URL manually:");
Console.WriteLine(authUrl);
Console.WriteLine();

try { Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true }); }
catch { /* user can copy the URL above */ }

var context = await listener.GetContextAsync();
var code = context.Request.QueryString["code"];
var error = context.Request.QueryString["error"];

var responseHtml = error is null
    ? "<html><body><h2>Done — you can close this tab.</h2></body></html>"
    : $"<html><body><h2>OAuth error: {WebUtility.HtmlEncode(error)}</h2></body></html>";
var bytes = Encoding.UTF8.GetBytes(responseHtml);
context.Response.ContentType = "text/html; charset=utf-8";
context.Response.ContentLength64 = bytes.Length;
await context.Response.OutputStream.WriteAsync(bytes);
context.Response.OutputStream.Close();
listener.Stop();

if (error is not null)
{
    Console.Error.WriteLine($"OAuth error: {error}");
    return 1;
}
if (string.IsNullOrEmpty(code))
{
    Console.Error.WriteLine("No authorization code returned.");
    return 1;
}

using var http = new HttpClient();
var tokenResp = await http.PostAsync("https://oauth2.googleapis.com/token",
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["code"] = code,
        ["client_id"] = clientId,
        ["client_secret"] = clientSecret,
        ["redirect_uri"] = redirectUri,
        ["grant_type"] = "authorization_code",
    }));

var json = await tokenResp.Content.ReadAsStringAsync();
if (!tokenResp.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"Token exchange failed ({(int)tokenResp.StatusCode}): {json}");
    return 1;
}

using var doc = JsonDocument.Parse(json);
if (!doc.RootElement.TryGetProperty("refresh_token", out var refreshToken))
{
    Console.Error.WriteLine("No refresh_token in response. Full response:");
    Console.Error.WriteLine(json);
    return 1;
}

Console.WriteLine();
Console.WriteLine("=== Paste into .env (replaces GMAIL_REFRESH_TOKEN line) ===");
Console.WriteLine($"GMAIL_REFRESH_TOKEN={refreshToken.GetString()}");
Console.WriteLine();
Console.WriteLine("Then: docker compose up -d --force-recreate api");
return 0;
