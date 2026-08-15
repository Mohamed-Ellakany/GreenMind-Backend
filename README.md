# GreenMind backend

ASP.NET Core API for authentication, farm administration, export plans, farm tasks, dashboards, and AI-assisted planning.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (the default development connection uses `(localdb)\\MSSQLLocalDB`)
- EF Core CLI 10 (`dotnet tool install --global dotnet-ef --version 10.*` if needed)
- A [WeatherAPI](https://www.weatherapi.com/) key
- An ITI Student Bedrock Gateway API key

## One-time development setup

Run these commands from `GreenMind-Backend`:

```powershell
dotnet restore
dotnet dev-certs https --trust
dotnet user-secrets set --project Greenmind.Api "Jwt:Key" "replace-with-a-random-secret-at-least-32-characters-long"
dotnet user-secrets set --project Greenmind.Api "ScheduleAnalysis:SigningKey" "replace-with-an-independent-random-secret-at-least-32-characters-long"
dotnet user-secrets set --project Greenmind.Api "Weather:BaseUrl" "https://api.weatherapi.com/v1/"
dotnet user-secrets set --project Greenmind.Api "Weather:ApiKey" "YOUR_WEATHER_API_KEY"
dotnet user-secrets set --project Greenmind.Api "iti:ApiKey" "YOUR_SBG_API_KEY"
dotnet user-secrets set --project Greenmind.Api "iti:Model" "anthropic.claude-sonnet-4-6"
dotnet user-secrets set --project Greenmind.Api "iti:VisionModel" "anthropic.claude-haiku-4-5-20251001-v1:0"
dotnet user-secrets set --project Greenmind.Api "Smtp:UserName" "your-email@gmail.com"
dotnet user-secrets set --project Greenmind.Api "Smtp:Password" "your-16-char-google-app-password"
dotnet ef database update --project Greenmind.Infrastructure --startup-project Greenmind.Api
```

Alternatively, set the gateway key in the environment as `SBG_API_KEY`. Do not commit API
keys. The chat gateway requires one of these values; without it, failed chat requests are
recorded in the application log with the gateway status and response.

### Gmail Email Provider Setup
Email notifications (admin invitations, password resets, registration notifications, and approval emails) use **Gmail SMTP** (`smtp.gmail.com` on port 587 with STARTTLS).

To configure Gmail:
1. Enable **2-Step Verification** on your Google Account: (Google Account -> Security -> 2-Step Verification).
2. Generate an **App Password**: (Google Account -> Security -> 2-Step Verification -> App passwords). Select app name (e.g. `GreenMind`) and copy the generated 16-character key.
3. Configure `appsettings.json` or user secrets:
   - `Smtp:UserName`: Your Gmail address (e.g., `your-email@gmail.com`)
   - `Smtp:Password`: Your 16-character App Password (spaces will be automatically trimmed)
   - `Smtp:From`: (Optional, defaults to your `UserName`)
   - `Smtp:DisplayName`: Sender name (e.g., `"GreenMind | نظام العقل الأخضر"`)
   - In production / Docker, provide them as environment variables `Smtp__UserName` and `Smtp__Password`.

Serilog writes application and HTTP request events to the SQL Server `Logs` table created
by the `AddSerilogLogs` EF migration. It uses `ConnectionStrings:online` when that value
is non-empty, otherwise it uses `ConnectionStrings:DefaultConnection`.

Claude Sonnet 4.6 is the default for user chat, while Claude Haiku 4.5 is used for
disease-image analysis. OpenAI GPT-OSS 120B handles structured planning and task
proposals. Compliance retrieval uses text ranking because the approved OpenAI and
Anthropic models do not provide embeddings.

## Run

```powershell
dotnet run --project Greenmind.Api --launch-profile https
```

The API runs at `https://localhost:7132`; Swagger is at `https://localhost:7132/swagger`.

For local testing, startup seeds this administrator:

- Email: `admin@local`
- Password: `ChangeMe!P@ssw0rd`

This credential is for development only and must be replaced before deployment.

## Automatic initial planning

Creating an export plan saves it immediately with planning state `Pending`. A background worker then gathers:

- the full export plan, farm, crop, destination, dates, location, and season stage;
- recent soil measurements for the plan;
- the farm's existing schedule;
- tasks from the same farm and crop during the previous 90 days;
- configured task types and EU pre-harvest rules;
- a live seven-day weather forecast.

It asks the AI planner for tasks from tomorrow through the next seven days, validates and deduplicates them, then saves them. Planning status can be read from `GET /api/farm-owner/export-plans/{id}/planning-status`; a failed job can be retried with `POST /api/farm-owner/export-plans/{id}/planning/retry`.

## Coordinate-based schedule analysis

Before an export plan can be created, call `POST /api/farm-owner/export-plans/schedule-analysis` with the crop, destination, farm details, and exact `latitude`/`longitude`. The service uses the coordinates—not the city name—for NASA POWER climate data and returns proposed dates, feasibility, confidence, official sources, AI-inferred facts, and a short-lived signed `analysisToken`.

Send that token unchanged when creating the plan. Moving the map marker, changing plan inputs, or editing dates requires a new analysis. Plans assessed as risky, not recommended, or low-confidence also require `acceptScheduleWarnings: true`. `city` and `governorate` remain display/search metadata only and are never used as weather-query inputs.

## Test

```powershell
dotnet test Greenmind.Application.Tests/Greenmind.Application.Tests.csproj
dotnet test Greenmind.Api.Tests/Greenmind.Api.Tests.csproj
```
