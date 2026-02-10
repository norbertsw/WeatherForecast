targetScope = 'resourceGroup'

// ============================================================================
// Parameters
// ============================================================================

@description('The name of the application')
param appName string = 'weatherforecast'

@description('The deployment environment (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'prod'

@description('The Azure region for all resources')
param location string = resourceGroup().location

@description('The API key for endpoint authentication')
@secure()
param apiKey string

@description('App Service Plan SKU')
@allowed([
  'F1'
  'B1'
  'B2'
  'S1'
])
param appServicePlanSku string = 'B1'

@description('Redis Cache SKU')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param redisSku string = 'Basic'

@description('Redis Cache capacity (0-6)')
@minValue(0)
@maxValue(6)
param redisCapacity int = 0

// ============================================================================
// Variables
// ============================================================================

var resourceToken = uniqueString(resourceGroup().id)

var names = {
  logAnalytics: '${appName}-${environment}-law'
  appInsights: '${appName}-${environment}-ai'
  acr: '${appName}${environment}acr${take(resourceToken, 6)}'
  redis: '${appName}-${environment}-redis'
  keyVault: '${appName}-${environment}-kv'
  appServicePlan: '${appName}-${environment}-plan'
  appService: '${appName}-${environment}-app'
}

var tags = {
  application: appName
  environment: environment
}

// Built-in role definition IDs
var roles = {
  keyVaultSecretsUser: subscriptionResourceId(
    'Microsoft.Authorization/roleDefinitions',
    '4633458b-17de-408a-b874-0445c86b69e6'
  )
  acrPull: subscriptionResourceId(
    'Microsoft.Authorization/roleDefinitions',
    '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  )
}

// ============================================================================
// Monitoring
// ============================================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: names.logAnalytics
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: names.appInsights
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 30
  }
}

// ============================================================================
// Container Registry
// ============================================================================

resource acr 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: names.acr
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// ============================================================================
// Azure Cache for Redis
// ============================================================================

resource redis 'Microsoft.Cache/redis@2024-11-01' = {
  name: names.redis
  location: location
  tags: tags
  properties: {
    sku: {
      name: redisSku
      family: redisSku == 'Premium' ? 'P' : 'C'
      capacity: redisCapacity
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

// ============================================================================
// Key Vault
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: names.keyVault
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// Key Vault Secrets
resource secretRedisConnectionString 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'ConnectionStrings--Redis'
  properties: {
    value: '${redis.properties.hostName}:${redis.properties.sslPort},password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

resource secretAppInsightsConnectionString 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'ApplicationInsights--ConnectionString'
  properties: {
    value: appInsights.properties.ConnectionString
  }
}

resource secretApiKey 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'ApiKey'
  properties: {
    value: apiKey
  }
}

// ============================================================================
// App Service
// ============================================================================

resource appServicePlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: names.appServicePlan
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2025-03-01' = {
  name: names.appService
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    reserved: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|mcr.microsoft.com/dotnet/aspnet:10.0'
      acrUseManagedIdentityCreds: true
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/healthz'
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acr.properties.loginServer}'
        }
        {
           name: 'KeyVaultUri'
           value: keyVault.properties.vaultUri
        }
      ]
    }
  }
}

// ============================================================================
// Role Assignments
// ============================================================================

// App Service → Key Vault Secrets User
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appService.id, roles.keyVaultSecretsUser)
  scope: keyVault
  properties: {
    roleDefinitionId: roles.keyVaultSecretsUser
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// App Service → AcrPull
resource acrRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, appService.id, roles.acrPull)
  scope: acr
  properties: {
    roleDefinitionId: roles.acrPull
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================================
// Outputs
// ============================================================================

output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServiceName string = appService.name
output acrLoginServer string = acr.properties.loginServer
output acrName string = acr.name
