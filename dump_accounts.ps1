# dump_accounts.ps1
$accessPath = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$connStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$accessPath;"

function Get-Data($query) {
    $conn = New-Object System.Data.OleDb.OleDbConnection($connStr)
    $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($query, $conn)
    $dt = New-Object System.Data.DataTable
    $conn.Open()
    $adapter.Fill($dt) | Out-Null
    $conn.Close()
    return $dt
}

Write-Host "--- Top 5 rows from [Accounts] ---"
try {
    $data = Get-Data "SELECT TOP 5 * FROM [Accounts]"
    $data | Format-Table -AutoSize
} catch { Write-Host "Error reading Accounts: $($_.Exception.Message)" }

Write-Host "`n--- Top 5 rows from [Login] ---"
try {
    $data = Get-Data "SELECT TOP 5 * FROM [Login]"
    $data | Format-Table -AutoSize
} catch { Write-Host "Error reading Login: $($_.Exception.Message)" }
