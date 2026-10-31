$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;

public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

public static class W
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);

    public static IntPtr FindByTitle(string title)
    {
        return FindWindow(null, title);
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
"@

[void][W]::SetProcessDpiAwarenessContext([IntPtr]::new(-4))
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Pump([int]$milliseconds) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($milliseconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 15
    }
}

function WaitWindow([string]$title, [int]$milliseconds) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($milliseconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        $handle = [W]::FindByTitle($title)
        if ($handle -ne [IntPtr]::Zero) { return $handle }
        Start-Sleep -Milliseconds 40
    }
    return [IntPtr]::Zero
}

function Assert([bool]$condition, [string]$message) {
    if (-not $condition) { throw "ASSERT FAILED: $message" }
    Write-Host ("PASS " + $message)
}

function GetRect([IntPtr]$handle) {
    $rect = New-Object RECT
    if (-not [W]::GetWindowRect($handle, [ref]$rect)) { throw "GetWindowRect failed" }
    return $rect
}

function PostMouse([IntPtr]$handle, [int]$message, [int]$x, [int]$y, [bool]$down) {
    $lParam = [IntPtr]([int64](($y -shl 16) -bor ($x -band 0xFFFF)))
    $wParam = [IntPtr]$(if ($down) { 1 } else { 0 })
    [void][W]::PostMessage($handle, $message, $wParam, $lParam)
}

$snap = $null
$form = $null
$pin = [IntPtr]::Zero
$overlay = [IntPtr]::Zero
$passed = $false

try {
    Get-Process SnapMove -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300

    Write-Host "STEP 1: colorful reference window"
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'SnapMove E2E Reference'
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(200, 150)
    $form.ClientSize = New-Object System.Drawing.Size(640, 400)
    $form.TopMost = $true
    $form.add_Paint({
        param($sender, $eventArgs)
        $g = $eventArgs.Graphics
        $client = $sender.ClientRectangle
        $colors = @(
            [System.Drawing.Color]::FromArgb(255, 226, 62, 62),
            [System.Drawing.Color]::FromArgb(255, 236, 155, 45),
            [System.Drawing.Color]::FromArgb(255, 45, 170, 160),
            [System.Drawing.Color]::FromArgb(255, 55, 143, 215),
            [System.Drawing.Color]::FromArgb(255, 149, 112, 224)
        )
        $band = [int]($client.Width / 5)
        for ($i = 0; $i -lt 5; $i++) {
            $brush = New-Object System.Drawing.SolidBrush($colors[$i])
            $g.FillRectangle($brush, $i * $band, 0, $band + 1, $client.Height)
            $brush.Dispose()
        }
        $font = New-Object System.Drawing.Font('Microsoft YaHei UI', 30, [System.Drawing.FontStyle]::Bold)
        $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
        $g.DrawString('SnapMove E2E', $font, $white, 24, 34)
        $g.DrawString('BORDERLESS PIN', $font, $white, 24, 110)
        $white.Dispose()
        $font.Dispose()
    })
    $form.Show()
    Pump 400

    Write-Host "STEP 2: start SnapMove tray app"
    $snap = Start-Process -FilePath (Join-Path $PWD 'SnapMove.exe') -PassThru
    Pump 1200
    Assert (-not $snap.HasExited) 'tray app is running'

    $logPath = Join-Path $env:TEMP 'SnapMove\snapmove.log'
    if (Test-Path $logPath) {
        $registered = Select-String -Path $logPath -Pattern 'Hotkey registered' | Select-Object -Last 1
        Assert ($null -ne $registered) ('a global hotkey was registered: ' + $registered.Line.Trim())
    }

    Write-Host "STEP 3: trigger capture by messaging the running instance"
    $message = [W]::RegisterWindowMessage('SnapMove_ShowCapture')
    $hotkeyHost = WaitWindow 'SnapMove_HotkeyHost' 5000
    Assert ($hotkeyHost -ne [IntPtr]::Zero) 'hotkey host window is reachable'
    [void][W]::PostMessage($hotkeyHost, [int]$message, [IntPtr]::Zero, [IntPtr]::Zero)
    $overlay = WaitWindow 'SnapMove_Overlay' 8000
    Assert ($overlay -ne [IntPtr]::Zero) 'capture overlay appeared'
    Pump 200

    Write-Host "STEP 4: drag a 400x250 selection over the reference window"
    PostMouse $overlay 0x0201 300 250 $true
    Pump 40
    PostMouse $overlay 0x0200 700 500 $true
    Pump 40
    PostMouse $overlay 0x0202 700 500 $false

    $pin = WaitWindow 'SnapMove_Pin' 8000
    Assert ($pin -ne [IntPtr]::Zero) 'pinned window appeared'

    Write-Host "STEP 4b: verify captured content through the clipboard"
    # The tray app copies the image right after showing the pin; poll for it instead of racing.
    $clip = $null
    $clipDeadline = [DateTime]::UtcNow.AddSeconds(5)
    while ([DateTime]::UtcNow -lt $clipDeadline) {
        [System.Windows.Forms.Application]::DoEvents()
        $candidate = $null
        try { $candidate = [System.Windows.Forms.Clipboard]::GetImage() } catch { $candidate = $null }
        if ($null -ne $candidate) {
            if ($candidate.Width -eq 400 -and $candidate.Height -eq 250) {
                $lp = $candidate.GetPixel(20, 20)
                $rp = $candidate.GetPixel(380, 20)
                if ($lp.R -gt 200 -and $lp.G -lt 100 -and $rp.B -gt 190) {
                    $clip = $candidate
                    break
                }
            }
            $candidate.Dispose()
        }
        Start-Sleep -Milliseconds 100
    }
    Assert ($null -ne $clip) 'clipboard received the captured 400x250 image'
    $leftPixel = $clip.GetPixel(20, 20)
    $rightPixel = $clip.GetPixel(380, 20)
    Assert ($leftPixel.R -gt 200 -and $leftPixel.G -lt 100) ('captured left band is red (actual ' + $leftPixel + ')')
    Assert ($rightPixel.B -gt 190) ('captured right band is blue (actual ' + $rightPixel + ')')
    $clip.Save((Join-Path $PWD 'e2e_clipboard.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $clip.Dispose()

    $rect = GetRect $pin
    Assert ($rect.Left -eq 300 -and $rect.Top -eq 250) ('pinned at 300,250 (actual ' + $rect.Left + ',' + $rect.Top + ')')
    Assert (($rect.Right - $rect.Left) -eq 400 -and ($rect.Bottom - $rect.Top) -eq 250) ('pinned size is 400x250 (actual ' + ($rect.Right - $rect.Left) + 'x' + ($rect.Bottom - $rect.Top) + ')')

    $style = [W]::GetWindowLong($pin, -16)
    $exStyle = [W]::GetWindowLong($pin, -20)
    Assert (($style -band 0x00C00000) -eq 0) 'no title bar / caption'
    Assert (($style -band 0x00040000) -eq 0) 'no resizable frame'
    Assert (($style -band 0x00800000) -eq 0) 'no border'
    Assert (($exStyle -band 0x00000080) -ne 0) 'tool window style is set (not in Alt+Tab)'

    Write-Host "STEP 5: drag the pinned picture with the left mouse button"
    PostMouse $pin 0x0201 10 10 $true
    PostMouse $pin 0x0200 810 360 $true
    PostMouse $pin 0x0202 810 360 $false
    Pump 250

    $moved = GetRect $pin
    Assert ($moved.Left -eq 1100 -and $moved.Top -eq 600) ('dragged to 1100,600 (actual ' + $moved.Left + ',' + $moved.Top + ')')
    Assert (($moved.Right - $moved.Left) -eq 400 -and ($moved.Bottom - $moved.Top) -eq 250) 'dragging does not change size'

    Write-Host "STEP 6: screenshot the floating picture for visual proof"
    $shot = New-Object System.Drawing.Bitmap(520, 360)
    $graphics = [System.Drawing.Graphics]::FromImage($shot)
    $graphics.CopyFromScreen(1040, 540, 0, 0, (New-Object System.Drawing.Size(520, 360)))
    $graphics.Dispose()
    $png = Join-Path $PWD 'e2e_pin.png'
    $shot.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $shot.Dispose()
    Assert (Test-Path $png) 'saved e2e_pin.png'

    Write-Host "STEP 7: close the pinned picture"
    [void][W]::PostMessage($pin, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    Pump 400
    $pin = [IntPtr]::Zero
    Assert ([W]::FindByTitle('SnapMove_Pin') -eq [IntPtr]::Zero) 'pinned window closed'

    Write-Host "STEP 8: second instance must not start a second tray, just trigger a capture"
    $second = Start-Process -FilePath (Join-Path $PWD 'SnapMove.exe') -PassThru
    $second.WaitForExit(5000) | Out-Null
    Assert $second.HasExited 'second instance exited immediately'
    $overlay = WaitWindow 'SnapMove_Overlay' 8000
    Assert ($overlay -ne [IntPtr]::Zero) 'running instance showed the overlay when a second copy started'
    Pump 150
    PostMouse $overlay 0x0204 100 100 $false
    Pump 500
    $overlay = [IntPtr]::Zero
    Assert ([W]::FindByTitle('SnapMove_Overlay') -eq [IntPtr]::Zero) 'overlay closed after right click'
    Assert ([W]::FindByTitle('SnapMove_Pin') -eq [IntPtr]::Zero) 'cancel created no pinned window'

    $passed = $true
}
finally {
    if ($pin -ne [IntPtr]::Zero) { [void][W]::PostMessage($pin, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
    if ($overlay -ne [IntPtr]::Zero) { [void][W]::PostMessage($overlay, 0x0204, [IntPtr]::Zero, [IntPtr]::Zero) }
    if ($form -ne $null) { $form.Dispose() }
    Get-Process SnapMove -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 200
}

if ($passed) { Write-Host 'E2E RESULT: PASS' } else { Write-Host 'E2E RESULT: FAIL'; exit 1 }
