# Test API with Counter 5
$url = "https://asif.theswiftdevelopers.com/stagging/api/product/updated-records/download"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJPbmxpbmUgSldUIEJ1aWxkZXIiLCJpYXQiOjE3NjAwMTY5ODYsImV4cCI6MTc5MTU1Mjk4NiwiYXVkIjoid3d3LmV4YW1wbGUuY29tIiwic3ViIjoianJvY2tldEBleGFtcGxlLmNvbSIsIkdpdmVuTmFtZSI6IkpvaG5ueSIsIlN1cm5hbWUiOiJSb2NrZXQiLCJFbWFpbCI6Impyb2NrZXRAZXhhbXBsZS5jb20iLCJSb2xlIjpbIk1hbmFnZXIiLCJQcm9qZWN0IEFkbWluaXN0cmF0b3IiXX0.W_VOkuJ8DRCe_Ht_vmcmsr3G9S4V7FtekeREqlYIb64"

Write-Host "Testing Counter 5..." -ForegroundColor Cyan
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"

$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"bussiness_id`"$LF",
    "40",
    "--$boundary",
    "Content-Disposition: form-data; name=`"counter_no`"$LF",
    "5",
    "--$boundary--$LF"
) -join $LF

$response = Invoke-RestMethod -Uri $url -Method Post -Headers @{"Authorization" = "Bearer $token"} -ContentType "multipart/form-data; boundary=$boundary" -Body $bodyLines
Write-Host "Counter 5 Result:" -ForegroundColor Yellow
$response | ConvertTo-Json -Depth 3
