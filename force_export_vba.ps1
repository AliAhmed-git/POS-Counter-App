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
    
    Write-Host "Opening VBE..."
    $access.DoCmd.RunCommand(566) # acCmdVisualBasicEditor
    Start-Sleep -Seconds 5
    
    # Try to trigger the password prompt by trying to access a property that requires unlocking
    Write-Host "Attempting to unlock..."
    
    # Method: Alt+T, P (Project Properties) usually forces the password prompt
    $wshell.AppActivate("Microsoft Visual Basic for Applications")
    Start-Sleep -Seconds 1
    $wshell.SendKeys('%tp') # Tools -> Project Properties
    Start-Sleep -Seconds 2
    
    # Type password and enter
    $wshell.SendKeys($password)
    Start-Sleep -Seconds 1
    $wshell.SendKeys('{ENTER}')
    Start-Sleep -Seconds 3

    # Now that it's (hopefully) unlocked, export everything
    $vbe = $access.VBE
    $project = $vbe.ActiveVBProject
    
    Write-Host "Project Name: $($project.Name)"
    
    foreach ($component in $project.VBComponents) {
        $name = $component.Name
        $type = $component.Type
        $ext = ".vb"
        if ($type -eq 1) { $ext = ".bas" } # Module
        elseif ($type -eq 2) { $ext = ".cls" } # Class
        elseif ($type -eq 3) { $ext = ".frm" } # Form
        
        Write-Host "Exporting: $name ($ext)"
        try {
            # Use Export method instead of manual line reading if possible
            $targetPath = Join-Path $exportDir "$name$ext"
            $component.Export($targetPath)
            Write-Host "  Successfully exported to $targetPath"
        } catch {
            Write-Host "  Export failed, trying manual read..."
            if ($component.CodeModule.CountOfLines -gt 0) {
                $code = $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
                $code | Out-File -FilePath (Join-Path $exportDir "$name.txt")
                Write-Host "  Manually saved to $name.txt"
            }
        }
    }
    Write-Host "Extraction process finished."

} catch {
    Write-Host "Global Error: $($_.Exception.Message)"
} finally {
    # Keep it open for a bit to ensure all operations finish
    Start-Sleep -Seconds 5
    $access.Quit()
}
