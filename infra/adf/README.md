# Azure Data Factory Sprint 2 Sync

This folder contains the Sprint 2 ADF deployment artifact for external source ingestion and SQL synchronization.

## Components

The Bicep template creates:
- `ls_classfinder_api_rest`
- `ls_classfinder_students_http`
- `ls_classfinder_professors_http`
- `ls_classfinder_azure_sql`
- `ds_classfinder_api_classes`
- `ds_classfinder_api_enrollments`
- `ds_classfinder_api_waitlist`
- `ds_classfinder_students_csv`
- `ds_classfinder_professors_json`
- `ds_stage_classfinder_students`
- `ds_stage_classfinder_professors`
- `ds_stage_classfinder_classes`
- `ds_stage_classfinder_enrollments`
- `ds_stage_classfinder_waitlist`
- `pl_classfinder_external_sync`
- `trg_classfinder_external_sync_schedule`

## Loading Strategy

1. Copy source snapshots into SQL stage tables.
2. Call `dbo.usp_ClassFinder_ApplyExternalSync`.
3. The stored procedure deduplicates, normalizes, and upserts into curated tables.
4. Existing application-originated enrollments are not overwritten by the external snapshot.
5. Missing external enrollments are marked `Dropped` when they disappear from a later snapshot.

## Prerequisites

Apply the database changes before running the pipeline:

```bash
sqlcmd -S <server>.database.windows.net -d <database> -U <user> -P <password> -i infra/migrations/002_sprint2_catalog_sync.sql
```

Grant the Data Factory managed identity the minimum required SQL access:
- `db_datareader`
- `db_datawriter`
- `EXECUTE` on `dbo.usp_ClassFinder_BeginExternalSync`
- `EXECUTE` on `dbo.usp_ClassFinder_ApplyExternalSync`
- `EXECUTE` on `dbo.usp_ClassFinder_FailExternalSync`

## Deploy

1. Copy `classfinder-sprint2.parameters.example.json` to a private parameters file.
2. Replace the placeholder values with environment-specific values.
3. Deploy the template:

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file infra/adf/classfinder-sprint2.bicep \
  --parameters @infra/adf/classfinder-sprint2.parameters.json
```

## Cost Posture

This template already uses low-cost ADF primitives:
- copy activities plus stored procedures
- no Mapping Data Flows
- no managed virtual network

To keep spend low while maintaining batch-sync functionality:
- leave the schedule trigger stopped unless automated backfills are required
- prefer manual pipeline runs for one-off loads
- if automation is required, keep the default daily cadence unless a tighter SLA is actually needed

## Start The Trigger

The schedule trigger is created but left stopped. Start it after the linked services validate:

```bash
az datafactory trigger start \
  --resource-group <resource-group> \
  --factory-name <factory-name> \
  --name trg_classfinder_external_sync_schedule
```

## Validation

Run the pipeline once manually and verify:
- Stage tables contain fresh source rows.
- `dbo.ExternalSourceSyncRuns` shows `Succeeded`.
- `dbo.Students`, `dbo.Instructors`, `dbo.CourseClasses`, and `dbo.Enrollments` reflect the source snapshot.
- Catalog pages in the app show any newly imported classes or instructors.
