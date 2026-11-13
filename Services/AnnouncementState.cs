namespace PerfectRide.Services;

public class AnnouncementState
{
    public string Message { get; private set; } = "";
    public bool IsAnimated { get; private set; }
    public string MessageType { get; private set; } = "info";

    public event Action? OnChange;

    public void SetAnnouncement(string message, bool isAnimated, string messageType)
    {
        Message = message;
        IsAnimated = isAnimated;
        MessageType = messageType;

        OnChange?.Invoke();
    }
}