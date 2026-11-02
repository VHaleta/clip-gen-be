using ClipGen.Be.Host.Contracts.Clips;
using ClipGen.Be.Host.Errors;
using ClipGen.Core.Abstractions.Persistence;
using ClipGen.Core.Domain;

namespace ClipGen.Be.Host.Services;

public sealed class ClipService(IJobRepository jobs, IClipRepository clips, IUnitOfWork unitOfWork)
{
    public async Task<ClipResponse> AddAsync(Guid userId, Guid jobId, AddClipRequest request, CancellationToken ct)
    {
        var job = await jobs.FindWithClipsForUserAsync(jobId, userId, ct) ?? throw new NotFoundException();

        Clip clip;
        try
        {
            clip = job.AddClip(ToRange(request.StartSec, request.EndSec), request.Source, request.Score, request.Reason, request.Hook);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);

        return ClipResponse.From(clip);
    }

    public async Task<ClipResponse> UpdateAsync(Guid userId, Guid clipId, UpdateClipRequest request, CancellationToken ct)
    {
        var clip = await clips.FindWithJobForUserAsync(clipId, userId, ct) ?? throw new NotFoundException();

        try
        {
            clip.ApplyTimecodes(ToRange(request.StartSec, request.EndSec), clip.Job.SourceDuration);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        clip.SetSelected(request.IsSelected);
        clip.SetNarrativeText(request.NarrativeText);

        await unitOfWork.SaveChangesAsync(ct);

        return ClipResponse.From(clip);
    }

    public async Task DeleteAsync(Guid userId, Guid clipId, CancellationToken ct)
    {
        var clip = await clips.FindForUserAsync(clipId, userId, ct) ?? throw new NotFoundException();

        clips.Remove(clip);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static TimeRange ToRange(double startSec, double endSec) =>
        new(TimeSpan.FromSeconds(startSec), TimeSpan.FromSeconds(endSec));
}
