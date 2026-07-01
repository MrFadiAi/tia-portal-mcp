namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Decides what <c>get_block_content</c> returns, so that re-reading an UNCHANGED
/// block does not re-inject its full source into the conversation. Repeat reads of
/// identical content (8x for one block observed in a single chat) bloated the
/// context and pushed the model past its quality cliff. Siemens-free so it is
/// unit-testable (link-compiled into the test project).
/// </summary>
public static class BlockRereadResponse
{
    /// <summary>
    /// Return the full content on a first read (or after the block changed), or a
    /// compact note when the same content was already returned this session.
    /// </summary>
    public static string Respond(string blockPath, string fullContent, bool alreadyShown)
        => alreadyShown ? NoteFor(blockPath) : fullContent;

    /// <summary>The compact note returned instead of re-injecting the full source.</summary>
    public static string NoteFor(string blockPath) =>
        "[unchanged] Block '" + blockPath + "' is identical to its previous read — the full source "
        + "is already in the conversation above. Re-display skipped to conserve context. If you need "
        + "the code again, use search_code or re-ask explicitly.";
}
