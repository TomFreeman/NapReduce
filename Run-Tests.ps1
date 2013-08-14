##############################################################################
#
# Script to run Nunit tests on a remote machine
#
# Uses FastNunit.exe to deal with spinning up as many users
# as possible.
#
##############################################################################
param ($testAssembly, $category, $resultsFileName)

# Grabber from the Store-Credentials script, see there for legal / requirements
function Import-PSCredential {
        param ( $Path = "credentials.enc.xml" )
 
        # Import credential file
        $import = Import-Clixml $Path
       
        # Test for valid import
        if ( $import.PSObject.TypeNames -notcontains 'Deserialized.ExportedPSCredential' ) {
                Throw "Input is not a valid ExportedPSCredential object, exiting."
        }
        $Username = $import.Username
       
        # Decrypt the password and store as a SecureString object for safekeeping
        $SecurePass = $import.EncryptedPassword | ConvertTo-SecureString
       
        # Build the new credential object
        $Credential = New-Object System.Management.Automation.PSCredential $Username, $SecurePass
        Write-Output $Credential
}

# If not already enabled, turn on PSRemoting
$remoteHost = "btmgsrvhpv02.brislabs.com"
$hosts = Get-Item WsMan:\localhost\Client\TrustedHosts

if ( $hosts.Value -notcontains $remoteHost )
{
	Write-Host "Enabling PSRemoting..."
	winrm quickconfig

	winrm set winrm/config/client '@{TrustedHosts="btmgsrvhpv02.brislabs.com"}'
}

# Create a unique Id for the job.
$testId = [System.Guid]::NewGuid().ToString()
Write-Host "Started Job: $testId"

$credentials = Import-PSCredential

$zipFileName = "input.zip"
#run packager to build a zip
Write-Host "Packaging up assembly and dependent files."
.\Packager.exe -a $testAssembly -o $zipFileName

$drive = New-PSDrive -Name J -PSProvider FileSystem -Root "\\$remoteHost\Jobs" -Credential $credentials

$dir = New-Item "J:\$testId" -ItemType Directory
Copy-Item $zipFileName -Destination "J:\$testId"

# Copy FastNunit and all dependencies (packager could do this, a standalone exe would be nice though.)
Copy-Item ".\FastNunit.exe" -Destination "J:\$testId"
".\FSharp.Core.dll", ".\nunit.framework.dll", "CommandLine.dll", "FSharp.Data.dll", "FSharp.Data.TypeProviders.dll" | % { Copy-Item $_ -Destination "J:\$testId" }

Remove-PSDrive -Name J

#run the tests in a remote session:
$results = invoke-command -ComputerName $remoteHost -Credential $credentials -port 80 -ScriptBlock {
	param ($ass, $cat, $id, $zip)

	# Crack open the zip file - in an ideal world we'd infer the local path
	cd "c:\Jobs\$id"
	$shell = New-Object -com shell.application
	
	[System.Int32]$yesToAll = 16

	$path = (pwd).Path
	
	Write-Host "Unzipping file at $path\$zip"

	$zipFile = $shell.namespace("$path\$zip")
	
	$destination = $shell.Namespace("$path")
	$destination.CopyHere($zipFile.Items(), $yesToAll)
	
	$resultsFile = "results.xml"

	Write-Host "Asking manager to start the tests."

	$job = @{
		Id = $id
		Path = "$path\FastNunit.exe"
		Arguments = "-a $ass -o $resultsFile"
	}
	
	if($cat)
	{
		$job.Arguments += " -c:$cat"
	}
	
	$response = Invoke-RestMethod -Uri http://localhost:8080/jobs -Method POST -Body $job -ContentType application/json
	
	# TODO: Check the response code and deal appropriately
	if ($response.StatusCode -ne 301)
	{
		throw "Failed to create a new job."
	}

	$jobPath = $response.Headers["Location"]

	Write-Host "Waiting for the job to finish."
	do {
		$jobStatus = Invoke-RestMethod -Uri $jobPath

		Start-Sleep -Seconds 5 
	} while ( $jobStatus.StatusCode -eq 201 )

	if ($jobStatus.StatusCode -ne 200)
	{
		throw "Job failed to complete."
	}

	Get-Content $resultsFile
} -ArgumentList $testAssembly, $category, $testId, $zipFileName

Set-Content $resultsFileName -Value $results