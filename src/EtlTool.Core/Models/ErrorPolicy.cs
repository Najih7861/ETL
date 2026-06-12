namespace EtlTool.Core.Models;

/// <summary>What to do when a single row fails to transform.</summary>
public enum ErrorPolicy
{
    /// <summary>Log the bad row, skip it, and keep processing.</summary>
    Skip,

    /// <summary>Abort the file (and, for a folder run, the whole batch).</summary>
    Fail,
}

/// <summary>Thrown to abort a file when the error policy is <see cref="ErrorPolicy.Fail"/>.</summary>
public sealed class RowProcessingException : Exception
{
    public RowProcessingException(string message, Exception inner) : base(message, inner) { }
}
