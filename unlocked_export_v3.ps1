$dbPath = 'C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb'
$password = 'nadra@123'
$exportDir = 'C:\xampp\htdocs\pos-counter\extracted_scripts'

if (!(Test-Path $exportDir)) { New-Item -ItemType Directory -Path $exportDir }

$access = New-Object -ComObject Access.Application
$access.Visible = $true

try {
    Write-Host "Opening database..."
    $access.OpenCurrentDatabase($dbPath)
    $wshell = New-Object -ComObject WScript.Shell
    
    Start-Sleep -Seconds 5
    
    # Try to open VBE directly
    Write-Host "Triggering VBE..."
    $wshell.SendKeys('%{F11}')
    Start-Sleep -Seconds 3

    # Switch to VBE
    $wshell.AppActivate("Microsoft Visual Basic for Applications")
    Start-Sleep -Seconds 1

    # Try to expand the tree (usually triggers password)
    $wshell.SendKeys('{HOME}{RIGHT}{ENTER}')
    Start-Sleep -Seconds 2

    # Type password
    Write-Host "Sending password..."
    $wshell.SendKeys($password)
    $wshell.SendKeys('{ENTER}')
    Start-Sleep -Seconds 5

    # Check if we can access the modules now
    $modules = @("Swift", "FBR", "PrintFile", "Accounts")
    foreach ($m in $modules) {
        Write-Host "Attempting export for: $m"
        try {
            # Try to open the module as text
            # This sometimes works if the project is unlocked in the session
            $target = Join-Path $exportDir "$m.txt"
            $access.DoCmd.OutputTo(5, $m, "text/plain", $target) # acOutputModule
            Write-Host "  Exported $m to $target"
        } catch {
            Write-Host "  Failed to export $m using OutputTo: $($_.Exception.Message)"
            
            # Fallback to manual line read via Modules collection
            try {
                $access.DoCmd.OpenModule($m)
                $mod = $access.Modules.Item($m)
                $lines = $mod.Lines(1, $mod.CountOfLines)
                $lines | Out-File -FilePath $target
                Write-Host "  Manually exported $m"
            } catch {
                 Write-Host "  Manual read failed for $m: $($_.Exception.Message)"
            }
        }
    }

} catch {
    Write-Host "Global Error: $($_.Exception.Message)"
} finally {
    $access.Quit()
}
