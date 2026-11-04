<#
.SYNOPSIS
Replaces line-number log site keys ("<Mod>/src/<File>.cs:<Line>") with stable semantic keys.

.DESCRIPTION
Site keys passed to Shared.DebugLogHelper.LogErrorLimited/LogWarningLimited/LogDebugLimited (and
mod-local wrappers such as LogDebug(site, ...)) are rate-limit identities and, for errors and
warnings, the "feature=" identity of the RuntimeHealthLedger. A fixed line number goes stale as
soon as code moves and then claims a position that is no longer true.

The new key is "<Mod>/<FileStem>/<Member>[.<purpose>][-<n>]":
- <FileStem>  name of the file that contains the call (without .cs, partial suffixes kept);
- <Member>    enclosing method/property/constructor ("Constructor"), prefixed by nested types;
- <purpose>   short slug from the first message literal, added only when the member holds
              more than one converted site;
- -<n>        ordinal suffix, only when a key would otherwise collide inside the mod.
Keys never contain line numbers, so moving code does not change them. Existing non-line keys
are left untouched and reserved, so new keys never collide with them.

Without -Apply the script is a dry run: it scans, plans and reports, and writes nothing.
With -Apply all files are written only if every candidate file was parsed successfully;
encoding (BOM) and line endings of each file are preserved and verified after writing.

.PARAMETER ModDirectory
Mod folder (absolute, relative to the current directory or to the workspace root).

.PARAMETER Apply
Write the planned changes. Omit for a dry run.

.PARAMETER ReportPath
Optional report file: .json for JSON, anything else for tab-separated values (CRLF, UTF-8).

.PARAMETER SiteMethod
Callees whose literal site argument is converted. Names ending in "Limited" take the site as
second argument (after the log source); all other names take it as first argument.

.PARAMETER ModName
Key prefix; defaults to the leaf name of ModDirectory.

.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Shared\Tools\Diagnostics\Convert-LogSiteKeys.ps1 -ModDirectory UnitLimit
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Shared\Tools\Diagnostics\Convert-LogSiteKeys.ps1 -ModDirectory UnitLimit -Apply -ReportPath .work\a\logsites\UnitLimit.tsv
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ModDirectory,
    [switch]$Apply,
    [string]$ReportPath = '',
    [string[]]$SiteMethod = @('LogErrorLimited', 'LogWarningLimited', 'LogDebugLimited', 'LogDebug'),
    [string]$ModName = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

# C# 5 only: Windows PowerShell 5.1 compiles Add-Type sources with the legacy compiler.
$scannerSource = @'
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SerpLogSiteKeys.V1
{
    public sealed class Literal
    {
        public int Start;
        public int End;
        public string Content;
    }

    public sealed class Site
    {
        public string File;
        public int Start;
        public int End;
        public int Line;
        public string OldKey;
        public string Callee;
        public string Member;
        public string TypeChain;
        public string Message;
        public bool MemberResolved;
        public string NewKey;
    }

    public sealed class FileScan
    {
        public string File;
        public string FileStem;
        public bool Ok = true;
        public string Error;
        public List<Site> Sites = new List<Site>();
        public List<string> OtherKeys = new List<string>();
    }

    internal sealed class Scope
    {
        internal int Kind; // 0 root, 1 namespace, 2 type, 3 member, 4 block
        internal string Name;
        internal int HeaderStart;
        internal bool ExpressionBrace;
    }

    public static class Scanner
    {
        private static readonly Regex TypeHeader = new Regex(@"\b(?:class|struct|interface|enum|record)\s+@?([A-Za-z_][A-Za-z0-9_]*)");
        private static readonly Regex NamespaceHeader = new Regex(@"\bnamespace\b");
        private static readonly Regex TrailingIdentifier = new Regex(@"@?([A-Za-z_][A-Za-z0-9_]*)\s*$");
        private static readonly Regex OperatorWord = new Regex(@"\boperator\b");

        public static FileScan Scan(string file, string fileStem, string text, string oldKeyPattern, string[] siteMethods)
        {
            var result = new FileScan { File = file, FileStem = fileStem };
            var oldKey = new Regex(oldKeyPattern);
            var methods = new HashSet<string>(siteMethods, StringComparer.Ordinal);
            char[] mask = text.ToCharArray();
            var literals = new List<Literal>();
            try { Lex(text, mask, literals); }
            catch (Exception ex) { result.Ok = false; result.Error = "lexer: " + ex.Message; return result; }
            string code = new string(mask);

            var candidates = new List<Site>();
            foreach (Literal literal in literals)
            {
                string callee; int argument;
                if (!TryGetCall(code, literal.Start, out callee, out argument) || !methods.Contains(callee)) continue;
                int siteArgument = callee.EndsWith("Limited", StringComparison.Ordinal) ? 1 : 0;
                if (argument != siteArgument) continue;
                string raw = text.Substring(literal.Start, literal.End - literal.Start);
                bool plain = raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"' && raw.IndexOf('\\') < 0;
                string value = plain ? raw.Substring(1, raw.Length - 2) : null;
                if (value == null || !oldKey.IsMatch(value))
                {
                    if (value != null) result.OtherKeys.Add(value);
                    continue;
                }
                candidates.Add(new Site
                {
                    File = file, Start = literal.Start, End = literal.End, OldKey = value, Callee = callee,
                    Line = LineOf(text, literal.Start), Message = MessageAfter(code, literals, literal)
                });
            }
            if (candidates.Count == 0) return result;
            try { ResolveMembers(code, candidates); }
            catch (Exception ex) { result.Ok = false; result.Error = "scopes: " + ex.Message; return result; }
            result.Sites = candidates;
            return result;
        }

        private static int LineOf(string text, int position)
        {
            int line = 1;
            for (int i = 0; i < position; i++) if (text[i] == '\n') line++;
            return line;
        }

        // Blank comments, preprocessor lines and literals in mask; record literal spans.
        private static void Lex(string s, char[] mask, List<Literal> literals)
        {
            int i = 0, n = s.Length; bool lineStart = true;
            while (i < n)
            {
                char c = s[i];
                if (c == '\n') { lineStart = true; i++; continue; }
                if (lineStart && c == '#')
                {
                    int j = i; while (j < n && s[j] != '\n') j++;
                    Blank(mask, i, j); i = j; continue;
                }
                if (!char.IsWhiteSpace(c)) lineStart = false;
                if (c == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    int j = i; while (j < n && s[j] != '\n') j++;
                    Blank(mask, i, j); i = j; continue;
                }
                if (c == '/' && i + 1 < n && s[i + 1] == '*')
                {
                    int j = s.IndexOf("*/", i + 2, StringComparison.Ordinal); j = j < 0 ? n : j + 2;
                    Blank(mask, i, j); i = j; continue;
                }
                if (c == '\'') { int j = SkipChar(s, i); Blank(mask, i, j); i = j; continue; }
                if (IsStringStart(s, i) && (c == '"' || i == 0 || !IsIdentifierChar(s[i - 1])))
                {
                    var content = new StringBuilder();
                    int j = ReadString(s, i, content);
                    literals.Add(new Literal { Start = i, End = j, Content = content.ToString() });
                    Blank(mask, i, j); i = j; continue;
                }
                i++;
            }
        }

        private static bool IsIdentifierChar(char c) { return char.IsLetterOrDigit(c) || c == '_'; }

        private static void Blank(char[] mask, int from, int to)
        {
            for (int k = from; k < to && k < mask.Length; k++) if (mask[k] != '\n' && mask[k] != '\r') mask[k] = ' ';
        }

        private static int SkipChar(string s, int i)
        {
            int n = s.Length, j = i + 1;
            if (j < n && s[j] == '\\') j += 2; else j += 1;
            int limit = Math.Min(n, i + 12);
            while (j < limit && s[j] != '\'') j++;
            return Math.Min(n, j + 1);
        }

        private static bool IsStringStart(string s, int i)
        {
            int n = s.Length, j = i;
            while (j < n && s[j] == '$') j++;
            if (j < n && s[j] == '@') j++;
            while (j < n && s[j] == '$') j++;
            return j < n && s[j] == '"' && (j == i || s[i] == '$' || s[i] == '@');
        }

        // Returns the end of the literal at i; appends its text without interpolation holes.
        private static int ReadString(string s, int i, StringBuilder content)
        {
            int n = s.Length, j = i, dollars = 0; bool verbatim = false;
            while (j < n && (s[j] == '$' || s[j] == '@')) { if (s[j] == '$') dollars++; else verbatim = true; j++; }
            int quotes = 0; while (j + quotes < n && s[j + quotes] == '"') quotes++;
            if (quotes >= 3)
            {
                int k = j + quotes;
                while (k < n)
                {
                    int run = 0; while (k + run < n && s[k + run] == '"') run++;
                    if (run >= quotes) return k + run;
                    content.Append(s[k]); k += Math.Max(1, run);
                }
                throw new FormatException("unterminated raw string at " + i);
            }
            j++;
            while (j < n)
            {
                char c = s[j];
                if (verbatim)
                {
                    if (c == '"') { if (j + 1 < n && s[j + 1] == '"') { content.Append('"'); j += 2; continue; } return j + 1; }
                }
                else
                {
                    if (c == '\\') { content.Append(' '); j += 2; continue; }
                    if (c == '"') return j + 1;
                    if (c == '\n') throw new FormatException("unterminated string at " + i);
                }
                if (dollars > 0 && c == '{')
                {
                    if (j + 1 < n && s[j + 1] == '{') { content.Append('{'); j += 2; continue; }
                    j = SkipHole(s, j + 1); content.Append(' '); continue;
                }
                if (dollars > 0 && c == '}' && j + 1 < n && s[j + 1] == '}') { content.Append('}'); j += 2; continue; }
                content.Append(c); j++;
            }
            throw new FormatException("unterminated string at " + i);
        }

        private static int SkipHole(string s, int j)
        {
            int n = s.Length, depth = 0;
            while (j < n)
            {
                char c = s[j];
                if (c == '\'') { j = SkipChar(s, j); continue; }
                if (IsStringStart(s, j)) { j = ReadString(s, j, new StringBuilder()); continue; }
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == '}') { if (depth == 0) return j + 1; depth--; }
                j++;
            }
            throw new FormatException("unterminated interpolation hole");
        }

        // Innermost unmatched '(' before position: callee identifier and zero-based argument index.
        private static bool TryGetCall(string code, int position, out string callee, out int argument)
        {
            callee = null; argument = 0; int depth = 0, open = -1;
            for (int j = position - 1; j >= 0; j--)
            {
                char c = code[j];
                if (c == ')' || c == ']' || c == '}') depth++;
                else if (c == '(' || c == '[' || c == '{')
                {
                    if (depth > 0) { depth--; continue; }
                    if (c == '(') open = j;
                    break;
                }
                else if (c == ',' && depth == 0) argument++;
                else if (c == ';' && depth == 0) break;
            }
            if (open < 0) return false;
            int end = open - 1;
            while (end >= 0 && char.IsWhiteSpace(code[end])) end--;
            int start = end;
            while (start >= 0 && IsIdentifierChar(code[start])) start--;
            if (end < 0 || start == end) return false;
            callee = code.Substring(start + 1, end - start);
            return true;
        }

        private static string MessageAfter(string code, List<Literal> literals, Literal site)
        {
            int depth = 0, close = code.Length;
            for (int j = site.End; j < code.Length; j++)
            {
                char c = code[j];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') { if (depth == 0) { close = j; break; } depth--; }
                else if (c == ';' && depth == 0) { close = j; break; }
            }
            foreach (Literal literal in literals)
                if (literal.Start >= site.End && literal.Start < close) return literal.Content;
            return string.Empty;
        }

        private static void ResolveMembers(string code, List<Site> sites)
        {
            var stack = new List<Scope>(); stack.Add(new Scope { Kind = 0, HeaderStart = 0 });
            int next = 0;
            for (int i = 0; i <= code.Length; i++)
            {
                while (next < sites.Count && sites[next].Start == i) { Record(code, stack, i, sites[next]); next++; }
                if (i == code.Length) break;
                char c = code[i];
                Scope top = stack[stack.Count - 1];
                if (c == '{')
                {
                    var scope = new Scope { HeaderStart = i + 1 };
                    if (top.Kind <= 2)
                    {
                        string header = StripAttributes(code.Substring(top.HeaderStart, i - top.HeaderStart));
                        Match type;
                        if (HasTopLevelAssignment(header)) { scope.Kind = 3; scope.ExpressionBrace = true; scope.Name = ExtractName(header); }
                        else if (NamespaceHeader.IsMatch(header)) scope.Kind = 1;
                        else if ((type = TypeHeader.Match(header)).Success) { scope.Kind = 2; scope.Name = type.Groups[1].Value; }
                        else { scope.Kind = 3; scope.Name = ExtractName(header); }
                    }
                    else scope.Kind = 4;
                    stack.Add(scope);
                }
                else if (c == '}')
                {
                    if (stack.Count == 1) throw new FormatException("unbalanced '}' at " + i);
                    Scope closed = top; stack.RemoveAt(stack.Count - 1);
                    // A brace inside an expression-bodied member or initializer does not end the declaration.
                    if (!closed.ExpressionBrace) stack[stack.Count - 1].HeaderStart = i + 1;
                }
                else if (c == ';') top.HeaderStart = i + 1;
            }
            if (stack.Count != 1) throw new FormatException("unbalanced '{' at end of file");
            if (next != sites.Count) throw new FormatException("site positions were not reached");
        }

        private static void Record(string code, List<Scope> stack, int position, Site site)
        {
            var types = new List<string>(); string member = null;
            Scope top = stack[stack.Count - 1];
            if (top.Kind == 2) member = ExtractName(StripAttributes(code.Substring(top.HeaderStart, position - top.HeaderStart)));
            for (int k = stack.Count - 1; k >= 0; k--)
            {
                Scope scope = stack[k];
                if (scope.Kind == 3 && member == null) member = scope.Name;
                if (scope.Kind == 2) types.Insert(0, scope.Name);
            }
            if (member != null && types.Count > 0 && member == types[types.Count - 1]) member = "Constructor";
            site.MemberResolved = member != null;
            site.Member = member ?? "Unknown";
            site.TypeChain = string.Join(".", types.ToArray());
        }

        private static string StripAttributes(string header)
        {
            string h = header.TrimStart();
            while (h.Length > 0 && h[0] == '[')
            {
                int depth = 0, j = 0;
                for (; j < h.Length; j++)
                {
                    if (h[j] == '[' || h[j] == '(') depth++;
                    else if (h[j] == ']' || h[j] == ')') { depth--; if (depth == 0 && h[j] == ']') break; }
                }
                if (j >= h.Length) break;
                h = h.Substring(j + 1).TrimStart();
            }
            return h;
        }

        private static int FirstTopLevel(string h, bool stopAtParen)
        {
            int depth = 0;
            for (int j = 0; j < h.Length; j++)
            {
                char c = h[j];
                if (c == '(' && depth == 0 && stopAtParen) return j;
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == '=' && depth == 0)
                {
                    char before = j > 0 ? h[j - 1] : ' ';
                    char after = j + 1 < h.Length ? h[j + 1] : ' ';
                    if (after == '=' || before == '=' || before == '!' || before == '<' || before == '>') continue;
                    return j;
                }
            }
            return -1;
        }

        private static bool HasTopLevelAssignment(string h) { return FirstTopLevel(h, false) >= 0; }

        private static string ExtractName(string header)
        {
            string h = header;
            int cut = FirstTopLevel(h, true);
            string prefix = (cut < 0 ? h : h.Substring(0, cut)).TrimEnd();
            if (OperatorWord.IsMatch(prefix)) return "Operator";
            prefix = StripTrailingGroup(prefix, '<', '>');
            prefix = StripTrailingGroup(prefix, '[', ']');
            Match match = TrailingIdentifier.Match(prefix);
            if (!match.Success) return null;
            string name = match.Groups[1].Value;
            string before = prefix.Substring(0, match.Index).TrimEnd();
            if (before.EndsWith("~", StringComparison.Ordinal)) return "Finalizer";
            if (name == "this") return "Indexer";
            return name;
        }

        private static string StripTrailingGroup(string s, char open, char close)
        {
            s = s.TrimEnd();
            if (s.Length == 0 || s[s.Length - 1] != close) return s;
            int depth = 0;
            for (int j = s.Length - 1; j >= 0; j--)
            {
                if (s[j] == close) depth++;
                else if (s[j] == open) { depth--; if (depth == 0) return s.Substring(0, j).TrimEnd(); }
            }
            return s;
        }
    }

    public static class Planner
    {
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "an", "the", "of", "to", "for", "and", "or", "in", "on", "at", "with", "by", "from", "into",
            "as", "is", "was", "are", "were", "be", "been", "has", "have", "had", "its", "it", "this", "that"
        };
        private static readonly Regex CamelBoundary = new Regex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");
        private static readonly Regex NonWord = new Regex(@"[^a-z0-9]+");

        private const int SlugWords = 4, MaximumSlugLength = 48;

        public static List<string> Words(string message, string modName)
        {
            var skip = new HashSet<string>(StopWords, StringComparer.Ordinal);
            skip.Add(modName.ToLowerInvariant());
            foreach (string part in CamelBoundary.Split(modName)) skip.Add(part.ToLowerInvariant());
            skip.Add("qol"); skip.Add("bugfixes");
            var words = new List<string>();
            foreach (string word in NonWord.Split(CamelBoundary.Replace(message ?? string.Empty, " ").ToLowerInvariant()))
                // Single letters come from contractions and possessives ("workshop's").
                if (word.Length > 0 && !skip.Contains(word) && !(word.Length == 1 && char.IsLetter(word[0]))) words.Add(word);
            return words;
        }

        private static string Join(List<string> words, int from, int count)
        {
            string slug = string.Join("-", words.GetRange(from, Math.Max(0, Math.Min(count, words.Count - from))).ToArray());
            if (slug.Length <= MaximumSlugLength) return slug;
            int cut = slug.LastIndexOf('-', MaximumSlugLength);
            return cut > 0 ? slug.Substring(0, cut) : slug.Substring(0, MaximumSlugLength);
        }

        // Several sites in one member: drop the words all their messages start with, then use the
        // shortest word prefix (at least SlugWords) that differs from the other sites of the member.
        private static List<string> GroupSlugs(List<List<string>> messages)
        {
            int common = 0, shortest = int.MaxValue;
            foreach (List<string> words in messages) shortest = Math.Min(shortest, words.Count);
            if (messages.Count > 1)
                while (common < shortest - 1 && messages.TrueForAll(w => w[common] == messages[0][common])) common++;
            var slugs = new List<string>();
            for (int i = 0; i < messages.Count; i++)
            {
                List<string> words = messages[i];
                int length = Math.Min(SlugWords, words.Count - common);
                while (common + length < words.Count)
                {
                    string candidate = Join(words, common, length);
                    bool unique = true;
                    for (int j = 0; j < messages.Count && unique; j++)
                        if (j != i && Join(messages[j], common, length) == candidate) unique = false;
                    if (unique) break;
                    length++;
                }
                slugs.Add(Join(words, common, length));
            }
            return slugs;
        }

        // Deterministic: files in ordinal path order, sites in source order.
        public static void AssignKeys(string modName, List<FileScan> files)
        {
            files.Sort((a, b) => string.CompareOrdinal(a.File, b.File));
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (FileScan file in files) foreach (string key in file.OtherKeys) used.Add(key);
            foreach (FileScan file in files)
            {
                string primary = file.FileStem.Split('.')[0];
                var groups = new Dictionary<string, List<Site>>(StringComparer.Ordinal);
                foreach (Site site in file.Sites)
                {
                    string purpose = Purpose(site, primary);
                    List<Site> members;
                    if (!groups.TryGetValue(purpose, out members)) groups[purpose] = members = new List<Site>();
                    members.Add(site);
                }
                var slugs = new Dictionary<Site, string>();
                foreach (List<Site> group in groups.Values)
                {
                    if (group.Count == 1 && group[0].MemberResolved) continue;
                    var messages = group.ConvertAll(s => Words(s.Message, modName));
                    List<string> groupSlugs = GroupSlugs(messages);
                    for (int i = 0; i < group.Count; i++) slugs[group[i]] = groupSlugs[i];
                }
                foreach (Site site in file.Sites)
                {
                    string purpose = Purpose(site, primary);
                    string slug;
                    if (slugs.TryGetValue(site, out slug) && slug.Length > 0) purpose += "." + slug;
                    string candidate = modName + "/" + file.FileStem + "/" + purpose;
                    string key = candidate;
                    for (int n = 2; used.Contains(key); n++) key = candidate + "-" + n;
                    used.Add(key);
                    site.NewKey = key;
                }
            }
        }

        private static string Purpose(Site site, string primaryType)
        {
            var parts = new List<string>(site.TypeChain.Length == 0 ? new string[0] : site.TypeChain.Split('.'));
            if (parts.Count > 0 && parts[0] == primaryType) parts.RemoveAt(0);
            parts.Add(site.Member);
            return string.Join(".", parts.ToArray());
        }
    }
}
'@

if (-not ('SerpLogSiteKeys.V1.Scanner' -as [type])) { Add-Type -TypeDefinition $scannerSource -Language CSharp }

function Resolve-ModDirectory([string]$Path) {
    $workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
    $candidates = @($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
    if (-not [IO.Path]::IsPathRooted($Path)) { $candidates += [IO.Path]::GetFullPath((Join-Path $workspace $Path)) }
    foreach ($candidate in $candidates) {
        if ([IO.Directory]::Exists($candidate)) { return [IO.Path]::GetFullPath($candidate).TrimEnd('\') }
    }
    throw "Mod directory not found: $Path"
}

function Read-SourceFile([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = 0; if ($bom) { $offset = 3 }
    $encoding = New-Object Text.UTF8Encoding($false, $true)
    [pscustomobject]@{ Text = $encoding.GetString($bytes, $offset, $bytes.Length - $offset); Bom = $bom }
}

function Get-LineEndingCounts([string]$Text) {
    $crlf = ([regex]::Matches($Text, "`r`n")).Count
    $lf = ([regex]::Matches($Text, "(?<!`r)`n")).Count
    "$crlf/$lf"
}

$modRoot = Resolve-ModDirectory $ModDirectory
if (-not $ModName) { $ModName = Split-Path -Leaf $modRoot }
if ($ModName -notmatch '^[A-Za-z0-9_.-]+$') { throw "Unsupported mod name for keys: $ModName" }
$oldKeyPattern = '^[A-Za-z0-9_.-]+/src/[A-Za-z0-9_./-]+\.cs:[0-9]+$'
$excludedSegment = '^(bin|obj|BepInEx|tests|.+\.Tests)$'

$files = @(Get-ChildItem -LiteralPath $modRoot -Recurse -File -Filter '*.cs' | Where-Object {
        $relative = $_.FullName.Substring($modRoot.Length + 1)
        @($relative.Split('\') | Select-Object -SkipLast 1 | Where-Object { $_ -match $excludedSegment }).Count -eq 0
    } | Sort-Object FullName)

$scans = New-Object 'System.Collections.Generic.List[SerpLogSiteKeys.V1.FileScan]'
$sources = @{}
foreach ($file in $files) {
    $relative = $file.FullName.Substring($modRoot.Length + 1).Replace('\', '/')
    $source = Read-SourceFile $file.FullName
    $sources[$relative] = $source
    $stem = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    $scans.Add([SerpLogSiteKeys.V1.Scanner]::Scan($relative, $stem, $source.Text, $oldKeyPattern, [string[]]$SiteMethod))
}
[SerpLogSiteKeys.V1.Planner]::AssignKeys($ModName, $scans)

$failed = @($scans | Where-Object { -not $_.Ok })
$sites = @(foreach ($scan in $scans) { foreach ($site in $scan.Sites) { $site } })
$rows = @(foreach ($site in $sites) {
        [pscustomobject]@{ Mod = $ModName; File = $site.File; Line = $site.Line; Callee = $site.Callee
            Member = $site.Member; MemberResolved = $site.MemberResolved; OldKey = $site.OldKey; NewKey = $site.NewKey }
    })
$modeLabel = 'dry run'; if ($Apply) { $modeLabel = 'apply' }
Write-Host ("[{0}] {1}: {2} site key(s) in {3} file(s) ({4})" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'), $ModName,
    $rows.Count, @($rows | Select-Object -ExpandProperty File -Unique).Count, $modeLabel)
foreach ($group in @($rows | Group-Object Callee | Sort-Object Name)) { Write-Host ("  {0}: {1}" -f $group.Name, $group.Count) }
$unresolved = @($rows | Where-Object { -not $_.MemberResolved })
if ($unresolved.Count) { Write-Warning ("{0} site(s) without a resolvable member; their keys use 'Unknown' plus the message slug." -f $unresolved.Count) }
foreach ($scan in $failed) { Write-Warning ("{0}: {1}" -f $scan.File, $scan.Error) }

if ($ReportPath) {
    $reportFull = [IO.Path]::GetFullPath($ReportPath)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFull)) | Out-Null
    if ($reportFull.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) {
        $content = ConvertTo-Json -InputObject $rows -Depth 3
    } else {
        $lines = @("Mod`tFile`tLine`tCallee`tMember`tOldKey`tNewKey")
        $lines += @(foreach ($row in $rows) { ($row.Mod, $row.File, $row.Line, $row.Callee, $row.Member, $row.OldKey, $row.NewKey) -join "`t" })
        $content = $lines -join "`r`n"
    }
    $content = $content.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n"
    [IO.File]::WriteAllText($reportFull, $content, (New-Object Text.UTF8Encoding($false)))
    Write-Host "  report: $reportFull"
}

if ($failed.Count) { Write-Host 'ERROR: Some files could not be parsed; nothing was written.'; exit 1 }
if (-not $Apply) { $rows | Format-Table File, Line, OldKey, NewKey -AutoSize | Out-String -Width 400 | Write-Host; exit 0 }

$written = 0
foreach ($scan in $scans) {
    if ($scan.Sites.Count -eq 0) { continue }
    $source = $sources[$scan.File]
    $text = $source.Text
    $builder = New-Object Text.StringBuilder $text
    foreach ($site in @($scan.Sites | Sort-Object Start -Descending)) {
        $expected = '"' + $site.OldKey + '"'
        if ($text.Substring($site.Start, $site.End - $site.Start) -cne $expected) { throw "Span mismatch in $($scan.File) at line $($site.Line)" }
        [void]$builder.Remove($site.Start, $site.End - $site.Start).Insert($site.Start, '"' + $site.NewKey + '"')
    }
    $updated = $builder.ToString()
    $path = Join-Path $modRoot ($scan.File.Replace('/', '\'))
    $encoding = New-Object Text.UTF8Encoding($source.Bom)
    [IO.File]::WriteAllText($path, $updated, $encoding)
    $check = Read-SourceFile $path
    if ($check.Text -cne $updated -or $check.Bom -ne $source.Bom) { throw "Verification failed after writing $path" }
    if ((Get-LineEndingCounts $text) -ne (Get-LineEndingCounts $updated)) { throw "Line endings changed in $path" }
    $written++
}
Write-Host ("  wrote {0} file(s)" -f $written)
exit 0
