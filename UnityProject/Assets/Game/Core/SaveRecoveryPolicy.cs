using System;

namespace DontGetSidetracked.Core
{
    /// <summary>
    /// Pure decision policy for recovering a save that was interrupted between writing the temp file
    /// and replacing the primary file. File-system access stays in the platform/presentation repository.
    /// </summary>
    public static class SaveRecoveryPolicy
    {
        public static bool ShouldRecoverInterruptedWrite(
            bool primaryValid,
            bool tempValid,
            DateTime primaryWriteUtc,
            DateTime tempWriteUtc)
        {
            if (!tempValid) return false;
            if (!primaryValid) return true;
            return tempWriteUtc > primaryWriteUtc;
        }
    }
}
