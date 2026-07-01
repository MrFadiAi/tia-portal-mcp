using System;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Removes know-how protection from PLC blocks using the official
/// PlcBlockProtectionProvider.Unprotect(password) API. There is no IsProtected query on the
/// provider, so we attempt Unprotect on every block: it succeeds (and removes protection) only
/// when the block was protected AND the password is correct; it throws otherwise (already
/// unprotected, or wrong password). The export path's know-how exception is the authoritative
/// "this block is protected" signal, used by the auto-unlock fallback.
/// </summary>
internal static class KnowHowUnlocker
{
    public static KnowHowUnlockResultInfo Unlock(Project project, string? plcNameFilter, string password)
    {
        var result = new KnowHowUnlockResultInfo { PlcName = plcNameFilter };

        foreach (var (_, plc) in PlcSoftwareFinder.Filter(project, plcNameFilter))
        {
            WalkGroup(plc.BlockGroup, password, result);
        }

        // If Unprotect threw on protected blocks but succeeded nowhere, the password is likely wrong.
        result.PasswordLikelyIncorrect = result.Unprotected == 0 && result.Failed > 0;
        result.Message = result.PasswordLikelyIncorrect
            ? $"Could not unprotect any block with this password (wrong password, or none were protected). Failed: {result.Failed}."
            : $"Removed know-how protection from {result.Unprotected} block(s) ({result.Failed} already unprotected or skipped).";
        return result;
    }

    private static void WalkGroup(PlcBlockGroup group, string password, KnowHowUnlockResultInfo result)
    {
        foreach (PlcBlock block in group.Blocks)
        {
            result.TotalBlocks++;
            // PlcBlockProtectionProvider.Unprotect(SecureString) exists on V16/V18/V21+ — verified
            // by reflecting each version's Siemens.Engineering.dll (V16 exposes the identical
            // Protect/Unprotect(SecureString) pair). The earlier "#if !LEGACY_TIA" guard (claimed
            // "V21+ only") was a wrong assumption that silently no-op'd unlock on V16/V18 and
            // reported a misleading "wrong password". Unprotect succeeds only when the block was
            // protected AND the password is correct; it throws otherwise (already unprotected, or
            // wrong password) and is counted as Failed.
            try
            {
                var provider = block.GetService<PlcBlockProtectionProvider>();
                if (provider is null)
                {
                    result.Failed++;
                    continue;
                }

                provider.Unprotect(KnowHowPasswordStore.ToSecureString(password));
                result.Unprotected++;
            }
            catch (Exception)
            {
                result.Failed++;
            }
        }

        foreach (PlcBlockGroup child in group.Groups)
        {
            WalkGroup(child, password, result);
        }
    }
}
