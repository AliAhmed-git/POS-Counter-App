$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $tableNames = @()
    foreach ($table in $access.CurrentData.AllTables) {
        if ($table.Name -notlike 'MSys*') {
            $tableNames += $table.Name
        }
    }
    $tableNames | Out-File -FilePath 'C:\xampp\htdocs\pos-counter\tables.txt'
    Write-Output "Success"
} catch {
    $_.Exception.Message | Out-File -FilePath 'C:\xampp\htdocs\pos-counter\error.txt'
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
