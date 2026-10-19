using System;
using System.IO;

namespace AutoLoadSave
{
    internal static class LoadRequestTests
    {
        private static int count;
        private static void Check(bool value, string name)
        {
            if (!value) throw new Exception(name);
            count++;
        }
        public static int Main(string[] args)
        {
            string root = args[0];
            string save = Path.Combine(root, "fixture ü.sav");
            File.WriteAllText(save, "path fixture only; never passed to the game");
            Check(!new LoadRequest("  ").TryBegin(true), "empty config disables loading");
            var request = new LoadRequest("fixture ü.sav");
            Check(!request.TryBegin(false) && !request.Consumed, "wait for menu readiness");
            Check(request.TryBegin(true), "first ready call starts loading");
            Check(request.Consumed && !request.TryBegin(true), "reentry and later menu visits cannot reload");
            Check(request.ResolvePath(root) == save, "relative unicode filename");
            Check(new LoadRequest(save).ResolvePath(root) == save, "absolute filename");
            var missing = new LoadRequest("missing.sav");
            Check(missing.TryBegin(true), "consume attempt before failure");
            try { missing.ResolvePath(root); throw new Exception("missing file accepted"); }
            catch (FileNotFoundException) { count++; }
            Check(!missing.TryBegin(true), "failed loads cannot retry");
            foreach (string unsupported in new[] { "fixture.msv", "fixture.map", "fixture" })
            {
                try { new LoadRequest(unsupported).ResolvePath(root); throw new Exception("unsupported type accepted"); }
                catch (InvalidDataException) { count++; }
            }
            Console.WriteLine("PASS: " + count + " load-request checks (real production class).");
            return 0;
        }
    }
}
