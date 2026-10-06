using System;
using System.Threading.Tasks;

namespace SharedTests
{
    internal static class CaptureRefreshTests
    {
        internal static int Run()
        {
            int count = 0;
            var request = new Shared.DeferredSnapshotRefreshRequest();
            Action<bool, string> check = (value, message) =>
            { count++; if (!value) throw new Exception(message); };
            check(!request.Consume(), "No refresh before an event");
            request.Request(false, true);
            check(!request.Consume(), "Post outside a map is ignored");
            request.Request(true, false);
            check(!request.Consume(), "Pre cannot publish unfinished capture state");
            Parallel.For(0, 1000, _ => request.Request(true, true));
            check(request.Consume(), "Concurrent Posts request a deferred refresh");
            check(!request.Consume(), "Repeated Posts coalesce into one refresh");
            request.Request(true, true);
            request.Reset();
            check(!request.Consume(), "Map end discards pending work");
            request.Request(false, true);
            check(!request.Consume(), "Inactive map cannot repopulate pending work");
            request.Request(true, true);
            check(request.Consume(), "A new map accepts a fresh Post");
            request.Request(true, true);
            request.Request(true, false);
            check(request.Consume(), "Pre does not erase an already requested Post");
            check(!request.Consume(), "Consumption clears the request");
            return count;
        }
    }
}
