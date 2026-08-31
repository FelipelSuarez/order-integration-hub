using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderIntake.Infrastructure.Sagas;

namespace OrderIntake.Infrastructure.Persistence.Configurations;

public sealed class PedidoSagaStateConfiguration : IEntityTypeConfiguration<PedidoSagaState>
{
    public void Configure(EntityTypeBuilder<PedidoSagaState> builder)
    {
        builder.ToTable("PedidoSagaState");
        builder.HasKey(s => s.CorrelationId);

        builder.Property(s => s.CurrentState).HasMaxLength(64).IsRequired();
        builder.Property(s => s.PrimeiraTentativaEm);

        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
