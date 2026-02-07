$dbPath = 'C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb'
try {
    # Try to create DAO DBEngine
    $dbe = New-Object -ComObject DAO.DBEngine.120
    Write-Host "DAO DBEngine created."
    
    $db = $dbe.OpenDatabase($dbPath)
    Write-Host "Database opened."
    
    $output = @()
    foreach ($td in $db.TableDefs) {
        if ($td.Attributes -eq 0 -or ($td.Attributes -band 0x40000000)) { # Local or Linked
            # Filter out system tables
            if ($td.Name -notlike 'MSys*') {
                $tableName = $td.Name
                Write-Host "Processing table: $tableName"
                foreach ($field in $td.Fields) {
                    $output += [PSCustomObject]@{
                        Table  = $tableName
                        Column = $field.Name
                        Type   = $field.Type
                    }
                }
            }
        }
    }

    if ($output.Count -gt 0) {
        $output | Sort-Object Table | Export-Csv -Path 'C:\xampp\htdocs\pos-counter\schema_details.csv' -NoTypeInformation
        Write-Host "Schema details exported: $($output.Count) fields found."
    } else {
        Write-Host "No schema details found."
    }
    
    $db.Close()
} catch {
    Write-Error "Failed to extract schema using DAO: $($_.Exception.Message)"
}
