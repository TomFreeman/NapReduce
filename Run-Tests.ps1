﻿##############################################################################
#
# Script to run Nunit tests on a remote machine
#
# Uses FastNunit.exe to deal with spinning up as many users
# as possible.
#
##############################################################################
param ($TestAssembly, $Category, $ResultsFileName, $AdditionalFiles)

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
if ([String]::IsNullOrEmpty($AdditionalFiles))
{
	.\Packager.exe -a $TestAssembly -o $zipFileName
}
else
{
	.\Packager.exe -a $TestAssembly -o $zipFileName -f ([String]::Join(",", $AdditionalFiles))
}

$drive = New-PSDrive -Name J -PSProvider FileSystem -Root "\\$remoteHost\Jobs" -Credential $credentials

$dir = New-Item "J:\$testId" -ItemType Directory
Copy-Item $zipFileName -Destination "J:\$testId"

# Copy FastNunit and all dependencies (packager could do this, a standalone exe would be nice though.)
Copy-Item ".\FastNunit.exe" -Destination "J:\$testId"
".\FSharp.Core.dll", ".\nunit.framework.dll", "CommandLine.dll", "FSharp.Data.dll", "FSharp.Data.TypeProviders.dll", "Packager.exe" | % { Copy-Item $_ -Destination "J:\$testId" }

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
		sessionId = $id
		path = "$path\FastNunit.exe"
		arguments = "-a ""$path\$ass"" -o ""$path\$resultsFile"" -w ""$path"""
	}
	
	if($cat)
	{
		$job.Arguments += " -c:$cat"
	}
	
	$jobText = ConvertTo-Json $job -Compress

	$response = Invoke-RestMethod -Uri http://localhost:8080/jobs -Method POST -Body $jobText -ContentType application/json
	
	# TODO: Check the response code and deal appropriately
	if ($response -eq $null)
	{
		throw "Failed to create a new job."
	}

	$jobPath = "http://localhost:8080/job/$id"

	Write-Host "Waiting for the job to finish."
	do {
		$jobStatus = Invoke-RestMethod -Uri $jobPath

		Start-Sleep -Seconds 5 
	} while ( -not $jobStatus.completed )


	Write-Host "Job finished, output:"
	Write-Host $jobStatus
	
	Get-Content $resultsFile
} -ArgumentList $TestAssembly, $Category, $testId, $zipFileName

Set-Content $ResultsFileName -Value $results