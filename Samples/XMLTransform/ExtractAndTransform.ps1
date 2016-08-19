[CmdletBinding()]
param
(
    [Parameter(Mandatory = $true, HelpMessage="Enter the URL of the target site, e.g. 'https://intranet.mydomain.com/sites/targetSite'")]
    [String]
    $TargetSiteUrl,

    [Parameter(Mandatory = $false, HelpMessage="Enter the filepath for the template, e.q. Folder\File.xml or Folder\File.pnp")]
    [String]
    $FilePath,

    [Parameter(Mandatory = $false, HelpMessage="Enter the filepath for the transform XML, e.q. Folder\Transform.xml")]
    [String]
    $TransformPath,

    [Parameter(Mandatory = $false, HelpMessage="Optional administration credentials")]
    [PSCredential]
    $Credentials
)

if(!$FilePath)
{
    $FilePath = "site.xml"
}

if(!$TransformPath)
{
    $TransformPath = "Transform.xml"
}

if($Credentials -eq $null)
{
	$Credentials = Get-Credential -Message "Enter Admin Credentials"
}

Write-Host -ForegroundColor Yellow "Target Site URL: $targetSiteUrl"

try
{
    Connect-SPOnline $TargetSiteUrl -Credentials $Credentials -ErrorAction Stop

    [System.Reflection.Assembly]::LoadFrom("..\..\PnPExtensions\bin\Debug\CK.PnPExtensions.dll") | Out-Null
    $xmlTransform = New-Object CK.PnPExtensions.XMLTransform
    $xmlTransform.Initialize($transformPath)

    Get-SPOProvisioningTemplate -Out $FilePath -Handlers Lists,Fields,ContentTypes,CustomActions -TemplateProviderExtensions $xmlTransform

    Disconnect-SPOnline

}
catch
{
    Write-Host -ForegroundColor Red "Exception occurred!" 
    Write-Host -ForegroundColor Red "Exception Type: $($_.Exception.GetType().FullName)"
    Write-Host -ForegroundColor Red "Exception Message: $($_.Exception.Message)"
}