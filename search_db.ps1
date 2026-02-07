$dbPath = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$connString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$dbPath;"
$conn = New-Object System.Data.OleDb.OleDbConnection($connString)
$target = "0125487"

try {
    $conn.Open()
    $tables = $conn.GetSchema('Tables')
    foreach ($row in $tables.Rows) {
        if ($row.TABLE_TYPE -eq 'TABLE') {
            $t = $row.TABLE_NAME
            Write-Host "Searching inside table: $t"
            try {
                $da = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT * FROM [$t]", $conn)
                $dt = New-Object System.Data.DataTable
                $da.Fill($dt) | Out-Null
                foreach ($r in $dt.Rows) {
                    foreach ($col in $dt.Columns) {
                        if ($r[$col].ToString() -like "*$target*") {
                            Write-Host "--- MATCH ---"
                            Write-Host "Table: $t"
                            Write-Host "Column: $($col.ColumnName)"
                            Write-Host "Value: $($r[$col])"
                        }
                    }
                }
            } catch {
                Write-Host "Err on $t: $($_.Exception.Message)"
            }
        }
    }
} finally {
    $conn.Close()
}
