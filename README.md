# Class Finder Assistant - Sprint 1 (Student Dashboard)
[![CI](https://github.com/<your-org>/<your-repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-org>/<your-repo>/actions/workflows/ci.yml)

Production-ready Sprint 1 implementation for **Epic 1: Student Dashboard**.

## Repository Layout
- `frontend/` - React (Vite) student dashboard UI.
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
cd frontend
npm install
npm run dev
```
Frontend URL: `http://localhost:5173`

## Local Run (Docker Compose)
```bash
docker compose up --build -d
```
Then seed:
```bash
docker compose run --rm backend dotnet backend.dll --seed
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
cd frontend
npm run build
npm test
```

## SQL Migration + Seed Scripts
- Migration script: `infra/migrations/001_initial_schema.sql`
- Seed script: `infra/seed.sql`

Run these directly in SQL Server/Azure SQL if you want DB-first setup.

## Azure Deployment (Simple Path)

### Backend (Azure App Service)
1. Create Azure App Service (Linux, .NET 8).
2. Set `ConnectionStrings__DefaultConnection` app setting for Azure SQL.
3. Deploy `backend/` (GitHub Actions or `az webapp up`).
4. Run seed command once from Kudu/SSH:
   - `dotnet backend.dll --seed`

### Frontend (Azure Static Web Apps or App Service)
1. Build `frontend/` (`npm run build`).
2. Deploy `frontend/dist` to Azure Static Web App.
3. Set `VITE_API_BASE_URL` to the backend API URL before build.

## Demo / Grading Checks (Instructor Rubric)
1. Open `/dashboard` and show list view with enrolled classes and waitlist status.
2. Toggle to calendar view and show classes at expected day/time positions.
3. Click a class card and show detail page with professor/capacity/location/times/waitlist.
4. Open browser network tab and show requests to `/api/students/{id}/classes`, `/api/students/{id}/schedule`, `/api/classes/{id}`.
5. Run backend and frontend tests and show CI workflow status.

## ASSUMPTIONS
- Sprint 1 authentication is out of scope; dashboard uses seeded sample student ID `1`.
- Schedule events include waitlisted classes so students can still inspect timing conflicts.
- Time rendering uses local browser timezone and static class local-time strings (`HH:mm`).
- SQL Server auth uses local SA in development; production should use managed secrets.
