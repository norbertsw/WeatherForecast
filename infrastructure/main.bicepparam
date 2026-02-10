using './main.bicep'

param appName = 'weatherforecast'
param environment = 'prod'
param appServicePlanSku = 'B1'
param redisSku = 'Basic'
param redisCapacity = 0
param apiKey = ''
