using System;

namespace StartConditions
{
    internal sealed class RepairFailureLogState
    {
        private string lastFailure;

        internal bool ShouldLog(string failureSignature)
        {
            failureSignature = failureSignature ?? "unknown repair failure";
            if (string.Equals(lastFailure, failureSignature, StringComparison.Ordinal))
                return false;

            lastFailure = failureSignature;
            return true;
        }

        internal void MarkRecovered()
        {
            lastFailure = null;
        }
    }
}
