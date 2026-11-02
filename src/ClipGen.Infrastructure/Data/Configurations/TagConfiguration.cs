using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClipGen.Infrastructure.Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);
        // Id is generated in Tag.Create (Guid.NewGuid()), not by the DB (see JobConfiguration).
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => new { t.UserId, t.Name }).IsUnique();

        // A tag may be used by another job of the same user, hence NoAction;
        // user deletion is handled explicitly outside the cascade.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
