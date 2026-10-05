using System;
using RedBird.Core.Memory.Managed;

namespace ExtraFeatures
{
    public static class KeepBuildRangeTests
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Run()
        {
            CheckWriteOwnership();
            var value = new ManagedValue<int>(-1);
            var policy = new KeepBuildRangeOverride();
            policy.Reconcile(-1, value.GetValue, value.SetValue);
            Check(value.GetValue() == -1, "default must not override");
            policy.Reconcile(100, value.GetValue, value.SetValue);
            Check(value.GetValue() == 100, "100 activates");
            for (int n = 1; n <= 500; n++)
            {
                policy.Reconcile(n, value.GetValue, value.SetValue);
                Check(value.GetValue() == n && value.StackDepth == 1, "integer range without stack growth");
            }
            value.ClearOverrides();
            Check(value.GetValue() == 500, "SetValue survives ClearOverrides at base depth");
            policy.Reconcile(0, value.GetValue, value.SetValue);
            Check(value.GetValue() == -1, "zero restores original after positive changes");
            value.SetValue(85);
            policy.Reconcile(-1, value.GetValue, value.SetValue);
            Check(value.GetValue() == 85, "inactive leaves external value");
            policy.Reconcile(100, value.GetValue, value.SetValue);
            policy.Reconcile(-1, value.GetValue, value.SetValue);
            Check(value.GetValue() == 85, "restore preexisting override");
            policy.Reconcile(100, value.GetValue, value.SetValue);
            value.SetValue(123);
            policy.Reconcile(-1, value.GetValue, value.SetValue);
            Check(value.GetValue() == 123, "foreign replacement preserved on disable");
            policy.Reconcile(100, value.GetValue, value.SetValue);
            value.ResetToOriginal();
            policy.Reconcile(200, value.GetValue, value.SetValue);
            policy.Reconcile(-1, value.GetValue, value.SetValue);
            Check(value.GetValue() == -1, "map reset must not resurrect old override");
            value.Push(77);
            policy.Reconcile(100, value.GetValue, value.SetValue);
            value.ClearOverrides();
            policy.Reconcile(100, value.GetValue, value.SetValue);
            policy.Reconcile(-1, value.GetValue, value.SetValue);
            Check(value.GetValue() == -1 && value.StackDepth == 1, "unload removes overlay stack");
            policy.Reconcile(501, value.GetValue, value.SetValue);
            Check(value.GetValue() == 500, "runtime upper bound");
            policy.Reconcile(-1, value.GetValue, value.SetValue);

            // All legal Keep anchors, all orientations, and the four extremal AIV cells.
            // Max-axis distance on any footprint attains its maximum at a raster corner.
            int maximum = 0;
            int[,] offsets = { { 3, 7 }, { -1, 3 }, { 3, -1 }, { 7, 3 } };
            for (int rotation = 0; rotation < 4; rotation++)
            for (int keepX = 0; keepX <= 93; keepX++)
            for (int keepY = 0; keepY <= 93; keepY++)
            foreach (int x in new[] { 0, 99 })
            foreach (int y in new[] { 0, 99 })
            {
                int distance = Math.Max(Math.Abs(x - keepX - offsets[rotation, 0]),
                    Math.Abs(y - keepY - offsets[rotation, 1]));
                maximum = Math.Max(maximum, distance);
                Check(distance <= 100, "legal AIV footprint exceeds 100");
            }
            Check(maximum == 100, "100 is inclusive and needed at extreme anchors");
            Console.WriteLine("PASS: Keep range ownership, 500 integer values, resets and all AIV rotation bounds.");
        }

        private static void CheckWriteOwnership()
        {
            int current = 85, writes = 0;
            var policy = new KeepBuildRangeOverride();
            Func<int> read = () => current;
            Action<int> write = n => { current = n; writes++; };
            policy.Reconcile(85, read, write);
            policy.Reconcile(0, read, write);
            Check(writes == 0 && current == 85, "matching foreign value must not be owned or written");
            policy.Reconcile(100, read, write);
            for (int n = 0; n < 100; n++) policy.Reconcile(100, read, write);
            Check(writes == 1, "repeated lifecycle/settings refreshes must not write");
            policy.Reconcile(200, read, write);
            policy.Reconcile(200, read, write);
            policy.Reconcile(-1, read, write);
            policy.Reconcile(0, read, write);
            Check(writes == 3 && current == 85, "positive changes retain original predecessor");
            policy.Reconcile(100, read, write);
            current = 200; // An external owner changes the API before our next refresh.
            policy.Reconcile(200, read, write);
            policy.Reconcile(-1, read, write);
            Check(writes == 4 && current == 200, "matching foreign replacement releases stale ownership");
            policy.Reconcile(300, read, write);
            policy.Reconcile(0, read, write);
            Check(writes == 6 && current == 200, "new ownership restores the foreign predecessor");
        }
    }
}
