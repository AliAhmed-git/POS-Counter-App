$dbPath = 'C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb'
$password = 'nadra@123'
$exportDir = 'C:\xampp\htdocs\pos-counter\unlocked_vba'

if (!(Test-Path $exportDir)) { New-Item -ItemType Directory -Path $exportDir }

$access = New-Object -ComObject Access.Application
$access.Visible = $true

try {
    $access.OpenCurrentDatabase($dbPath)
    
    # Try to use SendKeys to enter the password in the VBE
    $wshell = New-Object -ComObject WScript.Shell
    
    # Open VBE
    $access.DoCmd.RunCommand(566) # acCmdVisualBasicEditor
    Start-Sleep -Seconds 2
    
    # Attempt to unlock (this is tricky with SendKeys but let's try a simple sequence)
    # Usually: Alt+T, P (Tools -> Project Properties) triggers password if locked
    $wshell.SendKeys('%tp')
    Start-Sleep -Seconds 1
    $wshell.SendKeys($password)
    $wshell.SendKeys('{ENTER}')
    Start-Sleep -Seconds 1

    # Now try to export using the VBE object model
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        foreach ($component in $project.VBComponents) {
            $name = $component.Name
            Write-Host "Exporting: $name"
            if ($component.CodeModule.CountOfLines -gt 0) {
                $code = $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
                $code | Out-File -FilePath "$exportDir\$name.vb"
            }
        }
    }
    Write-Host "Export completed to $exportDir"

} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    # $access.Quit() # Keep open briefly to see if it worked or manual intervention needed if it fails
}
