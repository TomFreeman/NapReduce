param($installPath, $toolsPath, $package, $project)

$paths = ($env:PSModulePath).Split(';')

$path = $paths[0] + "\Run-Tests"

if (-not (Test-Path $path))
{
	New-Item -Path $path -Type directory -Force
}

Write-Host "Copying module files over to $path"

Copy-Item -Path $toolsPath\Run-Tests.psm1 -Destination $path -Force
Copy-Item -Path $toolsPath\credentials.enc.xml -Destination $path -Force

Import-Module Run-Tests.psm1