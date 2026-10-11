# RoomRaah

Accommodation discovery for Pakistan: rooms, hostels and shared housing, with moderated
listings and communication inside the platform.

## Features

- Public search by location, rent, facilities and availability; photos and room comparison.
- A full-screen map with place search, a card per room and "Search this area" for the part
  of the map on screen.
- Seeker accounts with saved listings, visit requests, reviews and reports, plus Google Maps
  directions once an in-person visit is accepted.
- Live messaging through SignalR, with polling when the connection is unavailable. Either
  person can delete a chat from their own inbox; the other keeps their copy, and a new message
  brings it back with only what came after.
- Owner listing and photo management, availability confirmation and visit responses.
- Admin listing/revision approval, review/report moderation, inspections and account management.
- Email verification, JWT sessions and role permissions. Published edits need admin approval.
  An unverified account can browse; a banner says what needs verification, and the verify
  page returns the user to what they were doing.
- Responsive Angular interface for phone, tablet, laptop and desktop.
- **AI assistant** powered by a **local AI model (Qwen 3.5 9B via Ollama)**: finds rooms from a
  plain-language message in English or Roman Urdu, measures distance, and carries out actions
  for seekers, owners and admins after the user confirms. See [AI assistant](#ai-assistant).

## Repository

```text
backend/                 .NET 10 API, application, domain and infrastructure
  tests/                 Unit and integration test source
  RoomRaah.slnx          Backend solution
  docker-compose.yml    Local SQL Server, Redis, Azurite and Mailpit
frontend/                Angular 22 application, assets and unit tests
```

Stack: Angular 22, SCSS, Leaflet, ASP.NET Core, EF Core, SQL Server, Redis and SignalR.
The AI assistant runs on a local model, `qwen3.5:9b`, served by Ollama.
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
  ollamaUrl: 'http://localhost:11434',
  agentModel: 'qwen3.5:9b',
};
```

Keep `ollamaUrl` and `agentModel`; the AI assistant needs them (see [AI assistant](#ai-assistant)).

In a second terminal, from the repository root:

```powershell
cd frontend
npm ci
npm start
```

Open `http://localhost:4200`. For a production build, configure the corresponding endpoints
in `src/environments/environment.ts` and run `npm run build`. Browser location works on
localhost or HTTPS. Production hosting must provide compatible HTTPS API endpoints and CORS.

## AI assistant

RoomRaah includes an **AI assistant** that does what a user can do on the website, by
conversation. It uses a **local AI model: `qwen3.5:9b`** (Qwen 3.5, 9 billion parameters,
Q4_K_M quantisation, about 6.6 GB), served by **Ollama** on the same machine. No cloud AI
service or API key is involved, and no conversation leaves the computer.

**What it does**

- **Seekers:** finds rooms from a message such as *"mujhe Lahore mein 25k tak boys hostel
  chahiye wifi ke sath"*, ranks them by straight-line distance from the user's location or a
  named university, and shows rent, beds, rating and inspection status. It can open listings,
  compare, save, request or cancel visits, message owners, review and report.
- **Owners:** shows their listings and visit requests, edits listing details, confirms free
  beds, submits for review, requests inspection, accepts or declines visits and replies to
  messages. Changes to a live listing are sent for admin review, never saved directly.
- **Admins:** works the review queue, verification checks, revisions, inspections and badges,
  review and report moderation, and user accounts.
- Replies in English or Roman Urdu, matching how the user writes.

**How it is built**

- The Angular app calls Ollama directly from the browser (`frontend/src/app/core/agent/`).
- The model decides which action to take; each action runs through the same frontend
  services and API as the website, so the backend's permission rules still apply.
- Anything that changes data shows a **confirm card** first and runs only after the user
  presses Confirm.
- Ranking and distance are calculated in code, not by the model. Replies are scanned so no
  phone number or email address is ever shown, and the model is told to use only data
  returned by the site.
- If a search does not say who the room is for, it asks *"boys, girls ya family?"* rather
  than guessing.

**Setup**

Install [Ollama](https://ollama.com), then download the model once:

```powershell
ollama pull qwen3.5:9b
```

Keep Ollama running while using the site; it listens on `http://localhost:11434` and allows
calls from `http://localhost:4200` by default. Open the site and click **Ask RoomRaah** in the
bottom-right corner. The first answer takes 10 to 20 seconds while the model loads; later
answers usually take a few seconds. A GPU with 8 GB of memory is recommended. Without
Ollama running, the rest of the site works normally and the assistant says it is offline.

## Demo accounts

Local seeded accounts all use `Password1` for this demo only:

| Role | Email |
|---|---|
| Seeker | seeker1@roomraah.local |
| Owner | owner.checked@roomraah.local |
| Admin | admin@roomraah.local |

The login page also has a **quick sign-in (testing only)**: pick an account type, then any
account of that type. It reads the list through the seeded admin account, so it shows every
account's email - remove it before any public deployment. The verify and reset-password
pages have an **Open Mailpit** button for the same reason: local testing.

Suggested demo: search a room, compare options, save a listing, request a visit and open a
conversation. Then demonstrate owner management and admin approval with the other accounts.
Finally, open **Ask RoomRaah** and try the same tasks by conversation.

## Build and tests

```powershell
dotnet build backend/RoomRaah.slnx
dotnet test backend/tests/RoomRaah.UnitTests
dotnet test backend/tests/RoomRaah.IntegrationTests
cd frontend
npm run build
npm test -- --watch=false
```

Integration tests require Docker. Test source is included. The latest frontend validation
passed 268 unit tests, including the AI assistant's. An earlier packaging also passed 50 live
browser checks, including the assistant for seekers, owners and admins. The original browser
automation harness and generated reports are excluded from this source-only submission.

## Submission contents

Includes source, test source, assets, migrations, package lockfile and required build/run
configuration. Excludes Git history, dependencies, binaries, caches, logs, screenshots,
presentations, progress/agent documentation and private development configuration.
Original source repositories remain separate and unchanged.

Never commit real passwords, signing keys, cloud credentials or populated `.env` files.
