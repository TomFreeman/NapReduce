param($installPath, $toolsPath, $package, $project)

$dir = split-Path $MyInvocation.MyCommand.Path;
& "$dir\7b26a8d401de45929ff6e88126f559cd.ps1" $installPath $toolsPath $package $project;

