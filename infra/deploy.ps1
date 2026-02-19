param(
    [string]$resourceGroup = "rg-weatherstation",
    [string]$location = "westeurope",
    [string]$functionAppName = "func-weatherstation-$((Get-Random -Maximum 9999))",
    [string]$storageAccountName = "stweather$((Get-Random -Maximum 99999))",
    [string]$appInsightsName = "appi-weatherstation-$((Get-Random -Maximum 99999))"
)

Write-Host "Starting deployment..." -ForegroundColor Green

# -----------------------------------
# 1?? Create Resource Group
# -----------------------------------
Write-Host "Creating resource group..."
az group create `
    --name $resourceGroup `
    --location $location

# -----------------------------------
# 2?? Deploy Bicep Infrastructure
# -----------------------------------
Write-Host "Deploying infrastructure..."
az deployment group create `
    --resource-group $resourceGroup `
    --template-file ./main.bicep `
    --parameters `
        functionAppName=$functionAppName `
        storageAccountName=$storageAccountName `
        appInsightsName=$appInsightsName

# -----------------------------------
# 3?? Publish .NET Project
# -----------------------------------
Write-Host "Publishing .NET project..."

cd ../WeatherStation

dotnet publish -c Release -o publish

if (!(Test-Path publish)) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# -----------------------------------
# 4?? Zip Publish Folder
# -----------------------------------
Write-Host "Zipping project..."
Compress-Archive -Path publish\* -DestinationPath publish.zip -Force

# -----------------------------------
# 5?? Deploy to Azure Function
# -----------------------------------
Write-Host "Deploying to Azure Function..."

az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --src publish.zip

Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "Function App Name: $functionAppName"
Write-Host "Storage Account Name: $storageAccountName"
Write-Host "Application Insights Name: $appInsightsName
