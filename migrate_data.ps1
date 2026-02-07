# migrate_data.ps1 - Migrate POS data from Access to SQL Server LocalDB

$accessPath = "C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb"
$sqlServer = "(localdb)\mssqllocaldb"
$database = "PosDb"

$accessConnStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$accessPath;"
$sqlConnStr = "Server=$sqlServer;Database=$database;Trusted_Connection=True;"

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

function Write-SqlData($tableName, $dataTable) {
    $bulkCopy = New-Object System.Data.SqlClient.SqlBulkCopy($sqlConnStr)
    $bulkCopy.DestinationTableName = "[$tableName]"
    $bulkCopy.BatchSize = 1000
    try {
        $bulkCopy.WriteToServer($dataTable)
        Write-Host "Migrated $($dataTable.Rows.Count) rows to $tableName" -ForegroundColor Green
    } catch {
        Write-Host "Error migrating $tableName : $($_.Exception.Message)" -ForegroundColor Red
    } finally {
        $bulkCopy.Close()
    }
}

# Tables to migrate
$tables = @(
    @{ Name = "Logins"; Query = "SELECT [User], [Password], 'User' as Roll FROM [Login]" },
    @{ Name = "Counters"; Query = "SELECT [CounterNo], [CounterName] FROM [CounterInfo]" },
    @{ Name = "Items"; Query = "SELECT [ItemCode], [ItemName], [Company], [SPrice], [Packing], [STax], [Stock] FROM [Items]" },
    @{ Name = "Packings"; Query = "SELECT [ItemCode], [Packing] as PackingType, [BarCode], [SPrice], [Qty] FROM [Packing]" }
)

Write-Host "Starting migration from $accessPath to $database..." -ForegroundColor Cyan

foreach ($tbl in $tables) {
    Write-Host "Processing $($tbl.Name)..."
    try {
        $data = Get-AccessData $tbl.Query
        if ($data.Rows.Count -gt 0) {
            Write-SqlData $tbl.Name $data
        } else {
            Write-Host "No data found for $($tbl.Name)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Failed to process $($tbl.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Migration complete!" -ForegroundColor Cyan
