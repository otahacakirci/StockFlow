using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Entities;

namespace StockFlow.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(supplier => supplier.Email)
            .HasMaxLength(256);

        builder.Property(supplier => supplier.Phone)
            .HasMaxLength(32);

        builder.Property(supplier => supplier.Address)
            .HasMaxLength(500);
    }
}
