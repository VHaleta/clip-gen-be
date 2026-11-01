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
        // Id генерируется в Tag.Create (Guid.NewGuid()) — не в БД (см. JobConfiguration).
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => new { t.UserId, t.Name }).IsUnique();

        // Тег может использоваться другой обработкой того же пользователя — NoAction,
        // удаление пользователя обрабатывается явно вне каскада.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
