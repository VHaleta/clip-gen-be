using ClipGen.Be.Host.Contracts.Jobs;
using ClipGen.Be.Host.Errors;
using ClipGen.Core.Abstractions.Persistence;
using ClipGen.Core.Domain;

namespace ClipGen.Be.Host.Services;

public sealed class JobService(IJobRepository jobs, ITagRepository tags, IUnitOfWork unitOfWork)
{
    public async Task<JobResponse> CreateAsync(Guid userId, CreateJobRequest request, CancellationToken ct)
    {
        Job job;
        try
        {
            job = Job.Create(
                userId,
                request.OriginalFileName,
                request.SourceFileHash,
                TimeSpan.FromSeconds(request.SourceDurationSeconds),
                (request.Options ?? new ProcessingOptionsDto()).ToDomain());
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        jobs.Add(job);
        await unitOfWork.SaveChangesAsync(ct);

        return JobResponse.From(job);
    }

    public async Task<IReadOnlyList<JobListItem>> ListAsync(Guid userId, CancellationToken ct)
    {
        var entries = await jobs.ListForUserAsync(userId, ct);
        return entries
            .Select(e => new JobListItem(e.Id, e.OriginalFileName, e.Status.ToString(), e.ClipCount, e.CreatedAt))
            .ToList();
    }

    public async Task<JobResponse> GetAsync(Guid userId, Guid jobId, CancellationToken ct) =>
        JobResponse.From(await LoadDetailsAsync(userId, jobId, ct));

    public async Task<JobResponse> UpdateOptionsAsync(Guid userId, Guid jobId, ProcessingOptionsDto request, CancellationToken ct)
    {
        var job = await LoadDetailsAsync(userId, jobId, ct);

        job.UpdateOptions(request.ToDomain());
        await unitOfWork.SaveChangesAsync(ct);

        return JobResponse.From(job);
    }

    public async Task<JobResponse> UpdateTagsAsync(Guid userId, Guid jobId, UpdateJobTagsRequest request, CancellationToken ct)
    {
        var job = await LoadDetailsAsync(userId, jobId, ct);

        var names = request.TagNames
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTags = await tags.FindByNamesForUserAsync(userId, names, ct);

        var newTags = names
            .Except(existingTags.Select(t => t.Name), StringComparer.OrdinalIgnoreCase)
            .Select(n => Tag.Create(userId, n))
            .ToList();

        if (newTags.Count > 0)
            tags.AddRange(newTags);

        job.ReplaceTags(existingTags.Concat(newTags));
        await unitOfWork.SaveChangesAsync(ct);

        return JobResponse.From(job);
    }

    public async Task DeleteAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var job = await jobs.FindForUserAsync(jobId, userId, ct) ?? throw new NotFoundException();

        jobs.Remove(job);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Job> LoadDetailsAsync(Guid userId, Guid jobId, CancellationToken ct) =>
        await jobs.FindWithDetailsForUserAsync(jobId, userId, ct) ?? throw new NotFoundException();
}
