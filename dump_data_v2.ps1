$pth = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$cs = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$pth;"
$cn = New-Object System.Data.OleDb.OleDbConnection($cs)
try {
    $cn.Open()
    $tbls = @("SalesDetail", "Log", "Items", "Login", "CounterInfo", "SyncCounter")
    foreach ($t in $tbls) {
        Write-Host "--- TABLE: $t ---"
        try {
            $da = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT TOP 50 * FROM [$t]", $cn)
            $dt = New-Object System.Data.DataTable
            $da.Fill($dt) | Out-Null
            $dt | Format-Table -AutoSize | Out-String | Write-Host
        } catch { Write-Host "Error on $t: $($_.Exception.Message)" }
        Write-Host "--- END $t ---"
    }
} finally {
    $cn.Close()
}
