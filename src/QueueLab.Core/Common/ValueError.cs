namespace QueueLab.Core.Common;

/// <summary>
/// Invalid input or an invalid state. This is the C# equivalent of the Python
/// <c>ValueError</c>/<c>TypeError</c>/<c>OverflowError</c> that the original code raised
/// (and that the Flask routes turned into HTTP 400 responses).
/// </summary>
public sealed class ValueError : Exception
{
    public ValueError(string message) : base(message)
    {
    }
}
