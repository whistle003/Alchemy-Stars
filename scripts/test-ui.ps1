$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'release\Alchemy Stars\Alchemy Stars.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published application not found: $executable"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System.Runtime.InteropServices;

public static class UiSmokeMouse
{
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint x, uint y, uint data, System.UIntPtr extraInfo);
}
'@
$logPath = Join-Path (Split-Path -Parent $executable) 'Alchemy-Stars-Log.log'
$logStartLineCount = if (Test-Path -LiteralPath $logPath) { @(Get-Content -LiteralPath $logPath).Count } else { 0 }

function Find-Window([int]$processId, [string]$automationId) {
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $processId)
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            $condition)

        foreach ($window in $windows) {
            if ($window.Current.AutomationId -eq $automationId) {
                return $window
            }
        }

        Start-Sleep -Milliseconds 250
    }

    return $null
}

function Find-Control($root, [string]$automationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-Control($control, [string]$name) {
    if ($null -eq $control) {
        throw "UI control not found: $name"
    }

    $pattern = $control.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Select-Control($control, [string]$name) {
    if ($null -eq $control) {
        throw "UI control not found: $name"
    }

    $pattern = $control.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
}

function Find-ProcessControl([int]$processId, [string]$automationId) {
    $processCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $processId)
    $idCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    $condition = New-Object System.Windows.Automation.AndCondition($processCondition, $idCondition)
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
}

function Test-ContextMenu([System.Diagnostics.Process]$process, $control, [string]$menuItemId, [string]$unexpectedMenuItemId = '') {
    if ($null -eq $control) {
        throw "Context-menu surface not found for $menuItemId"
    }

    $shell = New-Object -ComObject WScript.Shell
    [void]$shell.AppActivate($process.Id)
    $control.SetFocus()
    Start-Sleep -Milliseconds 100
    $bounds = $control.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
        throw "Context-menu surface has no visible bounds: $menuItemId"
    }
    $point = New-Object System.Drawing.Point(
        [int]($bounds.Left + ($bounds.Width / 2)),
        [int]($bounds.Top + ($bounds.Height / 2)))
    [System.Windows.Forms.Cursor]::Position = $point
    [UiSmokeMouse]::mouse_event(0x0008, 0, 0, 0, [System.UIntPtr]::Zero)
    [UiSmokeMouse]::mouse_event(0x0010, 0, 0, 0, [System.UIntPtr]::Zero)

    $menuItem = $null
    for ($attempt = 0; $attempt -lt 20 -and $null -eq $menuItem; $attempt++) {
        Start-Sleep -Milliseconds 100
        $menuItem = Find-ProcessControl $process.Id $menuItemId
    }
    if ($null -eq $menuItem) {
        throw "Context menu item did not open: $menuItemId"
    }
    if ($unexpectedMenuItemId -and $null -ne (Find-ProcessControl $process.Id $unexpectedMenuItemId)) {
        throw "Unexpected context menu item opened: $unexpectedMenuItemId"
    }

    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
}

function Stop-TestProcess([System.Diagnostics.Process]$process) {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}

$process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
try {
    if (-not $process.WaitForInputIdle(10000)) {
        throw 'Application did not become input-idle within 10 seconds.'
    }

    $mainWindow = Find-Window $process.Id 'MainWindow'
    if ($null -eq $mainWindow) {
        throw 'Main window was not found.'
    }

    $initialTitle = $mainWindow.Current.Name
    $animationList = Find-Control $mainWindow 'AnimationList'
    $listItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $animationItems = $animationList.FindAll([System.Windows.Automation.TreeScope]::Children, $listItemCondition)
    if ($animationItems.Count -ne 0) {
        throw 'Application did not start with an empty batch.'
    }

    Test-ContextMenu $process $animationList 'ImportAnimationsContextMenuItem'
    Select-Control (Find-Control $mainWindow 'PartsTab') 'PartsTab'
    Start-Sleep -Milliseconds 250
    Test-ContextMenu $process (Find-Control $mainWindow 'ModelPartsList') 'ImportPartsContextMenuItem'
    Select-Control (Find-Control $mainWindow 'AnimationsTab') 'AnimationsTab'
    Start-Sleep -Milliseconds 250

    $languageButton = Find-Control $mainWindow 'LanguageButton'
    Invoke-Control $languageButton 'LanguageButton'
    Start-Sleep -Milliseconds 500
    $switchedTitle = $mainWindow.Current.Name
    if ($switchedTitle -eq $initialTitle) {
        throw "Language switch did not update the window title: $switchedTitle"
    }

    Invoke-Control $languageButton 'LanguageButton'
    Start-Sleep -Milliseconds 500
    if ($mainWindow.Current.Name -ne $initialTitle) {
        throw 'Switching back did not restore the original language.'
    }

    Invoke-Control (Find-Control $mainWindow 'AboutButton') 'AboutButton'
    $aboutWasShown = $false
    for ($attempt = 0; $attempt -lt 20 -and -not $aboutWasShown; $attempt++) {
        Start-Sleep -Milliseconds 250
        $newLogLines = @(Get-Content -LiteralPath $logPath | Select-Object -Skip $logStartLineCount)
        $aboutWasShown = $newLogLines -match 'About window shown: True'
    }
    if (-not $aboutWasShown) {
        throw 'About button did not create and show the About window.'
    }

    Stop-TestProcess $process

    $sprintProject = Join-Path $projectRoot 'fork\AlchemyStars\Example\Hawk\HawkSprint.aprj'
    $process = Start-Process -FilePath $executable -ArgumentList "`"$sprintProject`"" -PassThru -WindowStyle Hidden
    if (-not $process.WaitForInputIdle(10000)) {
        throw 'Project UI did not become input-idle within 10 seconds.'
    }

    $projectWindow = Find-Window $process.Id 'MainWindow'
    if ($null -eq $projectWindow) {
        throw 'Project window was not found.'
    }

    $layerList = Find-Control $projectWindow 'LayerList'
    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $layerTextBox = $layerList.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $editCondition)
    Test-ContextMenu $process $layerTextBox 'ImportLayersContextMenuItem' 'ImportAnimationsContextMenuItem'

    Write-Output "UI smoke passed: all context imports (including nested layer priority), $initialTitle -> $switchedTitle, and About window."
}
finally {
    Stop-TestProcess $process
}
