$access = New-Object -ComObject Access.Application
try {
    $access.OpenCurrentDatabase('C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb')
    $modules = @("Swift", "FBR", "PrintFile", "Accounts")
    foreach ($m in $modules) {
        Write-Output "--- MODULE: $m ---"
        try {
            # Open the module in the editor
            $access.DoCmd.OpenModule($m)
            $mod = $access.Modules.Item($m)
            if ($mod.CountOfLines -gt 0) {
                $mod.Lines(1, $mod.CountOfLines)
            }
        } catch {
            Write-Output "Error reading $m: $($_.Exception.Message)"
        }
        Write-Output "--- END ---"
    }
} catch {
    Write-Output "Global Error: $($_.Exception.Message)"
} finally {
    try { $access.Quit() } catch {}
}
