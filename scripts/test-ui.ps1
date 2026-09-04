$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'release\Alchemy Stars\Alchemy Stars.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published application not found: $executable"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
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

    Write-Output "UI smoke passed: $initialTitle -> $switchedTitle; About opened successfully."
}
finally {
    if (-not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit()
    }
}
