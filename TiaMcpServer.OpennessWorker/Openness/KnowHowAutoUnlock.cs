using System;
using Siemens.Engineering.SW.Blocks;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Auto-unlocks a know-how-protected block using the project's cached/env password, so read
/// tools (get_block_content, tag_usage, search_code) transparently pierce protection AFTER the
/// user has provided the password once (via knowhow_unlock). Used as a fallback when a block
/// export throws — that exception is the authoritative "this block is protected" signal.
/// </summary>
internal static class KnowHowAutoUnlock
{
    /// <summary>Remove know-how protection from <paramref name="block"/> if a password is cached
    /// for its project (or set via TIA_KNOWHOW_PASSWORD). Returns true if it was unprotected.</summary>
    public static bool TryUnprotect(PlcBlock block, string? projectPath)
    {
        var password = KnowHowPasswordStore.Resolve(null, projectPath);
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

#if !LEGACY_TIA
        try
        {
            var provider = block.GetService<PlcBlockProtectionProvider>();
            if (provider is null)
            {
                return false;
            }

            provider.Unprotect(KnowHowPasswordStore.ToSecureString(password));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false; // know-how unlock requires the V21+ Openness API
#endif
    }
}
