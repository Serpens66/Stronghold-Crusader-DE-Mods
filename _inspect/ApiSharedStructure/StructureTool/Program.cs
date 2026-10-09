using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
var options=new CSharpParseOptions(LanguageVersion.Preview,preprocessorSymbols:new[]{"API_SHARED_LOBBY_OBSERVER","API_SHARED_INTERNAL_JSON","API_SHARED_INTERNAL_TOOLTIP"});
string Name(MemberDeclarationSyntax m)=>m switch { MethodDeclarationSyntax n=>n.Identifier.Text, ConstructorDeclarationSyntax n=>n.Identifier.Text, BaseTypeDeclarationSyntax n=>n.Identifier.Text, DelegateDeclarationSyntax n=>n.Identifier.Text, PropertyDeclarationSyntax n=>n.Identifier.Text, FieldDeclarationSyntax n=>string.Join(",",n.Declaration.Variables.Select(v=>v.Identifier.Text)), _=>m.Kind().ToString() };
void Write(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,text.Replace("\r\n","\n").Replace("\n","\r\n"),new System.Text.UTF8Encoding(false));}
if(args[0]=="inventory" || args[0]=="split" || args[0]=="namespace") {
 var text=File.ReadAllText(args[1]); var tree=CSharpSyntaxTree.ParseText(text,options); var root=tree.GetRoot();
 if(args[0]=="namespace") {
   var ns=root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().First();
   var names=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[3]))!;
   foreach(var group in ns.Members.GroupBy(m=>names.GetValueOrDefault(Name(m),args[1]))) Write(group.Key,text[..ns.OpenBraceToken.Span.End]+string.Concat(group.Select(m=>m.ToFullString()))+text[ns.CloseBraceToken.FullSpan.Start..]);
   return;
 }
 var type=root.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c=>c.Identifier.Text==args[2]);
 if(args[0]=="inventory") { Console.WriteLine(JsonSerializer.Serialize(type.Members.Select((m,i)=>new{Index=i,Name=Name(m),Kind=m.Kind().ToString(),Start=tree.GetLineSpan(m.Span).StartLinePosition.Line+1,End=tree.GetLineSpan(m.Span).EndLinePosition.Line+1}),new JsonSerializerOptions{WriteIndented=true})); return; }
 var map=JsonSerializer.Deserialize<Dictionary<int,string>>(File.ReadAllText(args[3]))!;
 var prefix=text[..type.OpenBraceToken.Span.End];
 if(!type.Modifiers.Any(SyntaxKind.PartialKeyword)) prefix=prefix.Replace("class "+args[2],"partial class "+args[2]);
 var suffix=text[type.CloseBraceToken.FullSpan.Start..];
 foreach(var group in type.Members.Select((m,i)=>(Member:m,Index:i)).GroupBy(x=>map.GetValueOrDefault(x.Index,args[1]))) {
   Write(group.Key,prefix+string.Concat(group.Select(x=>x.Member.ToFullString()))+suffix);
 }
 return;
}
if(args[0]=="compare" || args[0]=="initializers") {
 var repo=Path.GetFullPath(args[1]);
 string Git(params string[] arguments){var psi=new System.Diagnostics.ProcessStartInfo("git"){WorkingDirectory=repo,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};foreach(var a in arguments)psi.ArgumentList.Add(a);using var p=System.Diagnostics.Process.Start(psi)!;var s=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(p.StandardError.ReadToEnd());return s.Replace("\r\n","\n").Replace("\n","\r\n");}
 IEnumerable<string> Declarations(string text){var root=CSharpSyntaxTree.ParseText(text,options).GetRoot(); if(root.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error))throw new Exception("Syntax error"); foreach(var n in root.DescendantNodes().OfType<MemberDeclarationSyntax>()) {
   var ns=n.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()??"";
   var owners=string.Join(".",n.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(t=>t.Identifier.Text));
   if(n is TypeDeclarationSyntax t){yield return ns+":"+owners+":"+t.Kind()+":"+string.Join(" ",t.Modifiers.Where(m=>!m.IsKind(SyntaxKind.PartialKeyword)))+":"+t.Identifier+":"+t.TypeParameterList+":"+t.BaseList+":"+string.Join(" ",t.ConstraintClauses);}
   else if(n is BaseMethodDeclarationSyntax || n is BasePropertyDeclarationSyntax || n is FieldDeclarationSyntax || n is EventFieldDeclarationSyntax || n is DelegateDeclarationSyntax || n is EnumDeclarationSyntax) yield return ns+":"+owners+":"+n.WithoutTrivia().NormalizeWhitespace().ToFullString();
 }}
 var oldPaths=Git("ls-tree","-r","--name-only",args[2],"--","src","Properties").Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(p=>p.TrimEnd('\r')).Where(p=>p.EndsWith(".cs"));
 if(args[0]=="initializers") {
   var types=new HashSet<string>{"UnitCommandPathRuntime","FormationRuntime","MoatWorkTargetBridge","UnitHudPresentationService"};
   IEnumerable<string> Initializers(string text) {
     var root=CSharpSyntaxTree.ParseText(text,options).GetRoot();
     foreach(var type in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(t=>types.Contains(t.Identifier.Text)))
       foreach(var field in type.Members.OfType<FieldDeclarationSyntax>().Where(f=>!f.Modifiers.Any(SyntaxKind.ConstKeyword)))
         foreach(var variable in field.Declaration.Variables.Where(v=>v.Initializer!=null))
           yield return type.Identifier.Text+":"+variable.Identifier.Text+":"+variable.Initializer!.WithoutTrivia().NormalizeWhitespace();
   }
   var previousParts=oldPaths.OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Initializers(Git("show",args[2]+":"+p)).ToArray()).Where(p=>p.Length>0).ToArray();
   var previous=previousParts.SelectMany(p=>p).ToArray();
   var current=Directory.GetFiles(Path.Combine(repo,"src"),"*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).SelectMany(p=>Initializers(File.ReadAllText(p))).ToArray();
   Write(args[3]+".before.txt",string.Join("\n",previous));
   Write(args[3]+".after.txt",string.Join("\n",current));
   if(!previous.OrderBy(p=>p,StringComparer.Ordinal).SequenceEqual(current.OrderBy(p=>p,StringComparer.Ordinal))) throw new Exception("Field initializers changed");
   foreach(var part in previousParts) {
     foreach(var type in types) {
       var originals=part.Where(p=>p.StartsWith(type+":")).ToArray();
       var members=originals.ToHashSet(StringComparer.Ordinal);
       if(!originals.SequenceEqual(current.Where(members.Contains))) throw new Exception("Initializer ordering changed within an original source part: "+type);
     }
   }
   Write(args[3],$"PASS: {previous.Length} field initializers unchanged; ordering within each of the {previousParts.Length} original source parts is preserved across the four split runtime services. Independent existing partials can have a different relative compilation order after moves.\n");
   Console.WriteLine(File.ReadAllText(args[3]));
   return;
 }
 var before=oldPaths.SelectMany(p=>Declarations(Git("show",args[2]+":"+p.TrimEnd('\r')))).ToList();
 var after=Directory.GetFiles(Path.Combine(repo,"src"),"*.cs",SearchOption.AllDirectories).Concat(Directory.GetFiles(Path.Combine(repo,"Properties"),"*.cs",SearchOption.AllDirectories)).SelectMany(p=>Declarations(File.ReadAllText(p))).ToList();
 // Partial headers can legitimately appear once per implementation part.
 var missing=before.Distinct().Except(after.Distinct()).ToArray(); var added=after.Distinct().Except(before.Distinct()).ToArray();
 Write(args[3],"Missing declarations:\n"+string.Join("\n",missing)+"\nAdded declarations:\n"+string.Join("\n",added));
 Console.WriteLine($"Git declaration comparison: before={before.Count}, after={after.Count}, missing={missing.Length}, added={added.Length}");
 if(missing.Length+added.Length>0)Environment.Exit(1);
}
