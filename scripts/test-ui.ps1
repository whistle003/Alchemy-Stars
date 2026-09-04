param(
    [string]$ExecutablePath = '',
    [string]$CaptureSettingsPath = ''
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

function Show-Control($control, [string]$name) {
    if ($null -eq $control) {
        throw "UI control not found: $name"
    }

    if ($control.Current.IsOffscreen) {
        $pattern = $control.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)
        $pattern.ScrollIntoView()
        Start-Sleep -Milliseconds 150
    }
}

function Test-SettingsLabelAndField([int]$processId, [string]$labelId, [string]$fieldId) {
    $label = Find-ProcessControl $processId $labelId
    $field = Find-ProcessControl $processId $fieldId
    if ($null -eq $label -or $null -eq $field) {
        throw "Settings label or input is missing: $labelId / $fieldId"
    }
    if ([string]::IsNullOrWhiteSpace($label.Current.Name) -or [string]::IsNullOrWhiteSpace($field.Current.Name)) {
        throw "Settings label or input has no accessible name: $labelId / $fieldId"
    }
    if (-not $field.Current.IsKeyboardFocusable) {
        throw "Settings input is not keyboard focusable: $fieldId"
    }
    $labelBounds = $label.Current.BoundingRectangle
    $fieldBounds = $field.Current.BoundingRectangle
    if ($labelBounds.Width -le 0 -or $labelBounds.Height -le 0 -or $fieldBounds.Height -lt 40) {
        throw "Settings label or input is clipped: $labelId / $fieldId"
    }
    if ($labelBounds.Bottom -gt $fieldBounds.Top + 1) {
        throw "Settings label overlaps its input: $labelId / $fieldId"
    }
}

function Get-EditablePathPattern([int]$processId, [string]$fieldId) {
    $field = Find-ProcessControl $processId $fieldId
    if ($null -eq $field -or [string]::IsNullOrWhiteSpace($field.Current.Name)) {
        throw "Editable path input is missing or has no accessible name: $fieldId"
    }
    if (-not $field.Current.IsKeyboardFocusable) {
        throw "Editable path input is not keyboard focusable: $fieldId"
    }
    $pattern = $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    if ($pattern.Current.IsReadOnly) {
        throw "Path input does not accept typed or pasted text: $fieldId"
    }
    return $pattern
}

function Find-ProcessControl([int]$processId, [string]$automationId) {
    $processCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $processId)
    $idCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        $processCondition)
    foreach ($window in $windows) {
        if ($window.Current.AutomationId -eq $automationId) {
            return $window
        }
        $control = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCondition)
        if ($null -ne $control) {
            return $control
        }
    }
    return $null
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

function Save-WindowCapture($window, [string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        return
    }
    $absolutePath = if ([System.IO.Path]::IsPathRooted($path)) {
        [System.IO.Path]::GetFullPath($path)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $projectRoot $path))
    }
    $parent = Split-Path -Parent $absolutePath
    if (-not (Test-Path -LiteralPath $parent)) {
        [void](New-Item -ItemType Directory -Path $parent)
    }
    $bounds = $window.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.Left, [int]$bounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($absolutePath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
    Write-Output "UI smoke: settings capture saved to $absolutePath"
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
$expectedVersion = '1.1.6'

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
    Write-Output 'UI smoke: main window and version detected.'
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
    Write-Output 'UI smoke: owner-centered application message verified.'

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
    Write-Output 'UI smoke: settings dialog opened.'
    $settingsBounds = $settingsDialog.Current.BoundingRectangle
    if ($settingsBounds.Width -le 0 -or $settingsBounds.Height -le 0 -or
        $settingsBounds.Left -lt $windowBounds.Left -or $settingsBounds.Right -gt $windowBounds.Right -or
        $settingsBounds.Top -lt $windowBounds.Top -or $settingsBounds.Bottom -gt $windowBounds.Bottom) {
        throw 'Settings dialog is clipped by the application window.'
    }
    Write-Output 'UI smoke: settings dialog bounds verified.'
    $formatOptionIds = @(
        'CastOutputFormatRadioButton', 'FbxOutputFormatRadioButton',
        'SmdOutputFormatRadioButton', 'SeanimOutputFormatRadioButton'
    )
    foreach ($formatOptionId in $formatOptionIds) {
        $formatOption = Find-Control $settingsDialog $formatOptionId
        if ($null -eq $formatOption -or [string]::IsNullOrWhiteSpace($formatOption.Current.Name)) {
            throw "Accessible output format option was not found: $formatOptionId"
        }
        if (-not $formatOption.Current.IsKeyboardFocusable -or
            $formatOption.Current.BoundingRectangle.Width -le 0 -or
            $formatOption.Current.BoundingRectangle.Height -le 0) {
            throw "Output format option is clipped or not keyboard focusable: $formatOptionId (focusable=$($formatOption.Current.IsKeyboardFocusable), bounds=$($formatOption.Current.BoundingRectangle))"
        }
    }
    $castFormatOption = Find-Control $settingsDialog 'CastOutputFormatRadioButton'
    $castFormatSelection = $castFormatOption.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if (-not $castFormatSelection.Current.IsSelected) {
        throw 'The default .cast output format option is not selected.'
    }
    Write-Output 'UI smoke: four direct output format choices verified.'
    $castAnimationOnly = Find-Control $settingsDialog 'CastAnimationOnlyCheckBox'
    if ($null -eq $castAnimationOnly -or [string]::IsNullOrWhiteSpace($castAnimationOnly.Current.Name)) {
        throw 'Animation-only CAST setting is missing its accessible checkbox label.'
    }
    if (-not $castAnimationOnly.Current.IsEnabled) {
        throw 'Animation-only CAST setting should be enabled for the default .cast format.'
    }
    Show-Control $castAnimationOnly 'CastAnimationOnlyCheckBox'
    $castAnimationOnlyToggle = $castAnimationOnly.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $castAnimationOnlyToggle.Toggle()
    if ($castAnimationOnlyToggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
        throw 'Animation-only CAST setting did not toggle on.'
    }
    $relevantBonesOnly = Find-Control $settingsDialog 'BakeRelevantBonesOnlyCheckBox'
    if ($null -eq $relevantBonesOnly -or [string]::IsNullOrWhiteSpace($relevantBonesOnly.Current.Name)) {
        throw 'Relevant-bones-only setting is missing its accessible checkbox label.'
    }
    Show-Control $relevantBonesOnly 'BakeRelevantBonesOnlyCheckBox'
    $relevantBonesToggle = $relevantBonesOnly.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $relevantBonesToggle.Toggle()
    if ($relevantBonesToggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
        throw 'Relevant-bones-only setting did not toggle on.'
    }
    Save-WindowCapture $mainWindow $CaptureSettingsPath
    Select-Control (Find-Control $settingsDialog 'IkSettingsTab') 'IkSettingsTab'
    Start-Sleep -Milliseconds 250
    foreach ($side in @('Left', 'Right')) {
        foreach ($role in @('Start', 'Middle', 'End', 'Target')) {
            Test-SettingsLabelAndField $process.Id "${side}Ik${role}Label" "${side}Ik${role}TextBox"
        }
    }
    $leftCardField = Find-ProcessControl $process.Id 'LeftIkStartTextBox'
    $rightCardField = Find-ProcessControl $process.Id 'RightIkStartTextBox'
    if ($leftCardField.Current.BoundingRectangle.Right -ge $rightCardField.Current.BoundingRectangle.Left) {
        throw 'Left and right IK setting cards overlap.'
    }
    Write-Output 'UI smoke: output options and IK settings verified.'
    Invoke-Control (Find-ProcessControl $process.Id 'SettingsCloseButton') 'SettingsCloseButton'
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
    Write-Output 'UI smoke: animation and model-part context menus verified.'

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
    Write-Output 'UI smoke: language modes and About content verified.'

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

    $animationPath = Find-ProcessControl $process.Id 'AnimationPathTextBox'
    $animationPathPattern = Get-EditablePathPattern $process.Id 'AnimationPathTextBox'
    $leftPosePath = Find-ProcessControl $process.Id 'LeftPosePathTextBox'
    [void](Get-EditablePathPattern $process.Id 'LeftPosePathTextBox')
    [void](Get-EditablePathPattern $process.Id 'RightPosePathTextBox')
    [void](Get-EditablePathPattern $process.Id 'OutputFolderTextBox')
    [void](Get-EditablePathPattern $process.Id 'LayerPathTextBox')
    $animationPath.SetFocus()
    $animationPathPattern.SetValue('"D:\Temporary\pasted animation.cast"')
    $leftPosePath.SetFocus()
    Start-Sleep -Milliseconds 150
    if ($animationPathPattern.Current.Value -ne 'D:\Temporary\pasted animation.cast') {
        throw 'Pasted path quotes and surrounding whitespace were not normalized.'
    }

    Select-Control (Find-Control $projectWindow 'PartsTab') 'PartsTab'
    Start-Sleep -Milliseconds 150
    [void](Get-EditablePathPattern $process.Id 'ModelPathTextBox')
    Select-Control (Find-Control $projectWindow 'AnimationsTab') 'AnimationsTab'
    Start-Sleep -Milliseconds 150
    Write-Output 'UI smoke: animation, pose, layer, output-folder, and model paths accept pasted text.'

    $layerList = Find-Control $projectWindow 'LayerList'
    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $layerTextBox = $layerList.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $editCondition)
    Test-ContextMenu $process $layerTextBox 'ImportLayersContextMenuItem' 'ImportAnimationsContextMenuItem'

    Write-Output "UI smoke passed: owner-centered messages, protected language/About layout, responsive grouped settings with eight accessible non-overlapping IK fields and four direct formats, editable pasted paths, output-option toggles, system-language mode, $($toolbarButtonIds.Count) accessible toolbar buttons, context imports, and bilingual About content."
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
