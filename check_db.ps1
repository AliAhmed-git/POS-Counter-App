# check_db.ps1
$sqlConnStr = "Server=(localdb)\mssqllocaldb;Database=PosDb;Trusted_Connection=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($sqlConnStr)
try {
    $conn.Open()
    $tables = @("Logins", "Items", "Counters", "Packings")
    foreach ($t in $tables) {
        $cmd = New-Object System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM [$t]", $conn)
        $count = $cmd.ExecuteScalar()
        Write-Host "Table $t has $count rows."
        if ($t -eq "Logins") {
            $cmd2 = New-Object System.Data.SqlClient.SqlCommand("SELECT * FROM [$t]", $conn)
            $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd2)
            $dt = New-Object System.Data.DataTable
            $adapter.Fill($dt)
            $dt | Format-Table | Out-String | Write-Host
        }
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    $conn.Close()
}
