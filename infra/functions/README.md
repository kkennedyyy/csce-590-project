# Blob Feed Function

This repository already contains a .NET isolated Azure Functions project at the repo root. The `FeedBlobIngestionFunction` reuses the existing backend `IStorageFeedImportService` and `StorageFeedEnvelopeDto` contract so blob-based imports and the API watcher path share the same validation and upsert logic.

## Required app settings

- `AzureWebJobsStorage`
- `ConnectionStrings__DefaultConnection`
- `FeedIngestionStorageConnection`
- `FeedIngestion__ContainerName`

`FeedIngestionStorageConnection` can point at the same storage account as `AzureWebJobsStorage` when the same account hosts the feed container.

## Deploy

Deploy the Function App code from the repo root and set the app settings above. A starter Bicep template is in [`infra/functions/classfinder-feed-function.bicep`](/home/mcs46/csce-590-project/infra/functions/classfinder-feed-function.bicep).
