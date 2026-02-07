$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $exportDir = "C:\xampp\htdocs\pos-counter\vba_export"
    if (!(Test-Path $exportDir)) { New-Item -ItemType Directory -Path $exportDir }

    # Export Modules
    foreach ($module in $access.CurrentProject.AllModules) {
        $name = $module.Name
        Write-Output "Exporting Module: $name"
        $access.SaveAsText([Microsoft.Office.Interop.Access.AcObjectType]::acModule, $name, "$exportDir\Module_$name.txt")
    }

    # Export Forms
    foreach ($form in $access.CurrentProject.AllForms) {
        $name = $form.Name
        Write-Output "Exporting Form: $name"
        $access.SaveAsText([Microsoft.Office.Interop.Access.AcObjectType]::acForm, $name, "$exportDir\Form_$name.txt")
    }

    # Export Reports
    foreach ($report in $access.CurrentProject.AllReports) {
        $name = $report.Name
        Write-Output "Exporting Report: $name"
        $access.SaveAsText([Microsoft.Office.Interop.Access.AcObjectType]::acReport, $name, "$exportDir\Report_$name.txt")
    }

    # Export Macros
    foreach ($macro in $access.CurrentProject.AllMacros) {
        $name = $macro.Name
        Write-Output "Exporting Macro: $name"
        $access.SaveAsText([Microsoft.Office.Interop.Access.AcObjectType]::acMacro, $name, "$exportDir\Macro_$name.txt")
    }

    Write-Output "VBA Export Success"
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
