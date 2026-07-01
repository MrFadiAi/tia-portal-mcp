using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

// get_block_content used to re-export and re-inject a block's full source on
// every call (8x for one block observed), bloating context and pushing the model
// past its quality cliff. These tests lock the new behaviour: the first read of
// a block returns full content; a repeat read of UNCHANGED content returns a
// short note instead. A changed block (different hash) re-shows full content.
public class BlockRereadResponseTests
{
    [Fact]
    public void FirstReadReturnsFullContent()
    {
        var result = BlockRereadResponse.Respond("PLC1/FC1", "// the code", alreadyShown: false);
        Assert.Equal("// the code", result);
    }

    [Fact]
    public void RepeatReadReturnsNoteInsteadOfFullContent()
    {
        var result = BlockRereadResponse.Respond("PLC1/FC1", "// the code", alreadyShown: true);
        Assert.NotEqual("// the code", result);
        Assert.Contains("PLC1/FC1", result);
        Assert.Contains("unchanged", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoteTellsCallerTheContentIsAlreadyInConversation()
    {
        var note = BlockRereadResponse.NoteFor("PLC1/FB5");
        Assert.Contains("PLC1/FB5", note);
        Assert.Contains("conversation", note, StringComparison.OrdinalIgnoreCase);
    }
}

public class ShownBlocksCacheTests
{
    public ShownBlocksCacheTests()
    {
        // Static store — reset between tests so they don't leak into each other.
        ShownBlocksCache.Clear();
    }

    [Fact]
    public void NotShownBeforeRemember()
    {
        Assert.False(ShownBlocksCache.WasAlreadyShown("PLC1/FC1", "hash1"));
    }

    [Fact]
    public void ShownAfterRememberWithSameHash()
    {
        ShownBlocksCache.Remember("PLC1/FC1", "hash1");
        Assert.True(ShownBlocksCache.WasAlreadyShown("PLC1/FC1", "hash1"));
    }

    [Fact]
    public void NotShownWhenHashDiffersSoEditedBlockIsReShown()
    {
        ShownBlocksCache.Remember("PLC1/FC1", "hash-before-edit");
        Assert.False(ShownBlocksCache.WasAlreadyShown("PLC1/FC1", "hash-after-edit"));
    }

    [Fact]
    public void DifferentBlocksAreTrackedIndependently()
    {
        ShownBlocksCache.Remember("PLC1/FC1", "h1");
        Assert.False(ShownBlocksCache.WasAlreadyShown("PLC2/FC1", "h1"));
    }

    [Fact]
    public void ContentHashIsDeterministicAndDistinct()
    {
        Assert.Equal(ShownBlocksCache.ContentHash("abc"), ShownBlocksCache.ContentHash("abc"));
        Assert.NotEqual(ShownBlocksCache.ContentHash("abc"), ShownBlocksCache.ContentHash("abd"));
    }

    [Fact]
    public void ClearResets()
    {
        ShownBlocksCache.Remember("PLC1/FC1", "h");
        ShownBlocksCache.Clear();
        Assert.False(ShownBlocksCache.WasAlreadyShown("PLC1/FC1", "h"));
    }
}
