# RoomRaah

Accommodation discovery for Pakistan: rooms, hostels and shared housing, with moderated
listings and communication inside the platform.

## Features

- Public search by location, rent, facilities and availability; photos, maps and room comparison.
- Seeker accounts with saved listings, visit requests, reviews and reports.
- Live messaging through SignalR, with polling when the connection is unavailable.
- Owner listing and photo management, availability confirmation and visit responses.
- Admin listing/revision approval, review/report moderation, inspections and account management.
- Email verification, JWT sessions and role permissions. Published edits need admin approval.
- Responsive Angular interface for phone, tablet, laptop and desktop.

## Repository

```text
backend/                 .NET 10 API, application, domain and infrastructure
  tests/                 Unit and integration test source
  RoomRaah.slnx          Backend solution
  docker-compose.yml    Local SQL Server, Redis, Azurite and Mailpit
frontend/                Angular 22 application, assets and unit tests
```

Stack: Angular 22, SCSS, Leaflet, ASP.NET Core, EF Core, SQL Server, Redis and SignalR.
The backend follows Clean Architecture and CQRS. Storage supports Azure Blob/Azurite and
AWS S3. The existing demo deployment uses EC2 and S3.

## Local setup

Install .NET SDK 10, Node.js 24.19+ and Docker Desktop with Linux containers.
Commands below use PowerShell. Start at the submission repository root.

### 1. Infrastructure and backend

In one terminal:

```powershell
cd backend
$env:ROOMRAAH_SQL_PASSWORD = 'SetYourOwn_StrongPassword123!'
docker compose up -d
```

Wait until SQL Server is ready to accept connections, then in the same terminal:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=RoomRaahDb;User Id=sa;Password=$env:ROOMRAAH_SQL_PASSWORD;TrustServerCertificate=True"
$env:ConnectionStrings__Redis = 'localhost:6379'
$env:ConnectionStrings__AzureBlob = 'UseDevelopmentStorage=true'
$env:Storage__Provider = 'AzureBlob'
$env:JwtSettings__Key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$env:EmailSettings__Host = 'localhost'
$env:EmailSettings__Port = '1025'
$env:EmailSettings__UseSsl = 'false'
$env:EmailSettings__FromAddress = 'noreply@roomraah.local'
$env:EmailSettings__FrontendBaseUrl = 'http://localhost:4200'
$env:Cors__AllowedOrigins__0 = 'http://localhost:4200'
dotnet restore RoomRaah.slnx
dotnet run --project RoomRaah.API --launch-profile http
```

The Development profile applies migrations and creates demo data. Use a disposable local
database. The API listens at `http://localhost:5019`, Swagger at `/swagger`, and Mailpit at
`http://localhost:8025`. Email verification and reset emails appear in Mailpit.

The SQL password must match the existing Docker volume if you reuse one. These commands
set environment variables for the current terminal. `.env.example` documents other options;
the .NET application does not automatically load it as a configuration file.

### 2. Frontend

The copied environment files retain the original demo API URLs. For a fully local run,
set the following in `frontend/src/environments/environment.development.ts`:

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5019/api/v1',
  hubUrl: 'http://localhost:5019/hubs/chat',
};
```

In a second terminal, from the repository root:

```powershell
cd frontend
npm ci
npm start
```

Open `http://localhost:4200`. For a production build, configure the corresponding endpoints
in `src/environments/environment.ts` and run `npm run build`. Browser location works on
localhost or HTTPS. Production hosting must provide compatible HTTPS API endpoints and CORS.

## Demo accounts

Local seeded accounts all use `Password1` for this demo only:

| Role | Email |
|---|---|
| Seeker | seeker1@roomraah.local |
| Owner | owner.checked@roomraah.local |
| Admin | admin@roomraah.local |

Suggested demo: search a room, compare options, save a listing, request a visit and open a
conversation. Then demonstrate owner management and admin approval with the other accounts.

## Build and tests

```powershell
dotnet build backend/RoomRaah.slnx
dotnet test backend/tests/RoomRaah.UnitTests
dotnet test backend/tests/RoomRaah.IntegrationTests
cd frontend
npm run build
npm test -- --watch=false
```

Integration tests require Docker. Test source is included. Previous frontend validation
passed 131 unit tests and 47 live browser checks before packaging; the original browser
automation harness and generated reports are excluded from this source-only submission.

## Submission contents

Includes source, test source, assets, migrations, package lockfile and required build/run
configuration. Excludes Git history, dependencies, binaries, caches, logs, screenshots,
presentations, progress/agent documentation and private development configuration.
Original source repositories remain separate and unchanged.

Never commit real passwords, signing keys, cloud credentials or populated `.env` files.
