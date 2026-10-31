namespace ClipGen.Core.Domain;

public enum JobStatus
{
    Draft,
    AwaitingReview,
    Done,
    Cancelled,
}

public enum ClipSource
{
    Ai,
    Manual,
}

public enum ClipLength
{
    Sec15 = 15,
    Sec30 = 30,
    Sec60 = 60,
    Sec90 = 90,
}

public enum CropMode
{
    Center,
    BlurredBackground,
    ManualOffset,
}
