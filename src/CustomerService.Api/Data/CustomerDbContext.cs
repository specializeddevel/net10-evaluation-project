using CustomerService.Api.Data.Configurations;
using CustomerService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Api.Data;

public sealed class CustomerDbContext : DbContext
{
    public CustomerDbContext(
        DbContextOptions<CustomerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(
    ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(
            new CustomerConfiguration());
    }
}
