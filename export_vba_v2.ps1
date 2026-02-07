$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $exportDir = "C:\xampp\htdocs\pos-counter\vba_export"
    if (!(Test-Path $exportDir)) { New-Item -ItemType Directory -Path $exportDir }

    $modules = @("Swift", "FBR", "PrintFile", "Accounts")
    foreach ($m in $modules) {
        try {
            Write-Output "Exporting Module: $m"
            $access.SaveAsText(5, $m, "$exportDir\Module_$m.txt") 
            Write-Output "  Success"
        } catch {
            Write-Output "  Failed: $($_.Exception.Message)"
        }
    }

    $forms = @("Login", "SalesHead", "Items", "ItemRegistratation")
    foreach ($f in $forms) {
        try {
            Write-Output "Exporting Form: $f"
            $access.SaveAsText(2, $f, "$exportDir\Form_$f.txt") 
            Write-Output "  Success"
        } catch {
            Write-Output "  Failed: $($_.Exception.Message)"
        }
    }
} catch {
    Write-Output "Global Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
