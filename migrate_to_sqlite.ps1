# migrate_to_sqlite.ps1 - Migrate POS data from Access to SQLite

$accessPath = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$sqlitePath = "C:\xampp\htdocs\pos-counter\PosApp\pos.db"
$binPath = "C:\xampp\htdocs\pos-counter\PosApp\bin\Debug\net6.0-windows"

# Remove existing SQLite db if it exists
if (Test-Path $sqlitePath) { Remove-Item $sqlitePath }

Write-Host "Loading dependencies from $binPath..."
# Load Assemblies in order
try {
    [Reflection.Assembly]::LoadFrom((Join-Path $binPath "SQLitePCLRaw.core.dll")) | Out-Null
    [Reflection.Assembly]::LoadFrom((Join-Path $binPath "SQLitePCLRaw.batteries_v2.dll")) | Out-Null
    [Reflection.Assembly]::LoadFrom((Join-Path $binPath "Microsoft.Data.Sqlite.dll")) | Out-Null
    
    # Initialize SQLitePCL
    $batteries = [SQLitePCL.Batteries_V2]::Init()
} catch {
    Write-Host "Error loading DLLs: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$accessConnStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$accessPath;"
$sqliteConnStr = "Data Source=$sqlitePath;"

function Get-AccessData($query) {
    $conn = New-Object System.Data.OleDb.OleDbConnection($accessConnStr)
    $cmd = New-Object System.Data.OleDb.OleDbCommand($query, $conn)
    $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $conn.Open()
    [void]$adapter.Fill($dt)
    $conn.Close()
    return $dt
}

Write-Host "Connecting to SQLite..."
$sqliteConn = New-Object Microsoft.Data.Sqlite.SqliteConnection($sqliteConnStr)
$sqliteConn.Open()

$createTables = @"
CREATE TABLE Logins (User TEXT PRIMARY KEY, Password TEXT, Roll TEXT);
CREATE TABLE Counters (CounterNo INTEGER PRIMARY KEY, CounterName TEXT);
CREATE TABLE Items (ItemCode INTEGER PRIMARY KEY, ItemName TEXT, Company TEXT, SPrice REAL, Packing TEXT, STax REAL, Stock REAL);
CREATE TABLE Packings (ItemCode INTEGER, PackingType TEXT, BarCode TEXT, SPrice REAL, Qty REAL, PRIMARY KEY (ItemCode, PackingType));
CREATE TABLE SalesHeads (InvoiceNo INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT, CustomerName TEXT, TotalAmount REAL, TotalTax REAL);
CREATE TABLE SalesDetails (InvoiceNo INTEGER, ItemCode INTEGER, ItemName TEXT, Company TEXT, SPrice REAL, Qty REAL, NetAmount REAL, TaxAmount REAL, PRIMARY KEY (InvoiceNo, ItemCode));
"@

$cmd = $sqliteConn.CreateCommand()
$cmd.CommandText = $createTables
$cmd.ExecuteNonQuery()

$tables = @(
    @{ Name = "Logins"; Query = "SELECT [User], [Password], 'User' as Roll FROM [Login]" },
    @{ Name = "Counters"; Query = "SELECT [CounterNo], [CounterName] FROM [CounterInfo]" },
    @{ Name = "Items"; Query = "SELECT [ItemCode], [ItemName], [Company], [SPrice], [Packing], [STax], [Stock] FROM [Items]" },
    @{ Name = "Packings"; Query = "SELECT [ItemCode], [Packing] as PackingType, [BarCode], [SPrice], [Qty] FROM [Packing]" }
)

foreach ($tbl in $tables) {
    Write-Host "Migrating $($tbl.Name)..."
    $data = Get-AccessData $tbl.Query
    
    $transaction = $sqliteConn.BeginTransaction()
    foreach ($row in $data.Rows) {
        $insertCmd = $sqliteConn.CreateCommand()
        $insertCmd.Transaction = $transaction
        $cols = @()
        $vals = @()
        
        foreach ($col in $data.Columns) {
            $cols += "[$($col.ColumnName)]"
            $vals += "@$($col.ColumnName)"
            $p = $insertCmd.CreateParameter()
            $p.ParameterName = "@$($col.ColumnName)"
            $p.Value = $row[$col.ColumnName]
            $insertCmd.Parameters.Add($p)
        }
        
        $insertCmd.CommandText = "INSERT INTO [$($tbl.Name)] ($( $cols -join "," )) VALUES ($( $vals -join "," ))"
        $insertCmd.ExecuteNonQuery()
    }
    $transaction.Commit()
    Write-Host "Migrated $($data.Rows.Count) rows."
}

$sqliteConn.Close()
Write-Host "Migration to SQLite complete!" -ForegroundColor Green
