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
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(j => j.SourceFileHash).HasMaxLength(64).IsRequired();

        // ProcessingOptions (including the nested CropSettings) is stored as a single JSON column.
        builder.ComplexProperty(j => j.Options, options => options.ToJson());

        // The only cascade branch: AppUser -> Job -> (Clip, JobTag). Everything else is NoAction (tech-spec section 4.4).
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

        // Tags is a derived property over JobTags and is not stored separately in the DB.
        builder.Ignore(j => j.Tags);

        builder.HasIndex(j => j.UserId);
    }
}
