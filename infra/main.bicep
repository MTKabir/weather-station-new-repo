param location string = resourceGroup().location
param functionAppName string
param storageAccountName string
param appInsightsName string

// -----------------------------------
// Storage Account
// -----------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

// Build proper connection string
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

// -----------------------------------
// Blob Container
// -----------------------------------
resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storage.name}/default/generated-images'
  dependsOn: [storage]
}

// -----------------------------------
// Queues
// -----------------------------------
resource jobStartQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: '${storage.name}/default/job-start-queue'
  dependsOn: [storage]
}

resource imageQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: '${storage.name}/default/image-process-queue'
  dependsOn: [storage]
}

// -----------------------------------
// Application Insights
// -----------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

// -----------------------------------
// Hosting Plan 
// -----------------------------------
resource hostingPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${functionAppName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true
  }
}

// -----------------------------------
// Function App
// -----------------------------------
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
  {
    name: 'AzureWebJobsStorage'
    value: storageConnectionString
  }
  {
    name: 'StorageConnection'
    value: storageConnectionString
  }
  {
    name: 'ImageContainerName'
    value: 'generated-images'
  }
  {
    name: 'BuienradarApiUrl'
    value: 'https://data.buienradar.nl/2.0/feed/json'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
]

    }
  }
}
