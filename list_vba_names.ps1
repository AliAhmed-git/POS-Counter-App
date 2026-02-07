$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $vbe = $access.VBE
    foreach ($project in $vbe.VBProjects) {
        Write-Output "Project: $($project.Name)"
        foreach ($component in $project.VBComponents) {
            Write-Output "  Component: $($component.Name) (Type: $($component.Type))"
        }
    }
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
