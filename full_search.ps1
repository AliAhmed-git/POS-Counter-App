$connString = 'Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb;'
$conn = New-Object System.Data.OleDb.OleDbConnection($connString)
$target = "0125487"
try {
    $conn.Open()
    $tables = $conn.GetSchema('Tables')
    foreach ($row in $tables.Rows) {
        if ($row.TABLE_TYPE -eq 'TABLE') {
            $t = $row.TABLE_NAME
            Write-Output "Searching Table: $t"
            try {
                $cmd = New-Object System.Data.OleDb.OleDbCommand("SELECT * FROM [$t]", $conn)
                $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
                $dt = New-Object System.Data.DataTable
                $adapter.Fill($dt)
                foreach ($r in $dt.Rows) {
                    foreach ($col in $dt.Columns) {
                        if ($r[$col].ToString() -like "*$target*") {
                            Write-Output "--- MATCH FOUND ---"
                            Write-Output "Table: $t"
                            Write-Output "Column: $($col.ColumnName)"
                            Write-Output "Row Data: $($r | Out-String)"
                            Write-Output "-------------------"
                        }
                    }
                }
            } catch {
                Write-Output "  Error searching $t: $($_.Exception.Message)"
            }
        }
    }
} finally {
    $conn.Close()
}
