using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderIntake.Domain.Pedidos;

namespace OrderIntake.Infrastructure.Persistence.Configurations;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Itens");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProdutoId).IsRequired();
        builder.Property(i => i.Quantidade).IsRequired();
    }
}
