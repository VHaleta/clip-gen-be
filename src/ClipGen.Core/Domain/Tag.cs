namespace ClipGen.Core.Domain;

public class Tag
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = "";

    private Tag()
    {
    }

    public static Tag Create(Guid userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));

        return new Tag { Id = Guid.NewGuid(), UserId = userId, Name = name.Trim() };
    }
}
