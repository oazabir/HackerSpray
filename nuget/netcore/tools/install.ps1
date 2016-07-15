param($installPath, $toolsPath, $package, $project)
Write-Host $installPath
Write-Host $toolsPath
Write-Host $project
Write-Host $package

$path = [System.IO.Path]
$projectPath = $path::GetDirectoryName($project.FileName)
Copy-File $path::Combine($installPath, "readme.txt") $projectPath


$readmefile = $path::Combine($projectPath, "\readme.txt")
$DTE.ExecuteCommand("File.OpenFile", $readmefile)