# Створює ярлик "Document Generator" з логотипом на робочому столі (і в меню Пуск).
# Запускати з папки, де лежить DocumentGenerator.exe (або після збірки з publish\win-x64).
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$exe = Join-Path $dir 'DocumentGenerator.exe'
if (-not (Test-Path $exe)) { $exe = Join-Path $dir 'publish\win-x64\DocumentGenerator.exe' }
if (-not (Test-Path $exe)) { throw 'DocumentGenerator.exe не знайдено поруч зі скриптом.' }
$exe = (Resolve-Path $exe).Path
$ico = Join-Path (Split-Path $exe) 'App.ico'
if (-not (Test-Path $ico)) { $ico = Join-Path $dir 'App.ico' }
$iconLocation = if (Test-Path $ico) { (Resolve-Path $ico).Path } else { "$exe,0" }

$shell = New-Object -ComObject WScript.Shell
$targets = @(
  [Environment]::GetFolderPath('Desktop'),
  (Join-Path ([Environment]::GetFolderPath('Programs')) '')
)
foreach ($folder in $targets) {
  $lnk = $shell.CreateShortcut((Join-Path $folder 'Document Generator.lnk'))
  $lnk.TargetPath       = $exe
  $lnk.WorkingDirectory = Split-Path $exe
  $lnk.IconLocation     = $iconLocation
  $lnk.Description      = 'Document Generator'
  $lnk.Save()
}
Write-Host 'Ярлики створено: робочий стіл і меню Пуск.' -ForegroundColor Green
