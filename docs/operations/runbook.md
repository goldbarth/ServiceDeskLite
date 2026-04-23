# Local Development Runbook

This guide covers how to run the full application stack locally after cloning the repository.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (optional, for PostgreSQL)

Verify your setup:

```bash
dotnet --version   # must be 10.x
docker --version   # optional
```

## One-time Setup

Trust the ASP.NET Core development certificate. Required because both the API and the Web frontend use HTTPS locally.

```bash
dotnet dev-certs https --trust
```

Run this once after cloning. Skip if already trusted.

## Running Locally (Recommended)

Start both services in separate terminals using the `https` launch profile.

**Terminal 1 — API:**

```bash
dotnet run --project src/ServiceDeskLite.Api --launch-profile https
```

API starts on `https://localhost:7238`. Uses InMemory persistence in Development — no database required.

**Terminal 2 — Web:**

```bash
dotnet run --project src/ServiceDeskLite.Web --launch-profile https
```

Web starts on `https://localhost:7023`.

Open **`https://localhost:7023`** in the browser.

### Why both services need the https profile

The API's CORS policy (in `appsettings.Development.json`) only allows `https://localhost:7023`.  
The Web's API client (also in `appsettings.Development.json`) targets `https://localhost:7238`.  
Running either service on its HTTP port breaks the connection.

### Port reference

| Service | HTTP             | HTTPS                  |
|---------|------------------|------------------------|
| API     | localhost:5300   | **localhost:7238**     |
| Web     | localhost:5310   | **localhost:7023**     |

Bold = required for local dev.

## Running with Docker (API + PostgreSQL)

Use this when you need a real PostgreSQL database.

```bash
# Start API and database
docker compose up --build
```

API starts on `http://localhost:8080`. The Web frontend is not included in Compose and must still be started locally.

To connect the local Web to the Docker API, override the base URL:

```bash
ApiClient__BaseUrl=http://localhost:8080 dotnet run --project src/ServiceDeskLite.Web
```

Note: CORS is not configured for this combination by default. Adjust `appsettings.Development.json` in the API if needed.

## Common Issues

### "Connection refused (localhost:7238)"

The API is not running or started without the https profile.

Fix: make sure `--launch-profile https` is passed when starting the API.

### Browser shows certificate warning

The dev certificate is not trusted yet.

Fix: run `dotnet dev-certs https --trust` and restart the browser.

### API starts but returns 401

The API key header is missing or wrong. In Development both services use `dev-api-key-not-a-secret` — check `appsettings.Development.json` in both projects.
