param(
    [string]$ExecutablePath = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    Join-Path $projectRoot 'release\Alchemy Stars\Alchemy Stars.exe'
} elseif ([System.IO.Path]::IsPathRooted($ExecutablePath)) {
    [System.IO.Path]::GetFullPath($ExecutablePath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $projectRoot $ExecutablePath))
}

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

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(System.IntPtr hWnd, int x, int y, int width, int height, bool repaint);
}
'@
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
        $process.Kill($true)
        if (-not $process.WaitForExit(5000)) {
            throw "UI smoke process did not exit after being terminated: $($process.Id)"
        }
    }
}

function Select-Language([System.Diagnostics.Process]$process, $button, [string]$menuItemId) {
    Invoke-Control $button 'LanguageButton'
    $menuItem = $null
    for ($attempt = 0; $attempt -lt 20 -and $null -eq $menuItem; $attempt++) {
        Start-Sleep -Milliseconds 100
        $menuItem = Find-ProcessControl $process.Id $menuItemId
    }
    Invoke-Control $menuItem $menuItemId
    Start-Sleep -Milliseconds 300
}

$previousSettingsPath = $env:ALCHEMY_STARS_SETTINGS_PATH
$testSettingsPath = Join-Path ([System.IO.Path]::GetTempPath()) ("alchemy-stars-ui-smoke-" + [Guid]::NewGuid().ToString('N') + '.json')
$env:ALCHEMY_STARS_SETTINGS_PATH = $testSettingsPath
$process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
try {
    $expectedVersion = '1.1.0'

    if (-not $process.WaitForInputIdle(10000)) {
        throw 'Application did not become input-idle within 10 seconds.'
    }

    $mainWindow = Find-Window $process.Id 'MainWindow'
    if ($null -eq $mainWindow) {
        throw 'Main window was not found.'
    }

    $initialTitle = $mainWindow.Current.Name
    if ($initialTitle -notmatch [regex]::Escape($expectedVersion)) {
        throw "Main window title does not contain version $expectedVersion`: $initialTitle"
    }
    $toolbarButtonIds = @(
        'OpenProjectButton', 'SaveProjectButton', 'SaveProjectAsButton',
        'AddAnimationsButton', 'ExportAnimationsButton', 'RemoveAnimationsButton', 'OutputFolderButton',
        'EnableLeftIkButton', 'EnableRightIkButton', 'DisableLeftIkButton', 'DisableRightIkButton',
        'AddPrefixButton', 'AddSuffixButton', 'SetLeftPoseButton', 'SetRightPoseButton',
        'AddLayersButton', 'RemoveLayersButton', 'AddPartsButton', 'RemovePartsButton',
        'SettingsButton', 'ExperimentalScriptsButton'
    )
    foreach ($toolbarButtonId in $toolbarButtonIds) {
        $toolbarButton = Find-Control $mainWindow $toolbarButtonId
        if ($null -eq $toolbarButton) {
            throw "Toolbar button was not found: $toolbarButtonId"
        }
        if ([string]::IsNullOrWhiteSpace($toolbarButton.Current.Name)) {
            throw "Toolbar button has no accessible name: $toolbarButtonId"
        }
    }
    Invoke-Control (Find-Control $mainWindow 'ExportAnimationsButton') 'ExportAnimationsButton'
    $messageCloseButton = $null
    for ($attempt = 0; $attempt -lt 20 -and $null -eq $messageCloseButton; $attempt++) {
        Start-Sleep -Milliseconds 100
        $messageCloseButton = Find-ProcessControl $process.Id 'AppMessageCloseButton'
    }
    if ($null -eq $messageCloseButton) {
        throw 'Owner-centered application message window was not shown.'
    }
    $messageWindow = $messageCloseButton
    $treeWalker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    while ($null -ne $messageWindow -and $messageWindow.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
        $messageWindow = $treeWalker.GetParent($messageWindow)
    }
    if ($null -eq $messageWindow) {
        throw 'Application message owner window could not be resolved.'
    }
    $mainBounds = $mainWindow.Current.BoundingRectangle
    $messageBounds = $messageWindow.Current.BoundingRectangle
    $mainCenterX = $mainBounds.Left + ($mainBounds.Width / 2)
    $mainCenterY = $mainBounds.Top + ($mainBounds.Height / 2)
    $messageCenterX = $messageBounds.Left + ($messageBounds.Width / 2)
    $messageCenterY = $messageBounds.Top + ($messageBounds.Height / 2)
    if ([Math]::Abs($mainCenterX - $messageCenterX) -gt 4 -or [Math]::Abs($mainCenterY - $messageCenterY) -gt 4) {
        throw 'Application message window is not centered over its owner.'
    }
    Invoke-Control $messageCloseButton 'AppMessageCloseButton'
    Start-Sleep -Milliseconds 150

    [void][UiSmokeMouse]::MoveWindow($process.MainWindowHandle, 40, 40, 900, 520, $true)
    Start-Sleep -Milliseconds 300
    $languageButton = Find-Control $mainWindow 'LanguageButton'
    $aboutButton = Find-Control $mainWindow 'AboutButton'
    $languageBounds = $languageButton.Current.BoundingRectangle
    $aboutBounds = $aboutButton.Current.BoundingRectangle
    $windowBounds = $mainWindow.Current.BoundingRectangle
    if ($languageBounds.Width -lt 44 -or $aboutBounds.Width -lt 44 -or
        $languageBounds.Right -gt $aboutBounds.Left -or $aboutBounds.Right -gt $windowBounds.Right) {
        throw 'The protected language/About area overlaps or clips at the minimum window size.'
    }

    [void][UiSmokeMouse]::MoveWindow($process.MainWindowHandle, 40, 40, 1366, 768, $true)
    Start-Sleep -Milliseconds 300
    $windowBounds = $mainWindow.Current.BoundingRectangle

    Invoke-Control (Find-Control $mainWindow 'SettingsButton') 'SettingsButton'
    $settingsDialog = $null
    for ($attempt = 0; $attempt -lt 20 -and $null -eq $settingsDialog; $attempt++) {
        Start-Sleep -Milliseconds 100
        $settingsDialog = Find-ProcessControl $process.Id 'SettingsTabs'
    }
    if ($null -eq $settingsDialog) {
        throw 'Redesigned settings dialog was not shown.'
    }
    $settingsBounds = $settingsDialog.Current.BoundingRectangle
    if ($settingsBounds.Width -le 0 -or $settingsBounds.Height -le 0 -or
        $settingsBounds.Left -lt $windowBounds.Left -or $settingsBounds.Right -gt $windowBounds.Right -or
        $settingsBounds.Top -lt $windowBounds.Top -or $settingsBounds.Bottom -gt $windowBounds.Bottom) {
        throw 'Settings dialog is clipped by the application window.'
    }
    $formatCombo = Find-ProcessControl $process.Id 'OutputFormatComboBox'
    if ($null -eq $formatCombo) {
        throw 'Default output format selector was not found.'
    }
    $expandPattern = $formatCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expandPattern.Expand()
    Start-Sleep -Milliseconds 200
    $processCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $process.Id)
    $listItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $formatCondition = New-Object System.Windows.Automation.AndCondition($processCondition, $listItemCondition)
    $formatNames = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $formatCondition) | ForEach-Object { $_.Current.Name })
    foreach ($format in @('.cast', '.fbx', '.smd', '.seanim')) {
        if ($formatNames -notcontains $format) {
            throw "Output format is missing from settings: $format"
        }
    }
    $expandPattern.Collapse()
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Start-Sleep -Milliseconds 200
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
    $explicitLanguage = if ($initialTitle -match '炼金之星') { 'EnglishLanguageMenuItem' } else { 'ChineseLanguageMenuItem' }
    Select-Language $process $languageButton $explicitLanguage
    $switchedTitle = $mainWindow.Current.Name
    if ($switchedTitle -eq $initialTitle) {
        throw "Language switch did not update the window title: $switchedTitle"
    }
    if ($switchedTitle -notmatch [regex]::Escape($expectedVersion)) {
        throw "Localized window title does not contain version $expectedVersion`: $switchedTitle"
    }

    Select-Language $process $languageButton 'SystemLanguageMenuItem'
    if ($mainWindow.Current.Name -ne $initialTitle) {
        throw 'Returning to automatic system language did not restore the initial language.'
    }

    Invoke-Control (Find-Control $mainWindow 'AboutButton') 'AboutButton'
    $aboutValidation = $null
    for ($attempt = 0; $attempt -lt 20 -and $null -eq $aboutValidation; $attempt++) {
        Start-Sleep -Milliseconds 250
        $aboutValidation = Find-ProcessControl $process.Id 'AboutValidation'
    }
    if ($null -eq $aboutValidation -or $aboutValidation.Current.Name -notmatch 'Maya 2025') {
        throw 'About window was not shown with the current Maya 2025 validation information.'
    }
    $aboutVersion = Find-ProcessControl $process.Id 'AboutAppVersion'
    if ($null -eq $aboutVersion -or $aboutVersion.Current.Name -ne $expectedVersion) {
        throw "About window does not show version $expectedVersion."
    }
    $initialAboutValidation = $aboutValidation.Current.Name
    Select-Language $process $languageButton $explicitLanguage
    for ($attempt = 0; $attempt -lt 20 -and $aboutValidation.Current.Name -eq $initialAboutValidation; $attempt++) {
        Start-Sleep -Milliseconds 100
    }
    if ($aboutValidation.Current.Name -eq $initialAboutValidation -or $aboutValidation.Current.Name -notmatch 'Maya 2025') {
        throw 'About window did not update its validation information after switching languages.'
    }
    Select-Language $process $languageButton 'SystemLanguageMenuItem'

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

    Write-Output "UI smoke passed: owner-centered messages, protected language/About layout, unclipped settings with four formats, system-language mode, $($toolbarButtonIds.Count) accessible toolbar buttons, context imports, and bilingual About content."
}
finally {
    Stop-TestProcess $process
    if ($null -eq $previousSettingsPath) {
        Remove-Item Env:ALCHEMY_STARS_SETTINGS_PATH -ErrorAction SilentlyContinue
    } else {
        $env:ALCHEMY_STARS_SETTINGS_PATH = $previousSettingsPath
    }
    if (Test-Path -LiteralPath $testSettingsPath) {
        Remove-Item -LiteralPath $testSettingsPath -Force
    }
}
