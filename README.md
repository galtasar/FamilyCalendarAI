# Family Calendar AI

Automatically reads emails sent to **dahl.aicalendar@gmail.com**, uses OpenAI to classify them, and creates Google Calendar events for the family's children — Vera, Tage, Sixten, and Folke.

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.0+ | [download](https://dotnet.microsoft.com/download) |
| Docker Desktop | Latest | Required by Aspire for PostgreSQL |
| Node.js | 20+ | For the React frontend |
| dotnet-ef | Latest | EF Core migrations tool |

Install the EF CLI tool if needed:
```bash
dotnet tool install --global dotnet-ef
```

---

## Google Cloud Setup

You need a Google Cloud project with OAuth2 credentials that have access to both Gmail and Google Calendar for the `dahl.aicalendar@gmail.com` account.

### 1. Create a Google Cloud Project

1. Go to [console.cloud.google.com](https://console.cloud.google.com)
2. Create a new project (e.g. `FamilyCalendarAI`)

### 2. Enable APIs

Enable the following APIs in your project:
- **Gmail API**
- **Google Calendar API**

### 3. Create OAuth2 Credentials

1. Go to **APIs & Services → Credentials**
2. Click **Create Credentials → OAuth client ID**
3. Application type: **Desktop app**
4. Download the JSON — note the `client_id` and `client_secret`

### 4. Obtain a Refresh Token

Run the OAuth2 flow once to get a refresh token with the required scopes:

**Required scopes:**
- `https://www.googleapis.com/auth/gmail.readonly`
- `https://www.googleapis.com/auth/gmail.modify`
- `https://www.googleapis.com/auth/calendar`

You can use the [Google OAuth Playground](https://developers.google.com/oauthplayground) or a small helper script. Store the resulting `refresh_token`.

---

## Configuration

Copy `appsettings.json` as a starting point and fill in your secrets. **Never commit secrets to source control.**

For development, create `src/FamilyCalendar.Api/appsettings.Development.json` (already gitignored):

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4.1"
  },
  "Gmail": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret",
    "RefreshToken": "your-refresh-token",
    "ApplicationEmail": "dahl.aicalendar@gmail.com"
  },
  "GoogleCalendar": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret",
    "RefreshToken": "your-refresh-token",
    "CalendarName": "Familjekalender"
  },
  "ReviewNotification": {
    "AppBaseUrl": "http://localhost:5000",
    "RecipientEmail": "dahl.aicalendar@gmail.com"
  },
  "WeeklySummary": {
    "SendOnDayOfWeek": 0,
    "SendAtHour": 18,
    "RecipientEmail": "dahl.aicalendar@gmail.com"
  }
}
```

### Environment Variable Reference

All settings can be overridden via environment variables using the `__` separator:

| Variable | Description |
|----------|-------------|
| `OpenAI__ApiKey` | Your OpenAI API key |
| `OpenAI__Model` | Model to use (default: `gpt-4.1`) |
| `Gmail__ClientId` | Google OAuth2 client ID |
| `Gmail__ClientSecret` | Google OAuth2 client secret |
| `Gmail__RefreshToken` | Gmail refresh token |
| `GoogleCalendar__ClientId` | Google OAuth2 client ID (same as Gmail) |
| `GoogleCalendar__ClientSecret` | Google OAuth2 client secret (same as Gmail) |
| `GoogleCalendar__RefreshToken` | Calendar refresh token (same as Gmail) |
| `ReviewNotification__AppBaseUrl` | Base URL for approve/reject links (default: `http://localhost:5000`) |

---

## Running the App

### Option A — Development (Aspire + Rider)

Start everything with a single command via .NET Aspire:

```bash
cd C:\path\to\FamilyCalendarAI
dotnet run --project src/FamilyCalendar.AppHost
```

This will:
1. Start a **PostgreSQL** container (via Docker)
2. Run EF Core migrations automatically on first start
3. Start the **ASP.NET Core API** at `http://localhost:5000`
4. Start the **React frontend** at `http://localhost:5173`
5. Open the **Aspire Dashboard** at `http://localhost:15888` (logs, traces, metrics)

### Option B — Always-on (Docker Compose)

Runs everything as background containers — no Rider needed. Starts automatically with Docker Desktop.

```bash
# First time: copy and fill in secrets
cp .env.example .env
# Edit .env with your API keys

# Start
docker compose up -d --build

# Stop
docker compose down
```

The app will be available at `http://localhost`. To auto-start on system boot, enable **"Start Docker Desktop on login"** in Docker Desktop settings.

### Frontend

The React dashboard is available at `http://localhost:5173` (Aspire) or `http://localhost` (Docker Compose):

| Page | URL | Description |
|------|-----|-------------|
| Översikt | `/` | Summary dashboard |
| Inkorg | `/emails` | All processed emails |
| Granskning | `/review` | Events pending manual approval |
| Händelser | `/events` | All calendar events |

---

## Project Structure

```
src/
  FamilyCalendar.AppHost/          # .NET Aspire orchestration
  FamilyCalendar.ServiceDefaults/  # Health checks, OpenTelemetry, Serilog
  FamilyCalendar.Api/              # ASP.NET Core Minimal API + Program.cs
  FamilyCalendar.Core/             # Domain models, interfaces, business rules
  FamilyCalendar.Infrastructure/   # EF Core, repositories, background services
  FamilyCalendar.AI/               # OpenAI GPT-4.1 integration
  FamilyCalendar.Email/            # Gmail polling, parsing, notifications
  FamilyCalendar.Calendar/         # Google Calendar adapter
  FamilyCalendar.Web/              # React + Vite + Material UI frontend
tests/
  FamilyCalendar.UnitTests/
  FamilyCalendar.IntegrationTests/
```

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/emails` | List recent processed emails |
| `GET` | `/api/emails/{id}` | Get email detail with AI result |
| `GET` | `/api/events` | List all events (filter by `childName`, `from`, `to`) |
| `GET` | `/api/events/pending-review` | Events awaiting manual approval |
| `POST` | `/api/events/{id}/approve` | Approve → creates Google Calendar event |
| `POST` | `/api/events/{id}/reject` | Reject event |
| `PATCH` | `/api/events/{id}` | Edit event fields |
| `POST` | `/api/process-email` | Manually submit a raw email for processing |

Swagger UI available at `http://localhost:5000/swagger` in development.

---

## How It Works

1. **Gmail polling** — every 5 minutes, unprocessed emails in `dahl.aicalendar@gmail.com` are fetched and labelled `FamilyCalendarProcessed`
2. **AI classification** — GPT-4.1 analyses each email in Swedish and returns structured JSON with relevance, confidence, child names, event details
3. **Business rules**:
   - `confidence ≥ 0.80` + clear date + known child → **auto-create** Google Calendar event
   - `confidence < 0.80`, ambiguous, or recurring → **queue for manual review** + send notification email
   - Newsletter / no activity / non-Swedish → **discard**
4. **Review** — approve/reject from the dashboard or directly from the notification email links
5. **Weekly summary** — every Sunday at 18:00 a digest email is sent to `dahl.aicalendar@gmail.com`

---

## Children (pre-seeded)

| Name | School | Class |
|------|--------|-------|
| Vera | Vattholmaskolan | Klass 5 |
| Tage | Vattholmaskolan | Klass 3 |
| Sixten | Hyttans förskola | Förskola |
| Folke | Hyttans förskola | Förskola |

To update (e.g. after school transitions), edit directly in the database or add an EF migration.
