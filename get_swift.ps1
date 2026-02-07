$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        foreach ($component in $project.VBComponents) {
            if ($component.Name -eq "Swift") {
                $code = $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
                $code | Out-File -FilePath 'C:\xampp\htdocs\pos-counter\Swift_Module.txt'
                Write-Output "Swift module exported"
            }
        }
    }
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
