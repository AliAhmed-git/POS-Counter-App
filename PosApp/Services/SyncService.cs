using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PosApp.Desktop.Models;

namespace PosApp.Desktop.Services
{
    public interface ISyncService
    {
        Task<List<ProductSyncData>?> DownloadUpdatedItemsAsync(int businessId, int counterNo);
        Task<bool> ConfirmSyncAsync(int businessId, int counterNo);
    }

    public class SyncService : ISyncService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://asif.theswiftdevelopers.com/stagging/api/";
        private const string Token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJPbmxpbmUgSldUIEJ1aWxkZXIiLCJpYXQiOjE3NjAwMTY5ODYsImV4cCI6MTc5MTU1Mjk4NiwiYXVkIjoid3d3LmV4YW1wbGUuY29tIiwic3ViIjoianJvY2tldEBleGFtcGxlLmNvbSIsIkdpdmVuTmFtZSI6IkpvaG5ueSIsIlN1cm5hbWUiOiJSb2NrZXQiLCJFbWFpbCI6Impyb2NrZXRAZXhhbXBsZS5jb20iLCJSb2xlIjpbIk1hbmFnZXIiLCJQcm9qZWN0IEFkbWluaXN0cmF0b3IiXX0.W_VOkuJ8DRCe_Ht_vmcmsr3G9S4V7FtekeREqlYIb64";

        public SyncService()
        {
            var handler = new HttpClientHandler();
            // Bypass SSL validation for staging/development issues
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Increase timeout for large initial sync
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        private void LogToFile(string message)
        {
            try
            {
                string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_log.txt");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                System.IO.File.AppendAllText(logPath, logMessage);
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch
            {
                // Silently fail if logging doesn't work
            }
        }

        public async Task<List<ProductSyncData>?> DownloadUpdatedItemsAsync(int businessId, int counterNo)
        {
            try
            {
                LogToFile($"=== SYNC STARTED === BusinessID: {businessId}, CounterNo: {counterNo}");
                LogToFile($"API URL: {BaseUrl}product/updated-records/download");

                var content = new MultipartFormDataContent();
                content.Add(new StringContent(businessId.ToString()), "bussiness_id");
                content.Add(new StringContent(counterNo.ToString()), "counter_no");

                LogToFile("Sending HTTP POST request...");
                var response = await _httpClient.PostAsync(BaseUrl + "product/updated-records/download", content);
                
                LogToFile($"HTTP Response: {response.StatusCode} ({(int)response.StatusCode})");
                
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    LogToFile($"ERROR: HTTP {response.StatusCode} - {error}");
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                LogToFile("Response stream opened. Starting deserialization...");
                
                try 
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var apiResponse = await JsonSerializer.DeserializeAsync<ProductApiResponse>(stream, options);
                    
                    LogToFile($"JSON parsed successfully via stream. Status: {apiResponse?.Status}, Count: {apiResponse?.Data?.Count ?? 0}");
                    
                    if (apiResponse?.Data == null || !apiResponse.Status)
                    {
                        LogToFile($"WARNING: No data returned. Status={apiResponse?.Status}, Data is null={apiResponse?.Data == null}");
                        return null;
                    }

                    LogToFile($"Processing {apiResponse.Data.Count} products...");

                    // Map API DTOs to ProductSyncData
                    var syncData = new List<ProductSyncData>();
                    foreach (var apiProduct in apiResponse.Data)
                    {
                        var product = new Item
                        {
                            ItemCode = int.TryParse(apiProduct.ItemCode, out int code) ? code : 0,
                            ItemName = apiProduct.Name ?? "",
                            Company = apiProduct.Brand ?? "",
                            UrduDesc = apiProduct.NameUr ?? "",
                            Packing = apiProduct.Packing ?? "",
                            PackQty = int.TryParse(apiProduct.PackQty, out int pq) ? pq : 1,
                            PPrice = decimal.TryParse(apiProduct.PPrice, out decimal ppItem) ? ppItem : 0,
                            SPrice = decimal.TryParse(apiProduct.SPrice, out decimal spItem) ? spItem : 0,
                            CPrice = decimal.TryParse(apiProduct.CPrice, out decimal cpItem) ? cpItem : 0,
                            RPrice = decimal.TryParse(apiProduct.TPrice, out decimal rpItem) && rpItem > 0 
                                     ? rpItem : (decimal.TryParse(apiProduct.SPrice, out decimal spVal) ? spVal : 0),
                            Stock = decimal.TryParse(apiProduct.Stock, out decimal stock) ? stock : 0,
                            STax = decimal.TryParse(apiProduct.SaleTax, out decimal tax) ? tax : 0
                        };

                        var packings = new List<Packing>();
                        if (apiProduct.ProductPackings != null)
                        {
                            foreach (var apiPacking in apiProduct.ProductPackings)
                            {
                                packings.Add(new Packing
                                {
                                    ItemCode = int.TryParse(apiPacking.ItemCode, out int pkgCode) ? pkgCode : 0,
                                    BarCode = apiPacking.BarCode,
                                    PackingType = apiPacking.Packing,
                                    SPrice = decimal.TryParse(apiPacking.SPrice, out decimal sp) ? sp : 0,
                                    PPrice = decimal.TryParse(apiPacking.PPrice, out decimal pp) ? pp : 0,
                                    CPrice = decimal.TryParse(apiPacking.CPrice, out decimal cp) ? cp : 0,
                                    RPrice = decimal.TryParse(apiPacking.TPrice, out decimal rp) ? rp : 0,
                                    Qty = decimal.TryParse(apiPacking.Qty, out decimal qty) ? qty : 1,
                                    Store = apiPacking.Store
                                });
                            }
                        }

                        LogToFile($"  Product: {product.ItemCode} - {product.ItemName} ({packings.Count} packings)");

                        // Ensure we don't have duplicate packings for this product locally
                        var uniquePackings = packings
                            .GroupBy(p => p.PackingType)
                            .Select(g => g.First())
                            .ToList();

                        syncData.Add(new ProductSyncData
                        {
                            Product = product,
                            Packings = uniquePackings
                        });
                    }

                    // FINAL PROTECTION: Ensure unique ItemCodes in the final list
                    var finalSyncData = syncData
                        .GroupBy(s => s.Product.ItemCode)
                        .Select(g => g.First())
                        .ToList();

                    LogToFile($"=== SYNC COMPLETED === Total products: {finalSyncData.Count}, Total packings: {finalSyncData.Sum(s => s.Packings?.Count ?? 0)}");
                    return finalSyncData;
                }
                catch (Exception ex)
                {
                    LogToFile($"ERROR: JSON Parsing failed - {ex.Message}");
                    LogToFile($"Stack trace: {ex.StackTrace}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"ERROR: Sync exception - {ex.Message}");
                LogToFile($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<bool> ConfirmSyncAsync(int businessId, int counterNo)
        {
            try
            {
                LogToFile($"Confirming sync - BusinessID: {businessId}, CounterNo: {counterNo}");
                
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(businessId.ToString()), "bussiness_id");
                content.Add(new StringContent(counterNo.ToString()), "counter_no");

                var response = await _httpClient.PostAsync(BaseUrl + "product/confirm-sync", content);
                
                LogToFile($"Confirm sync response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LogToFile($"ERROR: Confirm sync exception - {ex.Message}");
                return false;
            }
        }
    }

    // API Response DTOs
    public class ProductApiResponse
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }
        
        [JsonPropertyName("count")]
        public int Count { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("data")]
        public List<ProductApiDto>? Data { get; set; }
    }

    public class ProductApiDto
    {
        [JsonPropertyName("itemcode")]
        public string? ItemCode { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("name_ur")]
        public string? NameUr { get; set; }
        
        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("packing")]
        public string? Packing { get; set; }

        [JsonPropertyName("pack_qty")]
        public string? PackQty { get; set; }

        [JsonPropertyName("sprice")]
        public string? SPrice { get; set; }

        [JsonPropertyName("pprice")]
        public string? PPrice { get; set; }

        [JsonPropertyName("cprice")]
        public string? CPrice { get; set; }
        
        [JsonPropertyName("tprice")]
        public string? TPrice { get; set; }

        [JsonPropertyName("stock")]
        public string? Stock { get; set; }
        
        [JsonPropertyName("sale_tax")]
        public string? SaleTax { get; set; }
        
        [JsonPropertyName("product_packings")]
        public List<PackingApiDto>? ProductPackings { get; set; }
    }

    public class PackingApiDto
    {
        [JsonPropertyName("itemcode")]
        public string? ItemCode { get; set; }
        
        [JsonPropertyName("barcode")]
        public string? BarCode { get; set; }
        
        [JsonPropertyName("packing")]
        public string? Packing { get; set; }
        
        [JsonPropertyName("sprice")]
        public string? SPrice { get; set; }
        
        [JsonPropertyName("pprice")]
        public string? PPrice { get; set; }

        [JsonPropertyName("cprice")]
        public string? CPrice { get; set; }
        
        [JsonPropertyName("tprice")]
        public string? TPrice { get; set; }
        
        [JsonPropertyName("qty")]
        public string? Qty { get; set; }
        
        [JsonPropertyName("store")]
        public string? Store { get; set; }
    }

    // Data structure for sync
    public class ProductSyncData
    {
        public Item Product { get; set; } = new Item();
        public List<Packing> Packings { get; set; } = new List<Packing>();
    }
}
