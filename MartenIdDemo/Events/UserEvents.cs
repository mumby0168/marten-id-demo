namespace MartenIdDemo.Events;

public static class UserEvents
{
    public sealed record SignedUp(Guid Id, string UserId);
}