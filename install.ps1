param($installPath, $toolsPath, $package, $project)

if (-not (Test-Path $Profile))
{
	# Command to create a PowerShell profile
	New-Item -path $profile -type file -force
}

$currentProfile = Get-Content $profile

if ($currentProfile -notcontains "Run-Tests.ps1")
{
	$currentProfile += @"
	. $toolsPath\Run-Tests.ps1
	"@

	Set-Content -Path $profile -Value $currentProfile
}