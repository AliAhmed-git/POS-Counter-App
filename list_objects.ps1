$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $outputFile = "C:\xampp\htdocs\pos-counter\db_objects.txt"
    $objList = @()

    $objList += "Forms:"
    foreach ($form in $access.CurrentProject.AllForms) { $objList += "  $($form.Name)" }
    $objList += "`nReports:"
    foreach ($report in $access.CurrentProject.AllReports) { $objList += "  $($report.Name)" }
    $objList += "`nModules:"
    foreach ($module in $access.CurrentProject.AllModules) { $objList += "  $($module.Name)" }
    $objList += "`nMacros:"
    foreach ($macro in $access.CurrentProject.AllMacros) { $objList += "  $($macro.Name)" }

    $objList | Out-File -FilePath $outputFile
    Write-Output "Objects listed in $outputFile"
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
