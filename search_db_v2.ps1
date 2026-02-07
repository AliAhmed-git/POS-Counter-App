$pth = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$cs = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$pth;"
$cn = New-Object System.Data.OleDb.OleDbConnection($cs)
$tgt = "0125487"

try {
    $cn.Open()
    $tbls = $cn.GetSchema('Tables')
    foreach ($r in $tbls.Rows) {
        if ($r.TABLE_TYPE -eq 'TABLE') {
            $tn = $r.TABLE_NAME
            Write-Host "Searching inside table: $tn"
            try {
                $da = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT * FROM [$tn]", $cn)
                $dt = New-Object System.Data.DataTable
                $da.Fill($dt) | Out-Null
                foreach ($row in $dt.Rows) {
                    foreach ($c in $dt.Columns) {
                        if ($row[$c].ToString() -like "*$tgt*") {
                            Write-Host "--- MATCH ---"
                            Write-Host "Table: $tn"
                            Write-Host "Column: $($c.ColumnName)"
                            Write-Host "Value: $($row[$c])"
                        }
                    }
                }
            } catch {
                Write-Host "Err on $tn: $($_.Exception.Message)"
            }
        }
    }
} finally {
    $cn.Close()
}
