using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Entities;

namespace StockFlow.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "[Quantity] > 0");
            tableBuilder.HasCheckConstraint("CK_OrderItems_UnitPrice_Positive", "[UnitPrice] > 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.UnitPrice)
            .HasPrecision(18, 2);

        builder.HasIndex(item => new { item.OrderId, item.ProductId })
            .IsUnique()
            .HasDatabaseName("UX_OrderItems_OrderId_ProductId");

        builder.HasIndex(item => item.ProductId)
            .HasDatabaseName("IX_OrderItems_ProductId");

        builder.HasOne(item => item.Order)
            .WithMany(order => order.Items)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Product)
            .WithMany(product => product.OrderItems)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
