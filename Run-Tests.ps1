param ($testAssembly, $category)

#winrm quickconfig

#winrm set winrm/config/client '@{TrustedHosts="btmgsrvhpv02.brislabs.com"}'

$remoteHost = "btmgsrvhpv02.brislabs.com"
$testId = [System.Guid]::NewGuid().ToString()

$credentials = Get-Credential -Message "Please enter the brislabs credentials for a user with access to: $remoteHost" 

$zipFileName = "input.zip"
#run packager to build a zip
.\Packager.exe -a $testAssembly -o $zipFileName

New-PSDrive -Name J -PSProvider FileSystem -Root "\\$remoteHost\Jobs" -Credential $credentials

New-Item "J:\$testId" -ItemType Directory
Copy-Item $zipFileName -Destination "J:\$testId"

# Copy FastNunit and all dependencies (packager could do this, a standalone exe would be nice though.)
Copy-Item ".\FastNunit.exe" -Destination "J:\$testId"
".\FSharp.Core.dll", ".\nunit.framework.dll", "CommandLine.dll", "FSharp.Data.dll", "FSharp.Data.TypeProviders.dll" | % { Copy-Item $_ -Destination "J:\$testId" }

Remove-PSDrive -Name J

#run the tests in a remote session:
invoke-command -ComputerName $remoteHost -Credential $credentials -port 80 -ScriptBlock {
	param ($ass, $cat, $id)
	# Crack open the zip file - in an ideal world we'd infer the local path
	cd "c:\Jobs\$id"
	$shell = New-Object -com shell.application
	
	$path = (pwd).Path
	
	$zipFile = $shell.namespace("$path\$zipFileName")
	
	$destination = $shell.Namespace("$path")
	$destination.CopyHere($zipFile.Items())
	
	$resultsFile = "results.xml"
	
	#Run the fast-nunit exe to do all those lovely tests.
	$commandString = "$path\FastNunit.exe -a $ass -o $resultsFile"
	
	if($category)
	{
		$commandString += " -c:$cat"
	}
	
	iex $commandString
	
	Get-Content $resultsFile
} -ArgumentList $testAssembly, $category, $testId