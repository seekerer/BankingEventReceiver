using Microsoft.EntityFrameworkCore;
using BankingApi.EventReceiver.Models;

namespace BankingApi.EventReceiver
{
    public class BankingApiDbContext : DbContext
    {
        public BankingApiDbContext(DbContextOptions<BankingApiDbContext> options) : base(options)
        {
        }

        public DbSet<BankAccount> BankAccounts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                options.UseSqlServer("Data Source=.\\SQLEXPRESS;Initial Catalog=BankingApiTest;Integrated Security=True;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Balance)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                
                entity.HasIndex(e => e.Id).IsUnique();
            });
        }
    }
}
