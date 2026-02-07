using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.EntityFrameworkCore;
using PosApp.Desktop.Data;
using PosApp.Desktop.Models;

namespace PosApp.Desktop.Services
{
    public interface IDataService
    {
        Task<Login?> AuthenticateAsync(string username, string password);
        Task<bool> UserExistsAsync(string username);
        Task<List<CounterInfo>> GetCountersAsync();
        Task<CounterInfo?> GetCounterAsync(int counterNo);
        Task<List<Item>> SearchItemsAsync(string criteria, string filterType, int skip = 0, int take = 50);
        Task<Item?> GetItemByBarcodeAsync(string barcode);
        Task<List<Packing>> GetPackingsForItemAsync(int itemCode);
        Task<bool> ProcessSaleAsync(SalesHead sale);
        Task<int> GetNextInvoiceNoAsync();
        Task<SalesHead?> GetLastSaleAsync();
        Task<SalesHead?> GetSaleByInvoiceAsync(int invoiceNo);
        Task<List<Packing>> GetRandomPackingsAsync(int count);
        Task<List<SalesHead>> GetSalesForDCRAsync(DateTime date);
        Task EnsureDatabaseCreatedAsync();
        Task BackupDatabaseAsync();
        Task SyncItemsAsync(List<ProductSyncData> syncData);
        Task ClearAllProductsAsync();
        Task<bool> DeleteSaleAsync(int invoiceNo);
        Task<List<PaymentMethod>> GetPaymentMethodsAsync();
    }

    public class DataService : IDataService
    {
        private readonly IDbContextFactory<PosDbContext> _contextFactory;
        private bool _isDemoMode = false;

        public DataService(IDbContextFactory<PosDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task EnsureDatabaseCreatedAsync()
        {
            DebugLog("EnsureDatabaseCreatedAsync started");
            
            // Set a total time limit for DB init to avoid hanging the entire app
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(45));
            
            try 
            {
                // 1. Core creation 
                using (var _context = await _contextFactory.CreateDbContextAsync())
                {
                    DebugLog("Running EnsureCreatedAsync");
                    // EnsureCreated only creates the file/schema if it doesn't exist.
                    // If it exists, it returns false and does nothing.
                    bool created = await _context.Database.EnsureCreatedAsync(cts.Token);
                    DebugLog($"EnsureCreatedAsync checked/ran (created={created})");
                    
                    if (created || !await _context.Logins.AnyAsync(cts.Token))
                    {
                        DebugLog("Migration check triggered");
                        await MigrateFromAccessIfNeededAsync();
                        DebugLog("Migration process finished");
                    }
                }

                // 2. Self-healing (fresh context)
                try
                {
                    DebugLog("Self-healing phase started");
                    using (var _context = await _contextFactory.CreateDbContextAsync())
                    {
                        var conn = _context.Database.GetDbConnection();
                        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(cts.Token);
                        
                        try 
                        {
                            await RunSelfHealingAsync(conn);
                        }
                        finally 
                        {
                            if (conn.State == System.Data.ConnectionState.Open) await conn.CloseAsync();
                        }
                    }
                }
                catch (OperationCanceledException) { DebugLog("Self-healing timed out."); }
                catch (Exception ex)
                {
                    DebugLog($"Self-healing failed: {ex.Message}");
                }

                // 3. Seeding defaults (fresh context)
                DebugLog("Seeding data check started");
                using (var _context = await _contextFactory.CreateDbContextAsync())
                {
                    try {
                        if (!await _context.Accounts.AnyAsync(a => a.AccountID == 1, cts.Token))
                        {
                            _context.Accounts.Add(new Account { AccountID = 1, Title = "CASH ACCOUNT" });
                            await _context.SaveChangesAsync(cts.Token);
                        }
                    } catch (Exception ex) { DebugLog($"Seed Accounts failed: {ex.Message}"); }

                    try {
                        if (!await _context.Counters.AnyAsync(c => c.CounterNo == 1, cts.Token))
                        {
                            _context.Counters.Add(new CounterInfo { CounterNo = 1, CounterName = "Counter 01", SupervisorKey = "123" });
                            await _context.SaveChangesAsync(cts.Token);
                        }
                    } catch (Exception ex) { DebugLog($"Seed Counters failed: {ex.Message}"); }

                        // Always call SyncPaymentMethodsAsync to ensure high-quality logos and latest charges
                        await SyncPaymentMethodsAsync(_context);

                    try {
                        if (!await _context.Logins.AnyAsync(l => l.User == "admin", cts.Token))
                        {
                            _context.Logins.Add(new Login { User = "admin", Password = "123", Roll = "Admin" });
                            await _context.SaveChangesAsync(cts.Token);
                        }
                    } catch (Exception ex) { DebugLog($"Seed Admin Login failed: {ex.Message}"); }
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog("Database initialization TIMED OUT. Some updates might be skipped.");
            }
            catch (Exception ex)
            {
                DebugLog($"Database initialization critical failure: {ex.Message}.");
            }
            finally
            {
                DebugLog("EnsureDatabaseCreatedAsync finished");
            }
        }

        private async Task RunSelfHealingAsync(System.Data.Common.DbConnection conn)
        {
            using (var command = conn.CreateCommand())
            {
                // 1. Check SalesDetails
                DebugLog("Checking table_info for SalesDetails");
                command.CommandText = "PRAGMA table_info(SalesDetails);";
                var sdColumns = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) sdColumns.Add(reader["name"].ToString() ?? "");
                }

                string[] requiredSCols = { "LineNo", "Discount", "TaxAmount", "NetAmount", "PPrice", "RPrice", "Packing", "BatchNo" };
                foreach (var col in requiredSCols)
                {
                    if (!sdColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                    {
                        DebugLog($"Adding column {col} to SalesDetails");
                        using var cmd = conn.CreateCommand();
                        string type = (col == "LineNo") ? "INTEGER" : (col == "Packing" || col == "BatchNo") ? "TEXT" : "NUMERIC";
                        cmd.CommandText = $"ALTER TABLE SalesDetails ADD COLUMN {col} {type} DEFAULT {(type == "TEXT" ? "''" : "0")};";
                        await ExecuteNonQueryWithRetryAsync(conn, cmd.CommandText);
                    }
                }

                // 1.1 Rebuild SalesDetails if PK is missing (simplified check)
                command.CommandText = "PRAGMA table_info(SalesDetails);";
                int pkCount = 0;
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) if (Convert.ToInt32(reader["pk"]) > 0) pkCount++;
                }
                
                if (pkCount > 0 && pkCount < 4)
                {
                    DebugLog("Rebuilding SalesDetails table for correct PK");
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE SalesDetails_New (
                            InvoiceNo INTEGER NOT NULL,
                            ItemCode INTEGER NOT NULL,
                            LineNo INTEGER NOT NULL DEFAULT 1,
                            ItemName TEXT,
                            Company TEXT,
                            Packing TEXT NOT NULL DEFAULT 'Standard',
                            Qty NUMERIC NOT NULL DEFAULT 0,
                            SPrice NUMERIC NOT NULL DEFAULT 0,
                            PPrice NUMERIC NOT NULL DEFAULT 0,
                            RPrice NUMERIC NOT NULL DEFAULT 0,
                            Discount NUMERIC NOT NULL DEFAULT 0,
                            TaxAmount NUMERIC NOT NULL DEFAULT 0,
                            NetAmount NUMERIC NOT NULL DEFAULT 0,
                            BatchNo TEXT,
                            PRIMARY KEY (InvoiceNo, ItemCode, Packing, LineNo)
                        );
                        INSERT INTO SalesDetails_New (InvoiceNo, ItemCode, LineNo, ItemName, Company, Packing, Qty, SPrice, PPrice, RPrice, Discount, TaxAmount, NetAmount, BatchNo)
                        SELECT InvoiceNo, ItemCode, LineNo, ItemName, Company, Packing, Qty, SPrice, PPrice, RPrice, Discount, TaxAmount, NetAmount, BatchNo FROM SalesDetails;
                        DROP TABLE SalesDetails;
                        ALTER TABLE SalesDetails_New RENAME TO SalesDetails;";
                    await ExecuteNonQueryWithRetryAsync(conn, cmd.CommandText);
                }

                // 2. Check Items
                DebugLog("Checking table_info for Items");
                command.CommandText = "PRAGMA table_info(Items);";
                var itemColumns = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) itemColumns.Add(reader["name"].ToString() ?? "");
                }

                string[] requiredICols = { "STax", "Group", "Type", "Company", "UrduDesc", "Location", "Packing", "PackQty", "PPrice", "SPrice", "RPrice", "CPrice" };
                foreach (var col in requiredICols)
                {
                    if (!itemColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                    {
                        using var cmd = conn.CreateCommand();
                        string type = (col == "STax" || col == "PPrice" || col == "SPrice" || col == "RPrice" || col == "CPrice") ? "NUMERIC" : (col == "PackQty") ? "INTEGER" : "TEXT";
                        cmd.CommandText = $"ALTER TABLE Items ADD COLUMN {col} {type} DEFAULT {(type == "TEXT" ? "''" : "0")};";
                        await ExecuteNonQueryWithRetryAsync(conn, cmd.CommandText);
                    }
                }

                // 3. SalesHeads Fixes
                DebugLog("Checking table_info for SalesHeads");
                command.CommandText = "PRAGMA table_info(SalesHeads);";
                var shColumns = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) shColumns.Add(reader["name"].ToString() ?? "");
                }

                var salesHeadFixes = new Dictionary<string, string>
                {
                    { "IsRefund", "INTEGER DEFAULT 0" },
                    { "PaymentMethod", "TEXT DEFAULT 'Cash'" },
                    { "CardPaid", "NUMERIC DEFAULT 0" },
                    { "ServiceCharge", "NUMERIC DEFAULT 0" },
                    { "InvoiceDiscount", "NUMERIC DEFAULT 0" }
                };

                foreach (var fix in salesHeadFixes)
                {
                    if (!shColumns.Contains(fix.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"ALTER TABLE SalesHeads ADD COLUMN {fix.Key} {fix.Value};";
                        await ExecuteNonQueryWithRetryAsync(conn, cmd.CommandText);
                    }
                }

                // 4. Check/Create PaymentMethods table
                DebugLog("Checking PaymentMethods table existence");
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='PaymentMethods';";
                if (await command.ExecuteScalarAsync() == null)
                {
                    DebugLog("Creating PaymentMethods table");
                    await ExecuteNonQueryWithRetryAsync(conn, @"
                        CREATE TABLE PaymentMethods (
                            Method TEXT PRIMARY KEY,
                            ChargePercentage NUMERIC NOT NULL DEFAULT 0,
                            TaxPercentage NUMERIC NOT NULL DEFAULT 0,
                            ImagePath TEXT,
                            IsSelected INTEGER DEFAULT 0
                        );");
                }

                // 5. Check Counters table for SupervisorKey
                DebugLog("Checking table_info for Counters");
                command.CommandText = "PRAGMA table_info(Counters);";
                var counterColumns = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) counterColumns.Add(reader["name"].ToString() ?? "");
                }

                if (!counterColumns.Contains("SupervisorKey", StringComparer.OrdinalIgnoreCase))
                {
                    DebugLog("Adding column SupervisorKey to Counters");
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "ALTER TABLE Counters ADD COLUMN SupervisorKey TEXT DEFAULT '123';";
                    await ExecuteNonQueryWithRetryAsync(conn, cmd.CommandText);
                }
            }
        }

        private void DebugLog(string message)
        {
            try { File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] (DataService) {message}\n"); } catch { }
        }

        private async Task MigrateFromAccessIfNeededAsync()
        {
            try
            {
                // Try to find the Access database in common locations
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string accessPath = Path.Combine(appDir, "NewCounter 7.0.accdb");
                
                // Fallback for development path if not found in app dir
                if (!System.IO.File.Exists(accessPath))
                {
                    accessPath = @"C:\xampp\htdocs\pos-counter\NewCounter 7.0.accdb";
                }

                if (!System.IO.File.Exists(accessPath)) return;

                string connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={accessPath};";
                using var conn = new System.Data.OleDb.OleDbConnection(connectionString);
                conn.Open();

                using var _context = await _contextFactory.CreateDbContextAsync();

                // 0. Migrate Accounts
                var addedAccounts = new HashSet<int>();
                var accountCmd = new System.Data.OleDb.OleDbCommand("SELECT [AccountID], [Title] FROM [Accounts]", conn);
                using (var reader = accountCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = Convert.ToInt32(reader["AccountID"]);
                        if (!addedAccounts.Contains(id) && !await _context.Accounts.AnyAsync(a => a.AccountID == id))
                        {
                            _context.Accounts.Add(new Account { AccountID = id, Title = reader["Title"].ToString() });
                            addedAccounts.Add(id);
                        }
                    }
                }

                // 1. Migrate Logins
                var addedLogins = new HashSet<string>();
                var loginCmd = new System.Data.OleDb.OleDbCommand("SELECT [User], [Password] FROM [Login]", conn);
                using (var reader = loginCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var user = reader["User"].ToString();
                        if (user != null && !addedLogins.Contains(user) && !await _context.Logins.AnyAsync(l => l.User == user))
                        {
                            _context.Logins.Add(new Login { User = user, Password = reader["Password"].ToString() ?? "", Roll = "User" });
                            addedLogins.Add(user);
                        }
                    }
                }

                // 2. Migrate Counters
                var addedCounters = new HashSet<int>();
                var counterCmd = new System.Data.OleDb.OleDbCommand("SELECT [CounterNo], [CounterName] FROM [CounterInfo]", conn);
                using (var reader = counterCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var no = Convert.ToInt32(reader["CounterNo"]);
                        if (!addedCounters.Contains(no) && !await _context.Counters.AnyAsync(c => c.CounterNo == no))
                        {
                            _context.Counters.Add(new CounterInfo { CounterNo = no, CounterName = reader["CounterName"].ToString() });
                            addedCounters.Add(no);
                        }
                    }
                }

                // 3. Migrate Items
                var addedItems = new HashSet<int>();
                var itemCmd = new System.Data.OleDb.OleDbCommand("SELECT [ItemCode], [ItemName], [Company], [SPrice], [Packing], [STax], [Stock] FROM [Items]", conn);
                using (var reader = itemCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var code = Convert.ToInt32(reader["ItemCode"]);
                        if (!addedItems.Contains(code) && !await _context.Items.AnyAsync(i => i.ItemCode == code))
                        {
                            _context.Items.Add(new Item 
                            { 
                                ItemCode = code, 
                                ItemName = reader["ItemName"].ToString(),
                                Company = reader["Company"].ToString(),
                                SPrice = reader["SPrice"] != DBNull.Value ? Convert.ToDecimal(reader["SPrice"]) : 0m,
                                Packing = reader["Packing"].ToString(),
                                STax = reader["STax"] != DBNull.Value ? Convert.ToDecimal(reader["STax"]) : 0m,
                                Stock = reader["Stock"] != DBNull.Value ? Convert.ToDecimal(reader["Stock"]) : 0m
                            });
                            addedItems.Add(code);
                        }
                    }
                }

                // 4. Migrate Packings
                var addedPackings = new HashSet<string>(); // Key: "ItemCode|PackingType"
                var packingCmd = new System.Data.OleDb.OleDbCommand("SELECT [ItemCode], [Packing] as PackingType, [BarCode], [SPrice], [Qty], [PPrice], [RPrice], [CPrice] FROM [Packings]", conn);
                using (var reader = packingCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var code = Convert.ToInt32(reader["ItemCode"]);
                        var type = reader["PackingType"].ToString() ?? "";
                        var key = $"{code}|{type}";
                        
                        if (!addedPackings.Contains(key) && !await _context.Packings.AnyAsync(p => p.ItemCode == code && p.PackingType == type))
                        {
                            _context.Packings.Add(new Packing
                            {
                                ItemCode = code,
                                PackingType = type,
                                BarCode = reader["BarCode"].ToString(),
                                SPrice = reader["SPrice"] != DBNull.Value ? Convert.ToDecimal(reader["SPrice"]) : 0m,
                                Qty = reader["Qty"] != DBNull.Value ? Convert.ToDecimal(reader["Qty"]) : 0m,
                                PPrice = reader["PPrice"] != DBNull.Value ? Convert.ToDecimal(reader["PPrice"]) : 0m,
                                RPrice = reader["RPrice"] != DBNull.Value ? Convert.ToDecimal(reader["RPrice"]) : 0m,
                                CPrice = reader["CPrice"] != DBNull.Value ? Convert.ToDecimal(reader["CPrice"]) : 0m
                            });
                            addedPackings.Add(key);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine("Migration from Access to SQLite completed successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration failed: {ex.Message}");
            }
        }

        public async Task<Login?> AuthenticateAsync(string username, string password)
        {
            // Fallback for Admin
            if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) && password == "123")
            {
                return new Login { User = "admin", Password = "123", Roll = "Admin" };
            }

            try 
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                return await _context.Logins
                    .FirstOrDefaultAsync(l => EF.Functions.Like(l.User ?? "", username) && EF.Functions.Like(l.Password ?? "", password));
            }
            catch (Exception ex)
            {
                DebugLog($"Database login failed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase)) return true;

            try 
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                return await _context.Logins.AnyAsync(l => EF.Functions.Like(l.User ?? "", username));
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<CounterInfo>> GetCountersAsync()
        {
            if (_isDemoMode)
            {
                return new List<CounterInfo>
                {
                    new CounterInfo { CounterNo = 1, CounterName = "Counter 01", SupervisorKey = "123" },
                    new CounterInfo { CounterNo = 2, CounterName = "Counter 02", SupervisorKey = "123" }
                };
            }

            using var _context = await _contextFactory.CreateDbContextAsync();
            return await _context.Counters.ToListAsync();
        }

        public async Task<CounterInfo?> GetCounterAsync(int counterNo)
        {
            if (_isDemoMode) return new CounterInfo { CounterNo = counterNo, CounterName = $"Counter {counterNo:D2}", SupervisorKey = "123" };
            using var _context = await _contextFactory.CreateDbContextAsync();
            return await _context.Counters.FirstOrDefaultAsync(c => c.CounterNo == counterNo);
        }

        public async Task<List<Item>> SearchItemsAsync(string criteria, string filterType, int skip = 0, int take = 50)
        {
            if (_isDemoMode)
            {
                return new List<Item>
                {
                    new Item { ItemCode = 12169, ItemName = "Tapal Mezban 190G", Company = "Tapal", SPrice = 390m, Packing = "POUCH" },
                    new Item { ItemCode = 227, ItemName = "Danedar Tea 85G", Company = "Tapal", SPrice = 1110m, Packing = "Pkt" }
                };
            }

            using var _context = await _contextFactory.CreateDbContextAsync();
            var query = _context.Items.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(criteria))
            {
                var searchWords = criteria.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                switch (filterType)
                {
                    case "Item Name":
                        if (searchWords.Length > 1)
                        {
                            // Multi-word search: Item contains ANY of the words (OR logic)
                            // This provides a broader pool for the fuzzy scoring to filter
                            var predicates = searchWords.Select(w => (string)$"%{w}%").ToList();
                            query = query.Where(i => i.ItemName != null && predicates.Any(p => EF.Functions.Like(i.ItemName, p)));
                        }
                        else
                        {
                            query = query.Where(i => (i.ItemName != null && EF.Functions.Like(i.ItemName, $"%{criteria}%")) ||
                                                     (i.Company != null && EF.Functions.Like(i.Company, $"%{criteria}%")) ||
                                                     (i.Group != null && EF.Functions.Like(i.Group, $"%{criteria}%")) ||
                                                     (i.Type != null && EF.Functions.Like(i.Type, $"%{criteria}%")));
                        }
                        break;
                    case "Company":
                        query = query.Where(i => i.Company != null && EF.Functions.Like(i.Company, $"%{criteria}%"));
                        break;
                    case "Item Code":
                        if (int.TryParse(criteria, out int code))
                            query = query.Where(i => i.ItemCode == code);
                        break;
                }
            }

            var items = await query.OrderBy(i => i.ItemName)
                        .Skip(skip)
                        .Take(take)
                        .ToListAsync();

            if (items.Any())
            {
                var itemCodes = items.Select(i => i.ItemCode).ToList();
                var allPackings = await _context.Packings
                    .AsNoTracking()
                    .Where(p => itemCodes.Contains(p.ItemCode))
                    .ToListAsync();

                var scoredItems = new List<(Item item, int score)>();

                foreach (var item in items)
                {
                    // Calculate similarity score (lower is better/closer)
                    int score = 100; // Base score
                    string itemName = item.ItemName?.ToLower() ?? "";
                    string searchCriteria = criteria.ToLower();

                    if (itemName == searchCriteria) score = 0;
                    else if (itemName.StartsWith(searchCriteria)) score = 10;
                    else if (itemName.Contains(searchCriteria)) score = 20;
                    else score = LevenshteinDistance(itemName, searchCriteria);

                    var itemPackings = allPackings.Where(p => p.ItemCode == item.ItemCode).ToList();
                    item.HasMultiplePackings = itemPackings.Count > 1;
                    
                    if (itemPackings.Any())
                    {
                        var firstPkg = itemPackings.First();
                        if (item.SPrice == 0)
                        {
                            item.SPrice = firstPkg.SPrice;
                            item.Packing = firstPkg.PackingType;
                            item.PPrice = firstPkg.PPrice;
                        }
                        item.PackQty = (int)Math.Max(1, firstPkg.Qty);
                    }
                    scoredItems.Add((item, score));
                }

                return scoredItems.OrderBy(x => x.score)
                                  .ThenByDescending(x => x.item.Stock)
                                  .Select(x => x.item).ToList();
            }
            
            // FALLBACK: If no results found with direct LIKE, try a broader fuzzy search
            if (!string.IsNullOrWhiteSpace(criteria) && filterType == "Item Name")
            {
                var searchWords = criteria.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (searchWords.Any())
                {
                    string firstWord = searchWords[0];
                    if (firstWord.Length >= 2)
                    {
                        // Broad search: items starting with first 2 letters of first word
                        string prefix = firstWord.Substring(0, 2);
                        var fallbackItems = await _context.Items
                            .AsNoTracking()
                            .Where(i => i.ItemName != null && EF.Functions.Like(i.ItemName, $"{prefix}%"))
                            .Take(100)
                            .ToListAsync();

                        if (fallbackItems.Any())
                        {
                            var scoredFallbacks = new List<(Item item, int score)>();
                            foreach (var item in fallbackItems)
                            {
                                int dist = LevenshteinDistance(item.ItemName?.ToLower() ?? "", criteria.ToLower());
                                // Only keep reasonably close matches
                                if (dist < criteria.Length)
                                {
                                    scoredFallbacks.Add((item, dist));
                                }
                            }
                            return scoredFallbacks.OrderBy(x => x.score)
                                                  .ThenByDescending(x => x.item.Stock)
                                                  .Select(x => x.item).ToList();
                        }
                    }
                }
            }

            return items;
        }

        private int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        public async Task<Item?> GetItemByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;

            // Demo data for ease of testing
            if (_isDemoMode)
            {
                if (barcode == "1001" || barcode == "8853301012881") return new Item { ItemCode = 12169, ItemName = "Tapal Mezban 190G", Company = "Tapal", SPrice = 390m, Packing = "POUCH", STax = 17m };
                if (barcode == "1002" || barcode == "8961008237961") return new Item { ItemCode = 227, ItemName = "Danedar Tea 85G", Company = "Tapal", SPrice = 1110m, Packing = "Pkt", STax = 17m };
                return null;
            }

            using var _context = await _contextFactory.CreateDbContextAsync();
            
            // 1. ALWAYS Match barcode to Packing table FIRST
            var packing = await _context.Packings
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.BarCode == barcode);

            if (packing != null)
            {
                // 2. Match ItemCode to Items table
                var item = await _context.Items
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.ItemCode == packing.ItemCode);

                if (item != null)
                {
                    // Override item properties with specific packing context
                    item.Packing = packing.PackingType;
                    item.SPrice = packing.SPrice;
                    item.RPrice = packing.RPrice > 0 ? packing.RPrice : packing.SPrice;
                    item.PPrice = packing.PPrice > 0 ? packing.PPrice : packing.SPrice;
                    item.CPrice = packing.CPrice > 0 ? packing.CPrice : packing.SPrice;
                }
                return item;
            }

            // 3. Fallback: Lookup by ItemCode directly ONLY if no packing barcode matches
            if (int.TryParse(barcode, out int itemCode))
            {
                var item = await _context.Items
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.ItemCode == itemCode);
                
                // If found via ItemCode, try to load its default/base packing if available
                if (item != null)
                {
                    var defaultPacking = await _context.Packings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ItemCode == item.ItemCode);
                        
                    if (defaultPacking != null)
                    {
                        item.Packing = defaultPacking.PackingType;
                        item.SPrice = defaultPacking.SPrice;
                        item.RPrice = defaultPacking.RPrice > 0 ? defaultPacking.RPrice : defaultPacking.SPrice;
                        item.PPrice = defaultPacking.PPrice > 0 ? defaultPacking.PPrice : defaultPacking.SPrice;
                        item.CPrice = defaultPacking.CPrice > 0 ? defaultPacking.CPrice : defaultPacking.SPrice;
                    }
                }
                return item;
            }

            return null;
        }

        public async Task<List<Packing>> GetPackingsForItemAsync(int itemCode)
        {
            if (_isDemoMode)
            {
                return new List<Packing>
                {
                    new Packing { ItemCode = itemCode, PackingType = "Pc", SPrice = 100m, Qty = 1m },
                    new Packing { ItemCode = itemCode, PackingType = "Box", SPrice = 1100m, Qty = 12m }
                };
            }

            using var _context = await _contextFactory.CreateDbContextAsync();
            return await _context.Packings
                .Where(p => p.ItemCode == itemCode)
                .ToListAsync();
        }

        public async Task<bool> ProcessSaleAsync(SalesHead sale)
        {
            if (sale == null || sale.Details == null || !sale.Details.Any())
            {
                try { System.IO.File.AppendAllText("db_error_log.txt", $"[{DateTime.Now}] Validation failed: Sale is null or has no details.\n"); } catch {}
                return false;
            }

            int retryCount = 0;
            while (retryCount < 5)
            {
                try
                {
                    using var _context = await _contextFactory.CreateDbContextAsync();
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Ensure we have the latest invoice number if it's set to 0
                        if (sale.InvoiceNo <= 0)
                        {
                            if (!await _context.SalesHeads.AnyAsync()) sale.InvoiceNo = 1;
                            else sale.InvoiceNo = await _context.SalesHeads.MaxAsync(s => s.InvoiceNo) + 1;
                        }

                        // PKR specific: Round all currency values to whole numbers before saving
                        sale.TotalAmount = Math.Round(sale.TotalAmount, 0);
                        sale.CashPaid = Math.Round(sale.CashPaid, 0);
                        sale.CardPaid = Math.Round(sale.CardPaid, 0);
                        sale.ServiceCharge = Math.Round(sale.ServiceCharge, 0);

                        // Ensure all details have the correct InvoiceNo and LineNo BEFORE adding to context
                        int lineNo = 1;
                        foreach (var detail in sale.Details)
                        {
                            detail.InvoiceNo = sale.InvoiceNo;
                            detail.LineNo = lineNo++;
                            
                            // Round line item values
                            detail.SPrice = Math.Round(detail.SPrice, 0);
                            detail.NetAmount = Math.Round(detail.NetAmount, 0);
                            detail.TaxAmount = Math.Round(detail.TaxAmount, 0);
                            detail.Discount = Math.Round(detail.Discount, 0);
                        }

                        _context.SalesHeads.Add(sale);
                        
                        /* 
                        // Stock update disabled as per user request
                        foreach (var detail in sale.Details)
                        {
                            // Fetch fresh item instance from context to ensure it's tracked correctly
                            var item = await _context.Items.FindAsync(detail.ItemCode);
                            if (item != null)
                            {
                                item.Stock -= detail.Qty;
                            }
                        }
                        */

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return true;
                    }
                    catch (Exception ex) when (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase))
                    {
                        await transaction.RollbackAsync();
                        retryCount++;
                        DebugLog($"ProcessSale locked, retry {retryCount}/5...");
                        await Task.Delay(1000 * retryCount);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        string errorMsg = $"[{DateTime.Now}] Error processing sale: {ex.Message}\nStack: {ex.StackTrace}\n";
                        if (ex.InnerException != null)
                            errorMsg += $"Inner: {ex.InnerException.Message}\n";
                        
                        try { System.IO.File.AppendAllText("db_error_log.txt", errorMsg); } catch {}
                        return false;
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase))
                {
                    retryCount++;
                    DebugLog($"ProcessSale (Outer) locked, retry {retryCount}/5...");
                    await Task.Delay(1000 * retryCount);
                }
                catch (Exception ex)
                {
                    string errorMsg = $"[{DateTime.Now}] Critical DB Error in ProcessSale: {ex.Message}\n";
                    try { System.IO.File.AppendAllText("db_error_log.txt", errorMsg); } catch {}
                    return false;
                }
            }
            return false;
        }

        public async Task<int> GetNextInvoiceNoAsync()
        {
            try
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                if (!await _context.SalesHeads.AnyAsync()) return 1;
                return await _context.SalesHeads.MaxAsync(s => s.InvoiceNo) + 1;
            }
            catch
            {
                return 1;
            }
        }

        public async Task<SalesHead?> GetLastSaleAsync()
        {
            if (_isDemoMode) return null;

            try
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                var sale = await _context.SalesHeads
                    .OrderByDescending(s => s.InvoiceNo)
                    .FirstOrDefaultAsync();

                if (sale != null)
                {
                    // Explicitly load details
                    sale.Details = await _context.SalesDetails
                        .Where(d => d.InvoiceNo == sale.InvoiceNo)
                        .OrderBy(d => d.LineNo)
                        .ToListAsync();
                }

                return sale;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching last sale: {ex.Message}");
                return null;
            }
        }
        public async Task<SalesHead?> GetSaleByInvoiceAsync(int invoiceNo)
        {
            using var _context = await _contextFactory.CreateDbContextAsync();
            return await _context.SalesHeads
                .Include(s => s.Details)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.InvoiceNo == invoiceNo);
        }

        public async Task<List<Packing>> GetRandomPackingsAsync(int count)
        {
            if (_isDemoMode)
            {
                return new List<Packing>
                {
                    new Packing { ItemCode = 12169, PackingType = "POUCH", BarCode = "8853301012881", SPrice = 390 },
                    new Packing { ItemCode = 227, PackingType = "Pkt", BarCode = "8961008237961", SPrice = 1110 }
                };
            }

            try
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                // Randomly select packings that have barcodes
                return await _context.Packings
                    .Where(p => !string.IsNullOrEmpty(p.BarCode))
                    .OrderBy(p => EF.Functions.Random())
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching random packings: {ex.Message}");
                return new List<Packing>();
            }
        }

        public async Task<List<SalesHead>> GetSalesForDCRAsync(DateTime date)
        {
            if (_isDemoMode) return new List<SalesHead>();

            try
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1);

                return await _context.SalesHeads
                    .Include(s => s.Details)
                    .Where(s => s.Date >= startOfDay && s.Date < endOfDay)
                    .OrderBy(s => s.InvoiceNo)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching sales for DCR: {ex.Message}");
                return new List<SalesHead>();
            }
        }

        public async Task BackupDatabaseAsync()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pos.db");
                if (!System.IO.File.Exists(dbPath)) return;

                string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"pos_backup_{timestamp}.db");

                // Use File.Copy for a simple snapshot. 
                // Note: In a production app with high concurrency, we'd use SQLite's backup API,
                // but for a single-user POS app, File.Copy is usually fine if we don't have open write transactions.
                await Task.Run(() => System.IO.File.Copy(dbPath, backupPath, true));

                // Cleanup old backups (keep last 30)
                var files = Directory.GetFiles(backupDir, "*.db")
                                     .OrderByDescending(f => f)
                                     .Skip(30);
                foreach (var file in files)
                {
                    try { System.IO.File.Delete(file); } catch { }
                }

                System.Diagnostics.Debug.WriteLine($"Database backup created: {backupPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup failed: {ex.Message}");
            }
        }
        public async Task ClearAllProductsAsync()
        {
            using var _context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Items;");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Packings;");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                System.Diagnostics.Debug.WriteLine("All product data cleared successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"Error clearing data: {ex.Message}");
                throw;
            }
        }
        public async Task SyncItemsAsync(List<ProductSyncData> syncData)
        {
            if (syncData == null || !syncData.Any()) return;

            using var _context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Optimization: Disable tracking for lookups to keep memory low
                _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                foreach (var data in syncData)
                {
                    var product = data.Product;
                    
                    // STEP 1: Handle Product
                    var existingProduct = await _context.Items.FirstOrDefaultAsync(i => i.ItemCode == product.ItemCode);
                    if (existingProduct != null)
                    {
                        _context.Items.Remove(existingProduct);
                    }
                    _context.Items.Add(product);
                    
                    // STEP 2: Handle Packings
                    var existingPackings = await _context.Packings
                        .Where(p => p.ItemCode == product.ItemCode)
                        .ToListAsync();
                    
                    if (existingPackings.Any())
                    {
                        _context.Packings.RemoveRange(existingPackings);
                    }
                    
                    if (data.Packings != null && data.Packings.Any())
                    {
                        foreach (var packing in data.Packings)
                        {
                            // Safety check for empty keys
                            if (string.IsNullOrEmpty(packing.PackingType)) packing.PackingType = "Standard";
                            _context.Packings.Add(packing);
                        }
                    }
                }

                // Restore tracking before save
                _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                System.Diagnostics.Debug.WriteLine($"Successfully synced {syncData.Count} products");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                string errorLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_db_error.txt");
                string message = $"[{DateTime.Now}] SYNC DB ERROR: {ex.Message}\nInner: {ex.InnerException?.Message}\nStack: {ex.StackTrace}\n";
                File.AppendAllText(errorLog, message);
                throw;
            }
        }
        public async Task<List<PaymentMethod>> GetPaymentMethodsAsync()
        {
            if (_isDemoMode) return new List<PaymentMethod>();
            using var _context = await _contextFactory.CreateDbContextAsync();
            return await _context.PaymentMethods.ToListAsync();
        }

        private async Task ExecuteNonQueryWithRetryAsync(System.Data.Common.DbConnection conn, string commandText)
        {
            int retryCount = 0;
            while (retryCount < 5)
            {
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = commandText;
                    await cmd.ExecuteNonQueryAsync();
                    return;
                }
                catch (Exception ex) when (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase))
                {
                    retryCount++;
                    DebugLog($"Database locked, retry {retryCount}/5...");
                    await Task.Delay(2000 * retryCount);
                    if (retryCount >= 5) throw;
                }
            }
        }

        private async Task SyncPaymentMethodsAsync(PosDbContext context)
        {
            try
            {
                var imageMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Alfalah Card", "alfalah.png" },
                    { "HBL Card", "hbl.png" },
                    { "MCB Card", "mcb.png" },
                    { "UBL Card", "ubl.png" },
                    { "Meezan Card", "meezan.png" },
                    { "Allied Bank Card", "allied_bank.png" },
                    { "Bank Al Habib Card", "bank-al-habib-logo.png" },
                    { "Faysal Bank Card", "faysal_bank_logo.png" },
                    { "SadaPay", "sadapay.png" },
                    { "Nayapay", "nayapay.png" },
                    { "Raast", "Raast_ID.png" },
                    { "JazzCash", "jazzcash.png" },
                    { "EasyPaisa", "easypaisa.png" },
                    { "Cash", "cash.png" },
                    { "Other Bank Card", "other.png" }
                };

                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payment_charges.json");
                if (File.Exists(jsonPath))
                {
                    string json = await File.ReadAllTextAsync(jsonPath);
                    var data = System.Text.Json.JsonSerializer.Deserialize<List<PaymentMethodJsonModel>>(json);
                    if (data != null)
                    {
                        var existingMethods = await context.PaymentMethods.ToListAsync();

                        foreach (var item in data)
                        {
                            if (string.IsNullOrEmpty(item.Method)) continue;
                            
                            string fileName = imageMappings.ContainsKey(item.Method) ? imageMappings[item.Method] : $"{item.Method.ToLower().Replace(" ", "_")}.png";
                            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "payment_logos", fileName);

                            var existing = existingMethods.FirstOrDefault(m => string.Equals(m.Method, item.Method, StringComparison.OrdinalIgnoreCase));
                            if (existing != null)
                            {
                                existing.ChargePercentage = item.ChargePercentage;
                                existing.TaxPercentage = item.TaxPercentage;
                                existing.ImagePath = imagePath;
                                context.PaymentMethods.Update(existing);
                            }
                            else
                            {
                                context.PaymentMethods.Add(new PaymentMethod
                                {
                                    Method = item.Method,
                                    ChargePercentage = item.ChargePercentage,
                                    TaxPercentage = item.TaxPercentage,
                                    ImagePath = imagePath
                                });
                            }
                        }
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to seed payment methods: {ex.Message}");
            }
        }

        private class PaymentMethodJsonModel
        {
            public string? Method { get; set; }
            public decimal ChargePercentage { get; set; }
            public decimal TaxPercentage { get; set; }
        }
        public async Task<bool> DeleteSaleAsync(int invoiceNo)
        {
            if (_isDemoMode) return false;

            try
            {
                using var _context = await _contextFactory.CreateDbContextAsync();
                var sale = await _context.SalesHeads
                    .Include(s => s.Details)
                    .FirstOrDefaultAsync(s => s.InvoiceNo == invoiceNo);

                if (sale != null)
                {
                    _context.SalesDetails.RemoveRange(sale.Details);
                    _context.SalesHeads.Remove(sale);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                DebugLog($"DeleteSaleAsync failed: {ex.Message}");
                return false;
            }
        }
    }
}
