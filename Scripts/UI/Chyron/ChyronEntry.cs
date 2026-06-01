namespace UI.Chyron;

public struct ChyronEntry
{
    public string Message;           // Max 100 chars, truncated on creation
    public int DisplaysRemaining;    // Decrement each display cycle; remove at 0
    public int DisplayLength;        // Seconds to show
    public ChyronPriority Priority;

    public ChyronEntry(string message, ChyronPriority priority, int displayLength, int displays)
    {
        Message = message.Length > 100 ? message[..100] : message;
        Priority = priority;
        DisplayLength = displayLength;
        DisplaysRemaining = displays;
    }
}