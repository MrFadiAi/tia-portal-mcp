namespace TiaMcpServer.Contracts;

public class KnowHowUnlockResultInfo
{
    public string? PlcName { get; set; }

    /// <summary>Total blocks scanned.</summary>
    public int TotalBlocks { get; set; }

    /// <summary>Blocks that were know-how-protected and are now unprotected (correct password).</summary>
    public int Unprotected { get; set; }

    /// <summary>Blocks Unprotect threw on (already unprotected, or wrong password).</summary>
    public int Failed { get; set; }

    /// <summary>True if the password was saved for this project (future calls reuse it without asking).</summary>
    public bool PasswordCached { get; set; }

    /// <summary>True when no password was available — the caller must ask the user for it.</summary>
    public bool PasswordRequired { get; set; }

    /// <summary>Heuristic: Unprotect failed on protected blocks (password likely wrong).</summary>
    public bool PasswordLikelyIncorrect { get; set; }

    public string Message { get; set; } = string.Empty;
}
