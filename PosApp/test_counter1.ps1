# Test API Download Script
$url = "https://asif.theswiftdevelopers.com/stagging/api/product/updated-records/download"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJPbmxpbmUgSldUIEJ1aWxkZXIiLCJpYXQiOjE3NjAwMTY5ODYsImV4cCI6MTc5MTU1Mjk4NiwiYXVkIjoid3d3LmV4YW1wbGUuY29tIiwic3ViIjoianJvY2tldEBleGFtcGxlLmNvbSIsIkdpdmVuTmFtZSI6IkpvaG5ueSIsIlN1cm5hbWUiOiJSb2NrZXQiLCJFbWFpbCI6Impyb2NrZXRAZXhhbXBsZS5jb20iLCJSb2xlIjpbIk1hbmFnZXIiLCJQcm9qZWN0IEFkbWluaXN0cmF0b3IiXX0.W_VOkuJ8DRCe_Ht_vmcmsr3G9S4V7FtekeREqlYIb64"

Write-Host "Testing API Download with Counter 1..." -ForegroundColor Cyan
Write-Host "URL: $url" -ForegroundColor Gray

# Create multipart form data
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"

$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"bussiness_id`"$LF",
    "40",
    "--$boundary",
    "Content-Disposition: form-data; name=`"counter_no`"$LF",
    "1",
    "--$boundary--$LF"
) -join $LF

try {
    Write-Host "`nSending request... (This may take a few minutes for full dataset)" -ForegroundColor Yellow
    $startTime = Get-Date
    
    # Make the request with extended timeout
    $response = Invoke-RestMethod -Uri $url -Method Post `
        -Headers @{
            "Authorization" = "Bearer $token"
        } `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Body $bodyLines `
        -TimeoutSec 600

    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "`nAPI Response received successfully in $duration seconds!" -ForegroundColor Green
    
    # Save to JSON file
    $outputPath = "api_response_counter1.json"
    $response | ConvertTo-Json -Depth 10 | Out-File -FilePath $outputPath -Encoding UTF8
    
    Write-Host "Response saved to: $outputPath" -ForegroundColor Green
    Write-Host "`nResponse Summary:\" -ForegroundColor Yellow
    
    if ($response.status) {
        Write-Host "Status: $($response.status)" -ForegroundColor Green
        Write-Host "Message: $($response.message)" -ForegroundColor Cyan
        Write-Host "Total items: $($response.data.Count)" -ForegroundColor Cyan
        
        if ($response.data.Count -gt 0) {
            Write-Host "`nFirst item sample:" -ForegroundColor Yellow
            $response.data[0] | ConvertTo-Json -Depth 5
        }
    } else {
        $response | ConvertTo-Json -Depth 5
    }
    
} catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    }
}
