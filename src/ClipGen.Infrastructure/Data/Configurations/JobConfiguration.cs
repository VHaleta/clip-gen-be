using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClipGen.Infrastructure.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);
        // Id генерируется в Job.Create (Guid.NewGuid()), а не в БД — без этого EF Core может
        // спутать новую сущность, достигнутую через fixup навигации, с уже существующей строкой.
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(j => j.SourceFileHash).HasMaxLength(64).IsRequired();

        // ProcessingOptions (включая вложенный CropSettings) хранится целиком как JSON-колонка.
        builder.ComplexProperty(j => j.Options, options => options.ToJson());

        // Единственная каскадная ветка: AppUser -> Job -> (Clip, JobTag). Остальное — NoAction (раздел 4.4 tech-spec).
        builder.HasOne(j => j.User)
            .WithMany()
            .HasForeignKey(j => j.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.Clips)
            .WithOne(c => c.Job)
            .HasForeignKey(c => c.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.JobTags)
            .WithOne(jt => jt.Job)
            .HasForeignKey(jt => jt.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tags — производное свойство над JobTags, в БД не хранится отдельно.
        builder.Ignore(j => j.Tags);

        builder.HasIndex(j => j.UserId);
    }
}
