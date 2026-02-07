# Test API with BusinessID 41 and Counter 11 (from Postman collection)
$url = "https://asif.theswiftdevelopers.com/stagging/api/product/updated-records/download"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJPbmxpbmUgSldUIEJ1aWxkZXIiLCJpYXQiOjE3NjAwMTY5ODYsImV4cCI6MTc5MTU1Mjk4NiwiYXVkIjoid3d3LmV4YW1wbGUuY29tIiwic3ViIjoianJvY2tldEBleGFtcGxlLmNvbSIsIkdpdmVuTmFtZSI6IkpvaG5ueSIsIlN1cm5hbWUiOiJSb2NrZXQiLCJFbWFpbCI6Impyb2NrZXRAZXhhbXBsZS5jb20iLCJSb2xlIjpbIk1hbmFnZXIiLCJQcm9qZWN0IEFkbWluaXN0cmF0b3IiXX0.W_VOkuJ8DRCe_Ht_vmcmsr3G9S4V7FtekeREqlYIb64"

Write-Host "Testing BusinessID 41 with Counter 11 (from Postman)..." -ForegroundColor Cyan
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"

$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"bussiness_id`"$LF",
    "41",
    "--$boundary",
    "Content-Disposition: form-data; name=`"counter_no`"$LF",
    "11",
    "--$boundary--$LF"
) -join $LF

$response = Invoke-RestMethod -Uri $url -Method Post -Headers @{"Authorization" = "Bearer $token"} -ContentType "multipart/form-data; boundary=$boundary" -Body $bodyLines -TimeoutSec 120
Write-Host "`nBusinessID 41 Result:" -ForegroundColor Yellow
$response | ConvertTo-Json -Depth 3
Write-Host "`nTotal items: $($response.data.Count)" -ForegroundColor Cyan
