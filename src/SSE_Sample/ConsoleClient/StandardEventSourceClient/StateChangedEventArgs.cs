namespace ConsoleClient.SSE_Reader;

public class StateChangedEventArgs : EventArgs
{
    public ReadyState ReadyState { get; init; }
}