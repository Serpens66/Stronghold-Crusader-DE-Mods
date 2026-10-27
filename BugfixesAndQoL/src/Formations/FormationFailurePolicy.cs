using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed class FormationNativeContractException : InvalidOperationException
    {
        internal FormationNativeContractException(string message) : base(message) { }
    }

    // A render frame or input gesture is one attempt, regardless of callback count.
    // Callers serialize access and report success only when that attempt completed.
    internal sealed class FormationFailurePolicy
    {
        private string previousCause;
        private long previousAttempt = long.MinValue;
        internal int ConsecutiveFailures { get; private set; }

        internal bool RecordFailure(string stage, Exception error, long attempt, bool critical = false)
        {
            string cause = stage + ":" + error.GetType().FullName + ":" + error.Message;
            if (attempt != previousAttempt)
            {
                ConsecutiveFailures = cause == previousCause ? ConsecutiveFailures + 1 : 1;
                previousAttempt = attempt;
                previousCause = cause;
            }
            return critical || IsCritical(error) || ConsecutiveFailures >= 3;
        }

        internal void RecordSuccess(long attempt)
        {
            // A later callback in the failed attempt cannot make that attempt successful.
            if (attempt == previousAttempt) return;
            ConsecutiveFailures = 0;
            previousCause = null;
        }

        internal static bool IsCritical(Exception error) =>
            error is FormationNativeContractException || error is AccessViolationException || error is SEHException ||
            error is OutOfMemoryException || error is BadImageFormatException ||
            error is MissingMemberException || error is MemberAccessException;
    }
}
