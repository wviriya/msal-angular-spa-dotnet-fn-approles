@description('Suffix for naming resources')
param appNameSuffix string = 'app${uniqueString(resourceGroup().id)}'

@allowed([
  'dev'
  'test'
  'prod'
])
@description('Environment')
param environmentType string = 'dev'

@description('Do you want to create new vault?')
param createKeyVault bool = true

@description('Key Vault name')
param keyVaultName string = 'kv-${appNameSuffix}-${environmentType}'

@description('Key Vault resource group')
param keyVaultResourceGroup string = resourceGroup().name

@description('User assigned managed idenity name')
param userAssignedIdentityName string = 'umsi-${appNameSuffix}-${environmentType}'

@description('User assigned managed idenity resource group')
param userAssignedIdentityResourceGroup string = resourceGroup().name

param resourceTags object = {
  ProjectType: 'Azure Serverless Web'
  Purpose: 'Demo'
}

var location = resourceGroup().location
var staticWebsiteStorageAccountName = '${appNameSuffix}${environmentType}'
var functionStorageAccountName = 'fn${appNameSuffix}${environmentType}'
var functionAppName = 'fn-${appNameSuffix}-${environmentType}'
var functionRuntime = 'dotnet'
var appServicePlanName = 'asp-${appNameSuffix}-${environmentType}'
var appInsightsName = 'ai-${appNameSuffix}-${environmentType}'
var cosmosDbName = '${appNameSuffix}-${environmentType}'
var cosmosDbAccountName = 'cosmos-${appNameSuffix}-${environmentType}'
var sasToken = 'DefaultEndpointsProtocol=https;AccountName=wvapimtest;AccountKey=M+ouaabr5oz5ZLs9viSiyUggxzdDsQs0SZHJs29JXeLiIr1u+JXFH28LKhGt4/jHYmVY9h/e+Z9P2+/idfNymg==;EndpointSuffix=core.windows.net'

// SKUs
var functionSku = environmentType == 'prod' ? 'EP1' : 'Y1'

// static values
var cosmosDbCollectionName = 'items'

// Use existing User Assigned MSI. See https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deployment-script-template#configure-the-minimum-permissions
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userAssignedIdentityName
  scope: resourceGroup(userAssignedIdentityResourceGroup)
}

resource appInsights 'Microsoft.Insights/components@2018-05-01-preview' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

module staticWebsite 'modules/staticWebsite.bicep' = {
  name: 'staticWebsite'
  params: {
    storageAccountName: staticWebsiteStorageAccountName
    deploymentScriptServicePrincipalId: userAssignedIdentity.id
    resourceTags: resourceTags
  }
}

module cosmosDB 'modules/cosmosdb.bicep' = {
  name: 'cosmosdb'
  params: {
    accountName: cosmosDbAccountName
    databaseName: cosmosDbName
    collectionName: cosmosDbCollectionName
  }
}

module functionApp 'modules/function.bicep' = {
  name: 'functionApp'
  params: {
    functionRuntime: functionRuntime
    functionSku: functionSku
    storageAccountName: functionStorageAccountName
    functionAppName: functionAppName
    appServicePlanName: appServicePlanName
    appInsightsInstrumentationKey: appInsights.properties.InstrumentationKey
    staticWebsiteURL: staticWebsite.outputs.staticWebsiteURL
    cosmosAccountName: cosmosDbAccountName
    cosmosDbName: cosmosDbName
    cosmosDbCollectionName: cosmosDbCollectionName
    keyVaultName: keyVaultName
    resourceTags: resourceTags
  }
}

module keyVault 'modules/keyVault.bicep' = if (!createKeyVault) {
  name: 'keyVault'
  scope: resourceGroup(keyVaultResourceGroup)
  params: {
    keyVaultName: keyVaultName
    functionAppName: functionApp.outputs.functionAppName
    cosmosAccountName: cosmosDB.outputs.cosmosDBAccountName
    deploymentScriptServicePrincipalId: userAssignedIdentity.id
    currentResourceGroup: resourceGroup().name
  }
}

module newKeyVault 'modules/newKeyVault.bicep' = if (createKeyVault) {
  name: 'newKeyVault'
  params: {
    keyVaultName: keyVaultName
    functionAppName: functionApp.outputs.functionAppName
    cosmosAccountName: cosmosDB.outputs.cosmosDBAccountName 
    deploymentScriptServicePrincipalId: userAssignedIdentity.id
    resourceTags: resourceTags
  }
}

output functionAppName string = functionApp.outputs.functionAppName
output staticWebsiteStorageAccountName string = staticWebsiteStorageAccountName
output staticWebsiteUrl string = staticWebsite.outputs.staticWebsiteURL
