using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClipGen.Infrastructure.Data.Configurations;

public class JobTagConfiguration : IEntityTypeConfiguration<JobTag>
{
    public void Configure(EntityTypeBuilder<JobTag> builder)
    {
        builder.ToTable("JobTags");
        builder.HasKey(jt => new { jt.JobId, jt.TagId });

        // Job -> JobTag (cascade) is configured in JobConfiguration; only the Tag side (NoAction) is here.
        builder.HasOne(jt => jt.Tag)
            .WithMany()
            .HasForeignKey(jt => jt.TagId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
