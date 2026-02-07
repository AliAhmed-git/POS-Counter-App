$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        foreach ($component in $project.VBComponents) {
            $name = $component.Name
            Write-Output "--- COMPONENT: $name ---"
            if ($component.CodeModule.CountOfLines -gt 0) {
                $code = $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
                $code
            }
            Write-Output "--- END $name ---"
        }
    }
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
