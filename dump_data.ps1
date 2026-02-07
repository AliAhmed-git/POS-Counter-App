$connString = 'Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb;'
$conn = New-Object System.Data.OleDb.OleDbConnection($connString)
try {
    $conn.Open()
    $tables = @("Login", "Items", "CounterInfo")
    foreach ($t in $tables) {
        Write-Output "--- TABLE: $t ---"
        $cmd = New-Object System.Data.OleDb.OleDbCommand("SELECT TOP 5 * FROM [$t]", $conn)
        $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
        $dt = New-Object System.Data.DataTable
        [void]$adapter.Fill($dt)
        $dt | Format-Table -AutoSize | Out-String | Write-Output
        Write-Output "--- END $t ---"
    }
} catch {
    Write-Output "Error: $($_.Exception.Message)"
} finally {
    $conn.Close()
}
