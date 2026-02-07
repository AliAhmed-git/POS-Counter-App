$p = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$c = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$p;"
$cn = New-Object System.Data.OleDb.OleDbConnection($c)
try {
    $cn.Open()
    $ts = @("SalesDetail", "Log", "Items", "Login", "CounterInfo", "SyncCounter")
    foreach ($t in $ts) {
        Write-Output "--- TABLE: $t ---"
        try {
            $da = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT TOP 50 * FROM [$t]", $cn)
            $dt = New-Object System.Data.DataTable
            $da.Fill($dt) | Out-Null
            $dt | Format-Table -AutoSize | Out-String | Write-Output
        } catch { Write-Output "Err on $t: $($_.Exception.Message)" }
        Write-Output "--- END $t ---"
    }
} finally {
    $cn.Close()
}
