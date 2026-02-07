$dbPath = 'C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb'
$password = 'nadra@123'
$exportDir = 'C:\xampp\htdocs\pos-counter\unlocked_vba'

if (!(Test-Path $exportDir)) { New-Item -ItemType Directory -Path $exportDir }

$access = New-Object -ComObject Access.Application
$access.Visible = $true

try {
    $access.OpenCurrentDatabase($dbPath)
    $wshell = New-Object -ComObject WScript.Shell
    
    # Open VBE
    $access.DoCmd.RunCommand(566) 
    Start-Sleep -Seconds 3
    
    # Bring VBE to front (might need focus)
    # Sending {F2} or something to ensure focus usually helps
    $wshell.SendKeys('{F2}') 
    Start-Sleep -Seconds 1
    
    # Try to expand the project tree and trigger password
    # {HOME} then {RIGHT} {RIGHT} often lands on the project
    $wshell.SendKeys('{HOME}{RIGHT}{RIGHT}{ENTER}')
    Start-Sleep -Seconds 2
    
    # Send password
    $wshell.SendKeys($password)
    $wshell.SendKeys('{ENTER}')
    Start-Sleep -Seconds 3

    # Now attempt export
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        Write-Host "Project: $($project.Name) - Locked state: $($project.Mode)"
        foreach ($component in $project.VBComponents) {
            $name = $component.Name
            Write-Host "  Exporting: $name"
            try {
                if ($component.CodeModule.CountOfLines -gt 0) {
                    $code = $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
                    $code | Out-File -FilePath "$exportDir\$name.vb"
                    Write-Host "    Done"
                }
            } catch {
                Write-Host "    Failed: $($_.Exception.Message)"
            }
        }
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    # $access.Quit()
}
