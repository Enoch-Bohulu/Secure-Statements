# Secure Statements API

A small ASP.NET Core service for storing customer bank statements and handing them out through short-lived, signed download links. The idea is simple: statement PDFs are sensitive, so nobody gets a permanent URL to one. Instead, an authenticated customer asks for a link, gets a token that's valid for a few minutes, and that token is the only thing that unlocks the file, after the server re-checks ownership.

The project is split along Clean Architecture lines. It's deliberately storage-agnostic: file bytes live behind an interface (currently on local disk), and metadata lives in PostgreSQL.

## Overview

There are two kinds of caller:

- **Customers** — list their own statements and generate download links. They can't see anyone else's data; ownership is filtered in the query itself, not after the fact.
- **Admins** — upload statements. Gated behind a role claim (`statements-admin`).

Downloads are a two-step flow on purpose. Creating a link requires a full JWT and passes an ownership check. Redeeming a link only needs the token (so a browser or download manager can use it), but the download endpoint validates the token *and* re-checks ownership in the database before streaming a single byte. Belt and braces.

Identity is not handled here. The API validates JWTs issued elsewhere, it doesn't do logins or password storage.

## Features

- JWT bearer authentication with issuer/audience/lifetime validation and a tightened 30s clock skew
- Role-based upload restriction (`statements-admin`)
- Signed, expiring download tokens (HMAC-SHA256, expiry carried inside the signed payload, constant-time signature comparison)
- Ownership enforced on every read path (defends against IDOR; returns `404` rather than `403` so it doesn't leak whether a statement exists)
- Upload validation: PDF magic-byte check, 25 MB size cap, sanitized filenames
- Audit trail for link issuance, downloads, and denials
- Blob storage behind an interface, so swapping local disk for S3/Azure Blob later doesn't touch the core
- Automatic EF Core migrations on startup
- Swagger UI in Development

## Tech Stack

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8.0.10 with the Npgsql provider
- PostgreSQL 16
- JWT bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- Swashbuckle (Swagger)
- Docker / Docker Compose for local runs
- Testing: xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers for PostgreSQL

## Project Structure

```
src/
  SecureStatements.Domain/          Entities (Statement, AuditEntry) — no dependencies
  SecureStatements.Application/     Services + port interfaces (repositories, blob store, clock, current user)
  SecureStatements.Infrastructure/  EF Core, repositories, file-system blob store, DI wiring, migrations
  SecureStatements.Api/             Controllers, JWT setup, middleware, composition root
tests/
  SecureStatements.UnitTests/       Fast, no database — uses fakes
  SecureStatements.IntegrationTests/ Real HTTP via WebApplicationFactory + Testcontainers Postgres
Dockerfile                          Multi-stage build for the API image
docker-compose.yml                  Local PostgreSQL + API
.env.example                        Template for local secrets (copy to .env)
e2e-test.sh                         End-to-end smoke test (upload -> link -> download)
```

Dependencies point inward. Domain knows nothing about the web or the database, which is what keeps the business rules testable in isolation.

## Requirements

- Docker (for PostgreSQL, for running the whole stack, and for the integration tests)
- .NET 8 SDK (only needed to run or test outside containers)

## Getting Started

### Run with Docker (whole stack)

Copy the environment template and bring everything up:

```bash
cp .env.example .env
docker compose up --build
```

That builds the API image, starts PostgreSQL, waits for it to become healthy, applies migrations, and starts the API. The API is published on `http://localhost:5173`.

```bash
curl http://localhost:5173/health
# {"status":"healthy"}
```

Uploaded PDFs are stored in a named volume (`securestatements-blob-data`), so they survive restarts. To stop and wipe all data (database + blobs):

```bash
docker compose down -v
```

### Run locally with the SDK

Start just the database in Docker, then run the API with the SDK:

```bash
docker compose up -d db
dotnet run --project src/SecureStatements.Api
```

It listens on `http://localhost:5173` (and `https://localhost:7296`). Migrations are applied automatically at startup. In Development, Swagger is at `http://localhost:5173/swagger`.

Because the Docker and local settings share the same signing keys and database password (see Configuration), a token you mint once works in both modes.

## Configuration

Settings live in `src/SecureStatements.Api/appsettings.json` and can be overridden with environment variables (double-underscore syntax, e.g. `ConnectionStrings__Database`). When running with Docker Compose, secrets come from a local `.env` file (see `.env.example`); `.env` is gitignored.

| Key | Env / .env variable | Purpose |
|-----|---------------------|---------|
| `ConnectionStrings:Database` | `DB_PASSWORD` (password portion) | PostgreSQL connection string |
| `Jwt:Issuer` / `Jwt:Audience` | — | Expected token issuer and audience |
| `Jwt:SigningKey` | `JWT_SIGNING_KEY` | Symmetric key used to validate incoming JWTs (min 32 chars) |
| `DownloadToken:SigningKey` | `DOWNLOAD_TOKEN_SIGNING_KEY` | Key used to sign download tokens (min 32 chars) |
| `DownloadToken:LifetimeMinutes` | — | Link lifetime; defaults to 15 |
| `BlobStore:RootPath` | — | Where PDF bytes are written (default `./data/statements`; `/app/data/statements` in Docker) |

The values shipped in `appsettings.json` and `.env.example` are for local development only. The app validates them at startup and refuses to boot if a key is missing or too short. Don't run these in production — move secrets to environment variables or a secret manager.

Note on `DB_PASSWORD`: PostgreSQL only applies it when the data volume is first created. If you change it after the volume already exists, recreate the volume with `docker compose down -v`.

## API

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/health` | none | Liveness check |
| GET | `/statements` | customer JWT | List the caller's statements |
| POST | `/statements/{id}/download-link` | customer JWT | Create a signed, expiring link for one statement |
| GET | `/download/{token}` | token in URL | Redeem a link and stream the PDF |
| POST | `/admin/statements` | admin JWT | Upload a statement (multipart form) |

The upload form fields are `CustomerId`, `Period`, and `File`.

### Getting a token for testing

The API only validates tokens, so during development you supply your own. The token must be signed with `Jwt:SigningKey` and carry the configured issuer and audience. `sub` is the customer id; add a `statements-admin` role claim for uploads.

The quickest way is [jwt.io](https://jwt.io):

- **Header:**
  ```json
  { "alg": "HS256", "typ": "JWT" }
  ```
- **Payload (customer):**
  ```json
  { "sub": "cust-001",
    "iss": "https://auth.local.securestatements",
    "aud": "secure-statements-api",
    "exp": 4102444800 }
  ```
- **Payload (admin):** same, plus `"role": "statements-admin"`
- In the "verify signature" box, paste the secret: `local-dev-jwt-signing-key-change-me-at-least-32chars`

Copy the encoded token from jwt.io and send it as a bearer token (below). The `e2e-test.sh` script mints both an admin and a customer token with `openssl` if you'd rather not use the browser.

### Example

```bash
# Upload (admin token)
curl -X POST http://localhost:5173/admin/statements \
  -H "Authorization: Bearer $ADMIN" \
  -F "CustomerId=cust-001" -F "Period=2026-07" \
  -F "File=@sample.pdf;type=application/pdf"

# List (customer token)
curl http://localhost:5173/statements -H "Authorization: Bearer $CUST"

# Request a link, then download
curl -X POST http://localhost:5173/statements/<id>/download-link \
  -H "Authorization: Bearer $CUST"
curl "<downloadUrl>" -o statement.pdf
```

## Tests

```bash
dotnet test
```

Unit tests run without any external services. The integration tests spin up a throwaway PostgreSQL container through Testcontainers, so Docker needs to be running for those.

There's also an end-to-end smoke test that exercises the full upload/link/download path against a running instance:

```bash
./e2e-test.sh
```

## Deployment

The API ships as a container built from a multi-stage `Dockerfile`: the SDK image restores and publishes the app, and the smaller ASP.NET runtime image runs it as a non-root user on port 8080. `docker-compose.yml` wires the API to PostgreSQL, waits for the database to be healthy before starting, reads secrets from `.env`, and persists uploaded PDFs in a named volume.

For a real deployment you'd push the image to a registry and supply configuration (connection string, signing keys) through the platform's secret management rather than a `.env` file. Migrations currently run on startup, which is convenient but worth gating as a separate step once you run more than one instance.

## Notes and known gaps

- Migrations run on startup, which is convenient but not ideal for multi-instance deployments, you'd want to gate that as a separate step before scaling out.
- Local disk storage isn't shared between instances; horizontal scaling would need the blob store pointed at shared object storage (the interface is already there for it).
- No rate limiting on the download or admin endpoints yet.
- Download tokens can't be revoked individually before they expire; the short lifetime is the mitigation.



