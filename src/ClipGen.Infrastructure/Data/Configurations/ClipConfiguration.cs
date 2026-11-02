using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClipGen.Infrastructure.Data.Configurations;

public class ClipConfiguration : IEntityTypeConfiguration<Clip>
{
    public void Configure(EntityTypeBuilder<Clip> builder)
    {
        builder.ToTable("Clips");
        builder.HasKey(c => c.Id);
        // Id is generated in Clip.Create (Guid.NewGuid()), not by the DB (see JobConfiguration).
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Reason).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Hook).HasMaxLength(200);
        builder.Property(c => c.NarrativeText).HasMaxLength(4000);

        // TimeRange is a complex type, not JSON: two regular columns.
        builder.ComplexProperty(c => c.Range, range =>
        {
            range.Property(r => r.Start).HasColumnName("RangeStart").IsRequired();
            range.Property(r => r.End).HasColumnName("RangeEnd").IsRequired();
        });

        builder.HasIndex(c => new { c.JobId, c.Index }).IsUnique();

        // The Job -> Clip relationship (including cascade) is configured in JobConfiguration.
    }
}
