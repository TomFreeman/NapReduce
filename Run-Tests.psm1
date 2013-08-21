##############################################################################
#
# Script to run Nunit tests on a remote machine
#
# Uses FastNunit.exe to deal with spinning up as many users
# as possible.
#
##############################################################################

function Get-ScriptDirectory {   
	$invocation = (Get-Variable MyInvocation -Scope 1).Value
	$script = [IO.FileInfo] $invocation.MyCommand.Path
	if ([IO.File]::Exists($script)) {
    	Return (Split-Path $script.Fullname)
	} else {
		return $null
	}
}

$scriptPath = Get-ScriptDirectory
Write-Host "Script residing at $scriptPath"

<# 
 .Synopsis
  Run a bunch of Nunit tests on our big ol' server.

 .Description
  Packages up dependencies, then copies them to a share on the test server
  before running a job to execute the tests.

 .Parameter TestAssembly
  The assembly the tests are located in.

 .Parameter Category (optional)
  The category of tests to run, as defined in Nunit.

 .Parameter ResultsFileName
  The filename to store the results in.

 .Parameter AdditionalFiles (optional)
  Any additional files that the tests depend on, assembly dependencies will be automatically resolved
  so this list should be additional non-code dependencies, i.e. config files.

 .Example
   # Run all the tests in a given assembly and save to results.xml
   Run-Tests -TestAsssembly Nunit.Tests.dll -ResultsFileName results.xml

 .Example
   # Run all tests in a category
   Run-Tests -TestAsssembly Nunit.Tests.dll -Category sanity -ResultsFileName results.xml

 .Example
   # Run all tests with a couple of additional files
   Run-Tests -TestAssembly Nunit.Tests.dll -ResultsFileName results.xml -AdditionalFiles Nunit.Tests.dll.config,Additional.Data.xml
#>
function Run-Tests() {
	param ($TestAssembly, $Category, $ResultsFileName, $AdditionalFiles)

	Write-Host "Importing credentials management."
	. "$scriptPath\Store-Credentials.ps1"

	Write-Host "Importing stored credentials."
	$credentials = Import-PSCredential

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
		 Invoke-Expression "$scriptPath\Packager.exe -a $TestAssembly -o $zipFileName" 
	}
	else
	{
	     $files = ([String]::Join(",", $AdditionalFiles))

		 Write-Host "Additional files added to zip:"
		 Write-Host $files

		 $command = "$scriptPath\Packager.exe -a $TestAssembly -o $zipFileName -f $files"
		 Write-Host $command

		 Invoke-Expression $command 
	}

	$drive = New-PSDrive -Name J -PSProvider FileSystem -Root "\\$remoteHost\Jobs" -Credential $credentials

	$dir = New-Item "J:\$testId" -ItemType Directory
	Copy-Item $zipFileName -Destination "J:\$testId"

	# Copy FastNunit and all dependencies (packager could do this, a standalone exe would be nice though.)
	Copy-Item "$scriptPath\FastNunit.exe" -Destination "J:\$testId"
	"$scriptPath\FSharp.Core.dll", `
	"$scriptPath\nunit.framework.dll", `
	"$scriptPath\CommandLine.dll", `
	"$scriptPath\FSharp.Data.dll", `
	"$scriptPath\FSharp.Data.TypeProviders.dll", `
	"$scriptPath\Packager.exe" | % { Copy-Item $_ -Destination "J:\$testId" }

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
			arguments = "-a ""$ass"" -o ""$resultsFile"" -i ""$id"" -w ""$path"""
		}
	
		if($cat)
		{
			$job.Arguments += " -c $cat"
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

	Write-Host "Deleting temporary file $zipFileName"
	Remove-Item $zipFileName -Force


	Set-Content $ResultsFileName -Value $results
}

export-ModuleMember -Function Run-Tests