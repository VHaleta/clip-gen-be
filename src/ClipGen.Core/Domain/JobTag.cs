namespace ClipGen.Core.Domain;

/// <summary>Explicit Job&lt;-&gt;Tag join entity (not an implicit EF Core skip navigation).</summary>
public class JobTag
{
    public Guid JobId { get; private set; }
    public Guid TagId { get; private set; }

    public Job Job { get; private set; } = default!;
    public Tag Tag { get; private set; } = default!;

    private JobTag()
    {
    }

    public JobTag(Guid jobId, Guid tagId, Tag tag)
    {
        JobId = jobId;
        TagId = tagId;
        Tag = tag;
    }
}
