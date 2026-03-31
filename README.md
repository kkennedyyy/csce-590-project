# Class Finder Assistant
[![CI](https://github.com/<your-org>/<your-repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-org>/<your-repo>/actions/workflows/ci.yml)

Production-ready full-stack class registration app with React frontend, ASP.NET Core API, and SQL Server/Azure SQL.

## Plug-And-Play Quick Start

Single command local startup:

```bash
docker compose up --build
```

Open:
- Frontend (`ui-classfinder`): `http://localhost:5173`
- API: `http://localhost:8080/swagger`

Notes:
- DB migrations are applied automatically on API startup.
- Demo seed data is loaded automatically (`SeedDataOnStartup=true` in compose).
- Optional customization: copy `.env.example` to `.env` and adjust values.

## Repository Layout
- `frontend/ui-classfinder/` - Main React + TypeScript registration UI.
- `frontend/` - Legacy Sprint 1 dashboard UI scaffold.
- `backend/` - ASP.NET Core Web API (.NET 8).
- `infra/` - SQL migration + seed scripts.
- `.github/workflows/ci.yml` - CI pipeline for build + tests.
- `docker-compose.yml` - Local full-stack run with SQL Server.

## Implemented Sprint 1 Scope
- Student Dashboard home with enrolled classes.
- List view and calendar view toggle.
- Waitlist status shown in dashboard cards.
- Click class card to open class detail page.
- REST API-driven frontend (no hardcoded display data).
- Responsive and accessible UI (keyboard-focusable controls + ARIA labels).
- Backend unit + integration tests.
- Frontend Jest + React Testing Library tests.

## API Endpoints
- `GET /api/students/{id}/classes`
  - Returns student classes with fields required by Use Case 1.1.
- `GET /api/classes/{id}`
  - Returns class details (capacity, enrolled count, waitlist info).
- `GET /api/students/{id}/schedule`
  - Returns calendar events for dashboard calendar view.

### Scheduler/Cloud Compatibility Endpoints
These endpoints are added so `frontend/ui-classfinder` can read/write against the same backend:
- `POST /api/auth/login`
- `GET /api/classes?page=1&pageSize=10&search=...`
- `GET /api/classes/by/{idOrSection}`
- `GET /api/students/{id}/schedule/state`
- `POST /api/students/{id}/schedule`
- `DELETE /api/students/{id}/schedule/{classIdOrSection}`
- `GET /api/teachers/{teacherId}/classes`
- `GET /api/teachers/{teacherId}/classes/{classIdOrSection}/roster`
- `PUT /api/teachers/{teacherId}/classes/{classIdOrSection}/capacity`
- `DELETE /api/teachers/{teacherId}/classes/{classIdOrSection}/students/{studentId}`

JSON schemas are in `backend/Schemas/`:
- `student-classes.schema.json`
- `class-detail.schema.json`
- `student-schedule.schema.json`

## Prerequisites
- Node.js 20+
- .NET 8 SDK
- SQL Server (local or Azure SQL)
- Docker (optional, for compose-based local run)

## Local Run (Manual)

### 1. Configure database connection
Update `backend/appsettings.json` connection string if needed:
- `ConnectionStrings:DefaultConnection`

Default expects SQL Server on `localhost:1433` with:
- user: `sa`
- password: `Your_password123`
- database: `ClassFinderDb`

### 2. Seed database (single command)
```bash
dotnet run --project backend/backend.csproj -- --seed
```

### 3. Start backend
```bash
dotnet run --project backend/backend.csproj
```
Backend URL: `http://localhost:8080` (Swagger in development)

### 4. Start frontend
```bash
cd frontend/ui-classfinder
npm install
npm run dev
```
Frontend URL: `http://localhost:5173`

## Local Run (Docker Compose)
```bash
docker compose up --build
```
Open:
- Frontend: `http://localhost:5173`
- API: `http://localhost:8080/swagger`

## Tests

### Backend build + tests
```bash
dotnet build backend/backend.csproj
dotnet test backend/Tests/Backend.Tests.csproj
```

### Frontend build + tests
```bash
cd frontend/ui-classfinder
npm run build
npm test
```

## SQL Migration + Seed Scripts
- Migration script: `infra/migrations/001_initial_schema.sql`
- Seed script: `infra/seed.sql`
- Canonical schema scripts: `database/schema/*.sql` (aligned to EF model; legacy section/waitlist files are now documented no-ops).
- Azure schema check script: `infra/schema_check_azure.sql`

Run these directly in SQL Server/Azure SQL if you want DB-first setup.

## Azure Deployment (Simple Path)

### Backend (Azure App Service)
1. Create Linux App Service for `.NET 8`.
2. Set app settings:
   - `ConnectionStrings__DefaultConnection=<azure-sql-connection-string>`
   - `SeedDataOnStartup=true` (optional for demo environments)
3. Deploy `backend/`.
4. Verify `https://<api-app>.azurewebsites.net/swagger`.

### Frontend (Azure Static Web App or App Service)
1. Build/deploy `frontend/ui-classfinder`.
2. Set `VITE_API_BASE_URL=https://<api-app>.azurewebsites.net` at build time.
3. Verify frontend can hit `/api/*` endpoints from browser network tab.

### Existing Team Infra Deploy Script
Use `infra/deploy_existing_infra.sh` to deploy backend + frontend and run repeated DB read/write smoke tests.

Default target:
- Subscription: `6ce046bc-46c5-4dd5-a1b0-f1990fb9bfae`
- Resource group: `rg-classfinder-dev`
- Backend app: `classfinder-api-dev-e97ad3`
- Frontend storage: `classfinderuie97ad3`

Example:
```bash
./infra/deploy_existing_infra.sh
```

Optional overrides:
```bash
SUBSCRIPTION_ID=<sub-id> RESOURCE_GROUP=<rg> API_APP_NAME=<api-app> FRONTEND_STORAGE_ACCOUNT=<storage> SMOKE_CYCLES=8 ./infra/deploy_existing_infra.sh
```

## Demo / Grading Checks (Instructor Rubric)
1. Open `/schedule` and show schedule editor with seeded classes.
2. Open `/browse`, add a class, confirm schedule updates.
3. Click a scheduled class and show detail modal.
4. Trigger capacity/credit/overlap validation and confirm actionable errors.
5. Open browser network tab and show API calls.
6. Run backend and frontend tests and show CI workflow status.

## ASSUMPTIONS
- Sprint 1 authentication is out of scope; dashboard uses seeded sample student ID `1`.
- Schedule events include waitlisted classes so students can still inspect timing conflicts.
- Time rendering uses local browser timezone and static class local-time strings (`HH:mm`).
- SQL Server auth uses local SA in development; production should use managed secrets.
