$p = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$c = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$p;"
$cn = New-Object System.Data.OleDb.OleDbConnection($c)
try {
    $cn.Open()
    $da = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT ItemCode, Packing, BarCode, SPrice, Qty FROM [Packings]", $cn)
    $dt = New-Object System.Data.DataTable
    $da.Fill($dt) | Out-Null
    $dt | Export-Csv -Path "c:\xampp\htdocs\pos-counter\barcodes.csv" -NoTypeInformation
    Write-Output "Successfully exported barcodes to c:\xampp\htdocs\pos-counter\barcodes.csv"
} catch { 
    Write-Output "Error: $($_.Exception.Message)" 
} finally {
    $cn.Close()
}
