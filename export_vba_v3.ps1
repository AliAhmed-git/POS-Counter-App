$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $exportDir = "C:\xampp\htdocs\pos-counter\vba_export"
    if (!(Test-Path $exportDir)) { New-Item -ItemType Directory -Path $exportDir }

    # Enable trust for VBE if possible or just try to access it
    # Note: This might require manual setup if it's strictly disabled, but let's try.
    
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        foreach ($component in $project.VBComponents) {
            $name = $component.Name
            Write-Output "Exporting Component: $name"
            $code = $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
            $code | Out-File -FilePath "$exportDir\$name.vb"
            Write-Output "  Success"
        }
    }
    Write-Output "VBE Export Success"
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
