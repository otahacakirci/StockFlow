using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Entities;

namespace StockFlow.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Orders_Type_Valid", "[Type] IN (1, 2)");
            tableBuilder.HasCheckConstraint("CK_Orders_Status_Valid", "[Status] IN (1, 2, 3)");
            tableBuilder.HasCheckConstraint("CK_Orders_TotalAmount_Positive", "[TotalAmount] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Orders_PartyMatchesType",
                "([Type] = 1 AND [CustomerId] IS NOT NULL AND [SupplierId] IS NULL) OR " +
                "([Type] = 2 AND [SupplierId] IS NOT NULL AND [CustomerId] IS NULL)");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(order => order.Type)
            .HasConversion<int>();

        builder.Property(order => order.Status)
            .HasConversion<int>();

        builder.Property(order => order.OrderDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(order => order.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(order => order.CreatedByUserId)
            .HasMaxLength(450);

        builder.HasIndex(order => order.OrderNumber)
            .IsUnique()
            .HasDatabaseName("UX_Orders_OrderNumber");

        builder.HasIndex(order => new { order.Type, order.Status, order.OrderDate })
            .HasDatabaseName("IX_Orders_Type_Status_OrderDate");

        builder.HasIndex(order => order.CustomerId)
            .HasDatabaseName("IX_Orders_CustomerId");

        builder.HasIndex(order => order.SupplierId)
            .HasDatabaseName("IX_Orders_SupplierId");

        builder.HasIndex(order => order.CreatedByUserId)
            .HasDatabaseName("IX_Orders_CreatedByUserId");

        builder.HasOne(order => order.Customer)
            .WithMany(customer => customer.Orders)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.Supplier)
            .WithMany(supplier => supplier.Orders)
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
