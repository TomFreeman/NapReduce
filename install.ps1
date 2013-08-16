param($installPath, $toolsPath, $package, $project)

$currentProfile = Get-Content $profile

$profile += @"

. $toolsPath\Run-Tests.ps1

"