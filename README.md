# GreenMind backend

ASP.NET Core API for authentication, farm administration, export plans, farm tasks, dashboards, and AI-assisted planning.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (the default development connection uses `(localdb)\\MSSQLLocalDB`)
- EF Core CLI 10 (`dotnet tool install --global dotnet-ef --version 10.*` if needed)
- A [WeatherAPI](https://www.weatherapi.com/) key
- A GitHub Models token that can call `openai/gpt-4.1-mini`

## One-time development setup

Run these commands from `GreenMind-Backend`:

```powershell
dotnet restore
dotnet dev-certs https --trust
dotnet user-secrets set --project Greenmind.Api "Jwt:Key" "replace-with-a-random-secret-at-least-32-characters-long"
dotnet user-secrets set --project Greenmind.Api "Weather:BaseUrl" "https://api.weatherapi.com/v1/"
dotnet user-secrets set --project Greenmind.Api "Weather:ApiKey" "YOUR_WEATHER_API_KEY"
dotnet user-secrets set --project Greenmind.Api "OpenAI:Model" "openai/gpt-4.1-mini"
dotnet user-secrets set --project Greenmind.Api "OpenAI:ApiKey" "YOUR_GITHUB_MODELS_TOKEN"
dotnet ef database update --project Greenmind.Infrastructure --startup-project Greenmind.Api
```

Do not commit API keys. They are stored in .NET user secrets outside the repository.

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

## Test

```powershell
dotnet test Greenmind.Application.Tests/Greenmind.Application.Tests.csproj
dotnet test Greenmind.Api.Tests/Greenmind.Api.Tests.csproj
```

