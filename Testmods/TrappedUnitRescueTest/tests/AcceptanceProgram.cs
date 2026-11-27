using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Mono.Cecil;
namespace TrappedUnitRescueTest.Tests
{
    internal static class AcceptanceProgram
    {
        private static int Main(string[] args)
        {
            try
            {
                string game="E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition";
                var directories=new[]{args[0],Path.GetFullPath(Path.Combine(args[0],"../../patchers")),Path.Combine(game,"BepInEx/core"),
                    Path.Combine(game,"BepInEx/plugins/000shcdese"),Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed")};
                AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
                    string name=new AssemblyName(e.Name).Name+".dll";
                    foreach(var directory in directories){string file=Path.Combine(directory,name);if(File.Exists(file))return Assembly.LoadFrom(file);}return null;
                };
                SeriesFixture.Run();VerifyPackage(args[1]);return 0;
            }
            catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        private static void VerifyPackage(string path)
        {
                using(var module=ModuleDefinition.ReadModule(path))
                {
                    // Reject both the prototype namespace and accidentally copied migrated core types.
                    if(module.GetTypes().Any(t=>t.Namespace=="BugfixesAndQoL.TrappedUnitRescue" ||
                        t.Name=="RescueRuntime" || t.Name=="GeneralRescueObserver" || t.Name=="NativeRelocation" ||
                        t.Name=="FixesIntegration" || t.Name=="MountedScannerEarlyProvider"))
                        throw new Exception("Acceptance package contains a second production owner.");
                    if(!module.AssemblyReferences.Any(a=>a.Name=="BugfixesAndQoL"))throw new Exception("Acceptance package does not reference actual BQoL.");
                    if(module.Assembly.CustomAttributes.Any(a=>a.AttributeType.FullName=="APIShared.DependencyBootstrap.EarlyPluginActivationProviderAttribute"))
                        throw new Exception("Acceptance package published another early provider.");
                }
                Console.WriteLine("PASS: production acceptance package has no runtime/scanner owner; actual public automation series contract retained.");
        }
    }
}
