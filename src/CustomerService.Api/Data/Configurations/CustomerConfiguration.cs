using CustomerService.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerService.Api.Data.Configurations;

public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .ValueGeneratedOnAdd();

        builder.Property(customer => customer.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(customer => customer.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(customer => customer.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(customer => customer.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(customer => customer.BirthDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(customer => customer.CreatedAt)
            .IsRequired();

        builder.Property(customer => customer.UpdatedAt);

        builder.HasIndex(customer => customer.Email)
            .IsUnique();

        builder.HasIndex(customer => customer.DocumentNumber)
            .IsUnique();
    }
}
