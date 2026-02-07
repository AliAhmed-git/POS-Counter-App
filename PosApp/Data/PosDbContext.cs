using Microsoft.EntityFrameworkCore;
using PosApp.Desktop.Models;

namespace PosApp.Desktop.Data
{
    public class PosDbContext : DbContext
    {
        public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

        public DbSet<Item> Items { get; set; } = null!;
        public DbSet<Packing> Packings { get; set; } = null!;
        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<SalesHead> SalesHeads { get; set; } = null!;
        public DbSet<SalesDetail> SalesDetails { get; set; } = null!;
        public DbSet<Login> Logins { get; set; } = null!;
        public DbSet<CounterInfo> Counters { get; set; } = null!;
        public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>()
                .HasKey(i => i.ItemCode);
            modelBuilder.Entity<Item>()
                .HasIndex(i => i.ItemName);

            modelBuilder.Entity<Packing>()
                .HasKey(p => new { p.ItemCode, p.PackingType });

            modelBuilder.Entity<Account>()
                .HasKey(a => a.AccountID);

            modelBuilder.Entity<SalesHead>()
                .HasKey(s => s.InvoiceNo);
            modelBuilder.Entity<SalesHead>()
                .Property(s => s.InvoiceNo)
                .ValueGeneratedNever();

            modelBuilder.Entity<SalesDetail>()
                .HasKey(s => new { s.InvoiceNo, s.ItemCode, s.Packing, s.LineNo });

            modelBuilder.Entity<Login>()
                .HasKey(l => l.User);

            modelBuilder.Entity<CounterInfo>()
                .HasKey(c => c.CounterNo);

            // Relationships
            modelBuilder.Entity<SalesDetail>()
                .HasOne<SalesHead>()
                .WithMany(sh => sh.Details)
                .HasForeignKey(sd => sd.InvoiceNo);

            modelBuilder.Entity<SalesDetail>().Ignore(s => s.AvailablePackings);
            modelBuilder.Entity<SalesDetail>().Ignore(s => s.SelectedPacking);
            modelBuilder.Entity<SalesDetail>().Ignore(s => s.HasMultiplePackings);
            modelBuilder.Entity<SalesDetail>().Ignore(s => s.IsFlash);
            modelBuilder.Entity<Item>().Ignore(i => i.HasMultiplePackings);
        }
    }
}
