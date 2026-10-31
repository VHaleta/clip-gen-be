using Microsoft.AspNetCore.Identity;

namespace ClipGen.Core.Domain;

public class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
