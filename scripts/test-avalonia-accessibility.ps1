param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
$executable = Join-Path $publishPath 'AlchemyStars.Avalonia.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Avalonia executable was not found: $executable"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$arguments = '--accessibility-smoke --culture en-US --window-size 900x600 --page animations --dialog success'
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $publishPath -PassThru
try {
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $process.Id)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $window = $null
    do {
        Start-Sleep -Milliseconds 200
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            $processCondition)
    } while ($null -eq $window -and [DateTime]::UtcNow -lt $deadline -and -not $process.HasExited)

    if ($null -eq $window) {
        throw 'Windows UI Automation could not discover the Avalonia main window.'
    }

    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $requiredButtons = @('New', 'Open', 'Save', 'Save as', 'Export all', 'Animations', 'Model parts', 'Settings', 'About', 'Close')
    $keyTargets = @('New', 'Open', 'Save', 'Save as', 'Export all', 'Close')
    $elements = @{}
    foreach ($name in $requiredButtons) {
        $nameCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $name)
        $condition = [System.Windows.Automation.AndCondition]::new($buttonCondition, $nameCondition)
        $candidates = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $element = $null
        for ($index = 0; $index -lt $candidates.Count; $index++) {
            $candidate = $candidates.Item($index)
            if ($candidate.Current.IsKeyboardFocusable) {
                $element = $candidate
                break
            }
        }
        if ($null -eq $element) {
            throw "Required accessible button was not exposed: $name"
        }
        $elements[$name] = $element
    }

    foreach ($name in $keyTargets) {
        $bounds = $elements[$name].Current.BoundingRectangle
        if ($bounds.Width -lt 43 -or $bounds.Height -lt 43) {
            throw "Accessible target '$name' is smaller than 44x44 DIPs: $($bounds.Width)x$($bounds.Height)"
        }
    }

    $closeButton = $elements['Close']
    if (-not $closeButton.Current.HasKeyboardFocus) {
        $closeButton.SetFocus()
        Start-Sleep -Milliseconds 150
    }
    if (-not $closeButton.Current.HasKeyboardFocus) {
        throw 'The centered dialog did not provide a reliable keyboard focus target.'
    }

    Write-Output 'Windows UI Automation names, keyboard focus and 44x44 key targets: PASS'
}
finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(5000) | Out-Null
    }
    $process.Dispose()
}
