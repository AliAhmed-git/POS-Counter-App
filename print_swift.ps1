$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        foreach ($component in $project.VBComponents) {
            if ($component.Name -eq "Swift") {
                Write-Output "--- MODULE: Swift ---"
                if ($component.CodeModule.CountOfLines -gt 0) {
                   $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
                }
                Write-Output "--- END ---"
            }
        }
    }
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
