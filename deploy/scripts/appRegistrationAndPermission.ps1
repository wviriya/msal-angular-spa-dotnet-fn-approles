[CmdletBinding()]
param (
    [string] $clientName,
    [string] $apiName,
    [string] $resourceGroup,
    [string] $staticWebURL
)

# .\appRegistrationAndPermission.ps1 -clientName wvapimtest -apiName "fn-wvapim-test" -resourceGroup "wvapim-test-rg" -staticWebURL https://wvapimtest.z8.web.core.windows.net

az config set extension.use_dynamic_install=yes_without_prompt

((Get-Content -path .\replyUrls.json -Raw) -replace "__replyUrl__",$staticWebURL) | Set-Content -path .\replyUrls.json

# Create Client App
$clientApp=$(az ad app create --display-name "${clientName}-staticwebapp" --native-app false --oauth2-allow-implicit-flow false | ConvertFrom-Json)

# Create API App
$apiApp=$(az ad app create --display-name "${apiName}-app" --identifier-uris "api://${apiName}" --reply-urls "https://${apiName}.azurewebsites.net/.auth/login/aad/callback" | ConvertFrom-Json)

$clientObjId=$clientApp.objectId
$clientAppId=$clientApp.appId
$apiAppId=$apiApp.appId
$apiPermissionId=$apiApp.oauth2Permissions.id

# Create service principal for api app and to grant permissions
az ad sp create --id $apiAppId --only-show-errors

az ad app permission add --id $clientAppId --api $apiAppId --api-permissions "${apiPermissionId}=Scope"
az ad app permission add --id $apiAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions "e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope"

az ad app update --id $clientAppId --app-roles "@appRoles.json"
az ad app update --id $apiAppId --app-roles "@appRoles.json"

# Use Microsoft Graph API to create SPA type reply URL's - NOTE: only works with BETA endpoint
az rest --method patch --uri "https://graph.microsoft.com/beta/applications/$clientObjId" --headers "Content-Type=application/json" --body "@replyUrls.json"

# Allow client app above to access API as a trusted service
$secret=$(az ad app credential reset --id $apiApp.objectId --append --only-show-errors | ConvertFrom-Json)

$tenantDomainName=$clientApp.publisherDomain

$account=$(az account show | ConvertFrom-Json)
$tenantId=$account.tenantId

az webapp auth microsoft update -g $resourceGroup -n $apiName -y `
--allowed-audiences "api://${apiName}" `
--client-id $apiAppId `
--client-secret $secret.password  `
--issuer "https://sts.windows.net/${tenantId}/v2.0" -o none

az webapp auth update -g $resourceGroup -n $apiName --enabled true --action AllowAnonymous

echo "::set-output name=clientId::${clientAppId}"
echo "::set-output name=scope::api://${apiName}/user_impersonation"
echo "::set-output name=tenantDomainName::https://login.microsoftonline.com/${tenantDomainName}"