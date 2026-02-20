param(
    [string]$resourceGroup = "rg-weatherstation",
    [string]$location = "westeurope",
    [string]$functionAppName = "func-weatherstation-$((Get-Random -Maximum 9999))",
    [string]$storageAccountName = "stweather$((Get-Random -Maximum 99999))",
    [string]$appInsightsName = "appi-weatherstation-$((Get-Random -Maximum 99999))"
)

Write-Host "Starting deployment..." -ForegroundColor Green

# Save original path
$rootPath = Get-Location

# -----------------------------------
# 1 Create Resource Group
# -----------------------------------
az group create --name $resourceGroup --location $location

# -----------------------------------
# 2 Deploy Bicep Infrastructure
# -----------------------------------
az deployment group create `
    --resource-group $resourceGroup `
    --template-file "$rootPath/main.bicep" `
    --parameters `
        functionAppName=$functionAppName `
        storageAccountName=$storageAccountName `
        appInsightsName=$appInsightsName

# -----------------------------------
# 3 Publish .NET Project
# -----------------------------------
Set-Location "$rootPath/../WeatherStationClean"

dotnet publish -c Release -o publish

if (!(Test-Path publish)) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# -----------------------------------
# 4 Zip Publish Folder 
# -----------------------------------
Set-Location publish

Compress-Archive -Path * -DestinationPath ../deploy.zip -Force

Set-Location ..

# -----------------------------------
# 5 Deploy to Azure Function
# -----------------------------------
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --src deploy.zip

Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "Function App Name: $functionAppName"
Write-Host "Storage Account Name: $storageAccountName"
Write-Host "Application Insights Name: $appInsightsName"