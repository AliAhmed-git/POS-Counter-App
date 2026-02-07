# Verify SQLite Schema for POS App
$dbPath = "pos.db"
if (-not (Test-Path $dbPath)) {
    Write-Host "Error: pos.db not found!" -ForegroundColor Red
    exit 1
}

$tables = @("SalesHeads", "SalesDetails", "Items", "Counters")
$results = @()

foreach ($table in $tables) {
    Write-Host "Verifying table: $table" -ForegroundColor Cyan
    try {
        $columns = sqlite3 $dbPath "PRAGMA table_info($table);"
        $results += "--- $table ---"
        $results += $columns
    } catch {
        $results += "--- $table ---"
        $results += "Error: Could not query table info."
    }
}

$results | Out-File "db_schema_verification.txt"
Write-Host "Verification results saved to db_schema_verification.txt" -ForegroundColor Green
