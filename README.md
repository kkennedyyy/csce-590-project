# Class Finder

Full-stack class registration system with a .NET 8 API, React frontend, Azure SQL persistence, Azure Data Factory ingestion, a blob-triggered Azure Function for near-real-time feed imports, and Service Bus-based registration events.

## Sprint 2 Scope

This repository now includes Sprint 2 end-to-end behavior:
- Azure Data Factory ingestion for classes, enrollments, waitlist, students, and professors
- Azure Function blob-trigger ingestion that reuses the same storage feed import contract
- SQL stage and curated sync flow with idempotent merge behavior
- student enroll and drop flows with immediate persistence
- prerequisite, capacity, overlap, and drop-deadline validation
- waitlist promotion when seats open
- student dashboard registered-list and calendar views with waitlist visibility
- teacher workspace roster access control and roster enrollment metadata
- deterministic smart enrollment schedule generation
- prompt-driven smart enrollment that can optionally use an LLM to interpret free-form requests against the SQL-backed catalog
- class catalog filters and enroll/disenroll actions
- teacher catalog filters and enroll/disenroll actions
- Service Bus registration event publishing
- Logic App integration notes and event schema for enrollment/drop emails

## Repository Layout

- `backend/`: ASP.NET Core Web API (.NET 8)
- `frontend/ui-classfinder/`: main React + TypeScript UI
- `frontend/`: legacy Sprint 1 UI scaffold
- repo root: Azure Functions isolated worker for blob-trigger feed imports
- `database/`: canonical schema and stored procedure scripts
- `infra/`: deployment helpers, ADF artifacts, SQL migrations, Logic App notes

## Core Runtime Paths

Backend API:
- `GET /api/classes?page=1&pageSize=10&search=...&department=...&studentId=...`
- `GET /api/classes/by/{idOrSection}`
- `GET /api/students/{id}/schedule/state`
- `POST /api/students/{id}/schedule`
- `DELETE /api/students/{id}/schedule/{classIdOrSection}`
- `POST /api/students/{id}/schedule/finalize`
- `POST /api/students/{id}/smart-enrollment`
- `GET /api/teachers?search=...&department=...&studentId=...`
- `GET /api/teachers/{teacherId}/classes`
- `GET /api/teachers/{teacherId}/classes/{classIdOrSection}/roster`
- `PUT /api/teachers/{teacherId}/classes/{classIdOrSection}/capacity`
- `DELETE /api/teachers/{teacherId}/classes/{classIdOrSection}/students/{studentId}`

Frontend routes:
- `/schedule`
- `/browse`
- `/teachers`

## Local Run

### Docker compose

```bash
docker compose up --build
```

Open:
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080/swagger`

### Manual

1. Configure the backend connection string.
2. Seed the database.
3. Start the backend.
4. Start the frontend.

```bash
dotnet run --project backend/backend.csproj -- --seed
dotnet run --project backend/backend.csproj
cd frontend/ui-classfinder
npm install
npm run dev
```

## Configuration

Use environment variables or app settings instead of committing secrets.

### Backend

- `ConnectionStrings__DefaultConnection`
- `SeedDataOnStartup`
- `Notifications__Enabled`
- `Notifications__DirectEmailEnabled`
- `Notifications__FromEmail`
- `Notifications__FromDisplayName`
- `Notifications__PickupDirectory`
- `Notifications__SmtpHost`
- `Notifications__SmtpPort`
- `Notifications__ServiceBusConnectionString`
- `Notifications__ServiceBusEntityName`
- `SmartEnrollment__LlmEndpoint`
- `SmartEnrollment__LlmApiKey`
- `SmartEnrollment__LlmDeployment`
- `SmartEnrollment__LlmApiVersion`
- `SmartEnrollment__DefaultCandidateLimit`
- `FeedIngestion__Enabled`
- `FeedIngestion__ContainerName`
- `FeedIngestion__WatchPath`
- `FeedIngestion__ProcessedPath`
- `FeedIngestion__FailedPath`
- `FeedIngestionStorageConnection`
- `AzureWebJobsStorage`

### Frontend

- `VITE_API_BASE_URL`

### External source deployment inputs

These are consumed by the ADF deployment template and should be supplied through a private parameters file, Key Vault, or pipeline secret store:
- `CLASS_API_BASE_URL`
- `CLASS_API_FUNCTION_KEY`
- `STUDENTS_BLOB_SAS_URL`
- `PROFESSORS_BLOB_SAS_URL`

A safe example is in [`.env.example`](/home/mcs46/csce-590-project/.env.example).

## Database and Sync

Canonical scripts:
- schema updates: [`database/schema/09_Sprint2CatalogSync.sql`](/home/mcs46/csce-590-project/database/schema/09_Sprint2CatalogSync.sql)
- stage tables: [`database/schema/10_ExternalSyncStaging.sql`](/home/mcs46/csce-590-project/database/schema/10_ExternalSyncStaging.sql)
- merge procedures: [`database/stored_procedures/10_StoredProcedure.sql`](/home/mcs46/csce-590-project/database/stored_procedures/10_StoredProcedure.sql)
- sqlcmd wrapper: [`infra/migrations/002_sprint2_catalog_sync.sql`](/home/mcs46/csce-590-project/infra/migrations/002_sprint2_catalog_sync.sql)

The external sync flow is:
1. ADF copies source snapshots into SQL stage tables.
2. `dbo.usp_ClassFinder_ApplyExternalSync` merges stage data into curated tables.
3. application-originated enrollments are preserved during external sync.
4. missing external enrollments are marked `Dropped` on later reruns.

## Azure Data Factory

ADF artifacts live in `infra/adf/`:
- template: [`infra/adf/classfinder-sprint2.bicep`](/home/mcs46/csce-590-project/infra/adf/classfinder-sprint2.bicep)
- example parameters: [`infra/adf/classfinder-sprint2.parameters.example.json`](/home/mcs46/csce-590-project/infra/adf/classfinder-sprint2.parameters.example.json)
- setup notes: [`infra/adf/README.md`](/home/mcs46/csce-590-project/infra/adf/README.md)

The default trigger cadence is daily and the trigger is left stopped by default so Data Factory stays in a low-cost posture until automated batch sync is explicitly needed.

Deploy with:

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file infra/adf/classfinder-sprint2.bicep \
  --parameters @infra/adf/classfinder-sprint2.parameters.json
```

## Azure Function Feed Imports

The root Azure Functions project contains `FeedBlobIngestionFunction`, which reuses `IStorageFeedImportService` and `StorageFeedEnvelopeDto` from the backend instead of introducing a second import format.

Relevant files:
- function entry point: [`Functions/FeedBlobIngestionFunction.cs`](/home/mcs46/csce-590-project/Functions/FeedBlobIngestionFunction.cs)
- function host bootstrap: [`Program.cs`](/home/mcs46/csce-590-project/Program.cs)
- deployment notes: [`infra/functions/README.md`](/home/mcs46/csce-590-project/infra/functions/README.md)
- starter Bicep: [`infra/functions/classfinder-feed-function.bicep`](/home/mcs46/csce-590-project/infra/functions/classfinder-feed-function.bicep)

## Service Bus and Email Notifications

Successful enroll and drop operations publish registration events through the backend notification service.

Relevant files:
- notification abstraction: [`backend/Services/IEnrollmentNotificationService.cs`](/home/mcs46/csce-590-project/backend/Services/IEnrollmentNotificationService.cs)
- Service Bus publisher and direct email fallback: [`backend/Services/EnrollmentNotificationService.cs`](/home/mcs46/csce-590-project/backend/Services/EnrollmentNotificationService.cs)
- Logic App contract notes: [`infra/logicapp/README.md`](/home/mcs46/csce-590-project/infra/logicapp/README.md)
- Parse JSON schema: [`infra/logicapp/registration-event.schema.json`](/home/mcs46/csce-590-project/infra/logicapp/registration-event.schema.json)

## Smart Enrollment Prompt Flow

The student schedule page now treats smart enrollment as a prompt-first flow:
1. The student enters a free-form request in the planner text box.
2. The backend reads the class catalog from SQL for that student context.
3. If `SmartEnrollment__Llm*` settings are configured, the backend sends the prompt plus a catalog slice to the configured Azure OpenAI-compatible deployment to extract structured scheduling preferences.
4. The deterministic scheduler ranks valid schedule options and returns them to the UI.
5. Clicking a candidate previews it on the main schedule visualizer before the student applies it.

If no LLM settings are configured, the same endpoint falls back to the built-in parser and deterministic scheduler so local development and tests continue to work without external dependencies.

## Deploy Existing Team Infra

Use the existing helper to deploy the current backend container and the static frontend hosted in Azure Storage:

```bash
./infra/deploy_existing_infra.sh
```

The script:
- builds and pushes the backend container image
- updates the Azure Container App API
- builds `frontend/ui-classfinder`
- uploads the frontend to the storage account static website endpoint
- runs smoke checks against the API and frontend

The checked-in deployment helper targets the repository's current topology of Azure Container App for the API plus Azure Storage static website for the frontend. No additional App Service deployment script was added because the existing repo does not deploy that way and the accessible Azure subscription in this session did not expose a live ClassFinder App Service to update in place.

## Tests

Backend:

```bash
dotnet test backend/Tests/Backend.Tests.csproj
```

Frontend:

```bash
cd frontend/ui-classfinder
npm run build
npm test -- --runInBand
```

E2E:

```bash
cd frontend/ui-classfinder
npm run e2e
```

## Verification Checklist

1. Run the ADF pipeline and confirm `dbo.ExternalSourceSyncRuns` shows `Succeeded`.
2. Upload a valid feed blob to the configured `FeedIngestion__ContainerName` container and confirm the Function App imports it.
3. Confirm new classes and instructors appear in `/browse` and `/teachers`.
4. Enroll a student from `/browse` or `/teachers` and verify the dashboard updates immediately.
5. Drop the same class and verify seat counts, waitlist positions, and schedule views update immediately.
6. Confirm waitlisted students are promoted when seats open.
7. Confirm Service Bus receives registration events.
8. Confirm the Logic App or direct email path sends enrollment and drop emails.
