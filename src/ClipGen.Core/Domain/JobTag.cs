namespace ClipGen.Core.Domain;

/// <summary>Явная join-сущность Job&lt;-&gt;Tag (не неявная skip navigation EF Core).</summary>
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
