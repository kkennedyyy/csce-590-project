@description('Name of the Function App that processes feed blobs.')
param functionAppName string

@description('Location for the Function App resources.')
param location string = resourceGroup().location

@description('Existing storage account name used for Functions runtime state and feed blobs.')
param storageAccountName string

@description('App Service plan name. Use an existing plan by setting useExistingPlan=true, otherwise a Y1 Consumption plan is created.')
param appServicePlanName string = '${functionAppName}-plan'

@description('Set to true to bind the Function App to an existing App Service plan.')
param useExistingPlan bool = false

@description('SQL connection string for the existing ClassFinder database.')
@secure()
param sqlConnectionString string

@description('Blob container name watched by the feed ingestion trigger.')
param feedContainerName string = 'classfinder-feeds'

@description('Optional Application Insights connection string.')
@secure()
param applicationInsightsConnectionString string = ''

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource storageBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource feedContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: storageBlobService
  name: feedContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource newPlan 'Microsoft.Web/serverfarms@2023-12-01' = if (!useExistingPlan) {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
}

resource existingPlan 'Microsoft.Web/serverfarms@2023-12-01' existing = if (useExistingPlan) {
  name: appServicePlanName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: useExistingPlan ? existingPlan.id : newPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
        {
          name: 'FeedIngestionStorageConnection'
          value: storageConnectionString
        }
        {
          name: 'FeedIngestion__ContainerName'
          value: feedContainerName
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output feedContainerName string = feedContainer.name
