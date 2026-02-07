# check_db_counts.ps1
$sqlitePath = "C:\xampp\htdocs\pos-counter\PosApp\pos.db"
$connStr = "Data Source=$sqlitePath;"
$binPath = "C:\xampp\htdocs\pos-counter\PosApp\bin\Debug\net6.0-windows"

try {
    [Reflection.Assembly]::LoadFrom((Join-Path $binPath "SQLitePCLRaw.core.dll")) | Out-Null
    [Reflection.Assembly]::LoadFrom((Join-Path $binPath "SQLitePCLRaw.batteries_v2.dll")) | Out-Null
    [Reflection.Assembly]::LoadFrom((Join-Path $binPath "Microsoft.Data.Sqlite.dll")) | Out-Null
    [SQLitePCL.Batteries_V2]::Init()
} catch { }

$conn = New-Object Microsoft.Data.Sqlite.SqliteConnection($connStr)
$conn.Open()

$tables = @("Accounts", "Logins", "Counters", "Items", "Packings")
foreach ($tbl in $tables) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM $tbl"
    try {
        $count = $cmd.ExecuteScalar()
        Write-Host "$tbl count: $count"
    } catch {
        Write-Host "$tbl: Error - $($_.Exception.Message)"
    }
}
$conn.Close()
