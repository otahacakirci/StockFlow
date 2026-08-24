using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Entities;

namespace StockFlow.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_StockMovements_Type_Valid", "[Type] IN (1, 2)");
            tableBuilder.HasCheckConstraint("CK_StockMovements_Quantity_Positive", "[Quantity] > 0");
        });

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Type)
            .HasConversion<int>();

        builder.Property(movement => movement.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(movement => movement.MovementDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(movement => movement.OrderId)
            .HasDatabaseName("IX_StockMovements_OrderId");

        builder.HasIndex(movement => new { movement.ProductId, movement.MovementDate })
            .HasDatabaseName("IX_StockMovements_ProductId_MovementDate");

        builder.HasOne(movement => movement.Order)
            .WithMany(order => order.StockMovements)
            .HasForeignKey(movement => movement.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Product)
            .WithMany(product => product.StockMovements)
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
