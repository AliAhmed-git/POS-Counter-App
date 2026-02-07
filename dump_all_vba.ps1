$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $vbe = $access.VBE
    $allCode = ""
    foreach ($project in $vbe.VBProjects) {
        foreach ($component in $project.VBComponents) {
            $allCode += "`r`n`r`n' ========================================`r`n"
            $allCode += "' COMPONENT: $($component.Name)`r`n"
            $allCode += "' ========================================`r`n"
            if ($component.CodeModule.CountOfLines -gt 0) {
                $allCode += $component.CodeModule.Lines(1, $component.CodeModule.CountOfLines)
            }
        }
    }
    $allCode | Out-File -FilePath 'C:\xampp\htdocs\pos-counter\backend_code_dump.txt'
    Write-Output "Done"
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
