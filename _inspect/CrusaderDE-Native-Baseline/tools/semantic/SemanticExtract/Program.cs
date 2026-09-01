using System.Reflection;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly Regex AobRegex = new(
        @"^(?:[0-9A-Fa-f]{2}|\?{1,2})(?:\s+(?:[0-9A-Fa-f]{2}|\?{1,2})){3,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("Expected command: managed or source");
            }

            return args[0] switch
            {
                "managed" when args.Length == 5 => ExtractManaged(args[1], args[2], args[3], args[4]),
                "source" when args.Length == 4 => ExtractSource(args[1], args[2], args[3]),
                _ => throw new ArgumentException(
                    "Usage: managed <assembly> <exports.jsonl> <output-dir> <binary-hash> | " +
                    "source <script-extender-root> <output-dir> <git-commit>")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int ExtractManaged(string assemblyPath, string exportsPath, string outputDir, string binaryHash)
    {
        Directory.CreateDirectory(outputDir);
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader pe = new(stream);
        MetadataReader reader = pe.GetMetadataReader();
        SignatureProvider provider = new(reader);

        Dictionary<string, ExportRecord> exports = File.ReadLines(exportsPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<ExportRecord>(line, JsonOptions)!)
            .Where(record => record.Name != null)
            .ToDictionary(record => record.Name!, StringComparer.Ordinal);

        Dictionary<int, string> methodNames = new();
        List<object> methods = new();
        List<PInvokeInfo> pinvokes = new();

        foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
        {
            MethodDefinition definition = reader.GetMethodDefinition(handle);
            int token = MetadataTokens.GetToken(handle);
            string declaringType = GetTypeName(reader, definition.GetDeclaringType());
            string name = reader.GetString(definition.Name);
            MethodSignature<string> signature = definition.DecodeSignature(provider, null);
            string display = FormatMethod(declaringType, name, signature);
            methodNames[token] = display;
            methods.Add(new
            {
                binaryHash,
                token = HexToken(token),
                declaringType,
                name,
                display,
                signature = FormatSignature(name, signature),
                relativeVirtualAddress = definition.RelativeVirtualAddress,
                pinvoke = (definition.Attributes & MethodAttributes.PinvokeImpl) != 0
            });

            if ((definition.Attributes & MethodAttributes.PinvokeImpl) == 0)
            {
                continue;
            }

            MethodImport import = definition.GetImport();
            string module = reader.GetString(reader.GetModuleReference(import.Module).Name);
            string entryPoint = import.Name.IsNil ? name : reader.GetString(import.Name);
            exports.TryGetValue(entryPoint, out ExportRecord? export);
            string[] parameterNames = definition.GetParameters()
                .Select(parameterHandle => reader.GetParameter(parameterHandle))
                .Where(parameter => parameter.SequenceNumber > 0)
                .OrderBy(parameter => parameter.SequenceNumber)
                .Select(parameter => parameter.Name.IsNil ? string.Empty : reader.GetString(parameter.Name))
                .ToArray();
            pinvokes.Add(new PInvokeInfo(
                binaryHash,
                HexToken(token),
                declaringType,
                name,
                display,
                module,
                entryPoint,
                FormatSignature(entryPoint, signature),
                signature.ReturnType,
                signature.ParameterTypes.ToArray(),
                parameterNames,
                export?.Address,
                export?.Rva,
                export != null));
        }

        List<object> calls = new();
        foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
        {
            MethodDefinition definition = reader.GetMethodDefinition(handle);
            if (definition.RelativeVirtualAddress == 0)
            {
                continue;
            }

            int callerToken = MetadataTokens.GetToken(handle);
            MethodBodyBlock body;
            try
            {
                body = pe.GetMethodBody(definition.RelativeVirtualAddress);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            byte[] il = body.GetILBytes() ?? Array.Empty<byte>();
            foreach ((int offset, int targetToken, string opcode) in EnumerateCalls(il))
            {
                string target = ResolveMethod(reader, provider, targetToken, methodNames);
                calls.Add(new
                {
                    binaryHash,
                    callerToken = HexToken(callerToken),
                    caller = methodNames[callerToken],
                    targetToken = HexToken(targetToken),
                    target,
                    ilOffset = $"0x{offset:X}",
                    opcode
                });
            }
        }

        WriteJsonLines(Path.Combine(outputDir, "managed-methods.jsonl"), methods);
        WriteJsonLines(Path.Combine(outputDir, "pinvokes.jsonl"), pinvokes);
        WriteJsonLines(Path.Combine(outputDir, "managed-calls.jsonl"), calls);

        object summary = new
        {
            assembly = Path.GetFullPath(assemblyPath),
            binaryHash,
            methodCount = methods.Count,
            pinvokeCount = pinvokes.Count,
            resolvedCrusaderExports = pinvokes.Count(p => p.Resolved),
            callCount = calls.Count
        };
        WriteJson(Path.Combine(outputDir, "managed-summary.json"), summary);
        Console.WriteLine(JsonSerializer.Serialize(summary, JsonOptions));
        return 0;
    }

    private static int ExtractSource(string rootPath, string outputDir, string gitCommit)
    {
        string root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(outputDir);
        List<object> files = new();
        List<object> patterns = new();
        List<object> delegates = new();
        List<object> types = new();
        List<object> typeFields = new();
        List<object> vtableMembers = new();

        IEnumerable<string> sourceFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}SHCDESE.BepInEx{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains($"{Path.DirectorySeparatorChar}ReverseEngineering{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (string path in sourceFiles)
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            byte[] bytes = File.ReadAllBytes(path);
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            string text = File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text, path: path);
            CompilationUnitSyntax syntaxRoot = tree.GetCompilationUnitRoot();
            files.Add(new { gitCommit, path = relative, sha256 = hash, bytes = bytes.Length });

            foreach (LiteralExpressionSyntax literal in syntaxRoot.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    continue;
                }

                string value = literal.Token.ValueText.Trim();
                if (!AobRegex.IsMatch(value))
                {
                    continue;
                }

                VariableDeclaratorSyntax? variable = literal.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                AssignmentExpressionSyntax? assignment = literal.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
                ObjectCreationExpressionSyntax? creation = literal.Ancestors().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();
                InvocationExpressionSyntax? invocation = literal.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                string symbol = variable?.Identifier.ValueText ?? assignment?.Left.ToString() ?? "unknown";
                string context = creation?.Type.ToString() ?? invocation?.Expression.ToString() ?? "literal";
                bool directFunction = context.Contains("X64ManagedFunctionAOB", StringComparison.Ordinal) ||
                    context.Contains("X64ManagedFunctionDetourAOB", StringComparison.Ordinal) ||
                    context.Contains("LazyR3Detour", StringComparison.Ordinal);
                string resolutionKind = directFunction ? "function" :
                    context.Contains("VTable", StringComparison.OrdinalIgnoreCase) ? "vtable" :
                    context.Contains("Global", StringComparison.OrdinalIgnoreCase) ? "global" : "unknown";
                FileLinePositionSpan span = literal.GetLocation().GetLineSpan();
                patterns.Add(new
                {
                    gitCommit,
                    sourcePath = relative,
                    sourceFileHash = hash,
                    sourceLine = span.StartLinePosition.Line + 1,
                    symbol,
                    context,
                    directFunction,
                    resolutionKind,
                    pattern = Regex.Replace(value, @"\s+", " ").ToUpperInvariant()
                });
            }

            foreach (DelegateDeclarationSyntax declaration in syntaxRoot.DescendantNodes().OfType<DelegateDeclarationSyntax>())
            {
                FileLinePositionSpan span = declaration.GetLocation().GetLineSpan();
                delegates.Add(new
                {
                    gitCommit,
                    sourcePath = relative,
                    sourceFileHash = hash,
                    sourceLine = span.StartLinePosition.Line + 1,
                    name = declaration.Identifier.ValueText,
                    returnType = declaration.ReturnType.ToString(),
                    parameters = declaration.ParameterList.Parameters.Select(parameter => new
                    {
                        name = parameter.Identifier.ValueText,
                        type = parameter.Type?.ToString()
                    }).ToArray(),
                    signature = declaration.ToString()
                });
            }

            foreach (BaseTypeDeclarationSyntax declaration in syntaxRoot.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (declaration is not StructDeclarationSyntax && declaration is not EnumDeclarationSyntax)
                {
                    continue;
                }

                FileLinePositionSpan span = declaration.GetLocation().GetLineSpan();
                types.Add(new
                {
                    gitCommit,
                    sourcePath = relative,
                    sourceFileHash = hash,
                    sourceLine = span.StartLinePosition.Line + 1,
                    kind = declaration.Kind().ToString(),
                    name = declaration.Identifier.ValueText,
                    declaration = declaration.ToString()
                });

                if (declaration is StructDeclarationSyntax structure)
                {
                    bool isVtable = relative.Contains("/VTables/", StringComparison.OrdinalIgnoreCase) ||
                        structure.Identifier.ValueText.EndsWith("VTable", StringComparison.OrdinalIgnoreCase);
                    int slot = 0;
                    foreach (FieldDeclarationSyntax field in structure.Members.OfType<FieldDeclarationSyntax>())
                    {
                        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                        {
                            FileLinePositionSpan fieldSpan = variable.GetLocation().GetLineSpan();
                            string fieldText = field.ToString();
                            Match offsetMatch = Regex.Match(fieldText, @"\boffset\s+(0x[0-9A-Fa-f]+|\d+)\b", RegexOptions.IgnoreCase);
                            string? offsetEvidence = offsetMatch.Success ? offsetMatch.Groups[1].Value : null;
                            int slotSpan = 1;
                            if (variable.ArgumentList?.Arguments.Count > 0 &&
                                int.TryParse(variable.ArgumentList.Arguments[0].Expression.ToString(), out int fixedCount))
                            {
                                slotSpan = Math.Max(1, fixedCount);
                            }
                            var record = new
                            {
                                gitCommit,
                                sourcePath = relative,
                                sourceFileHash = hash,
                                sourceLine = fieldSpan.StartLinePosition.Line + 1,
                                typeName = structure.Identifier.ValueText,
                                fieldName = variable.Identifier.ValueText,
                                fieldType = field.Declaration.Type.ToString(),
                                ordinal = slot,
                                slotSpan,
                                offsetEvidence,
                                declaration = fieldText
                            };
                            typeFields.Add(record);
                            if (isVtable)
                                vtableMembers.Add(record);
                            slot += slotSpan;
                        }
                    }

                    foreach (PropertyDeclarationSyntax property in structure.Members.OfType<PropertyDeclarationSyntax>())
                    {
                        string propertyText = property.ToString();
                        Match offsetMatch = Regex.Match(propertyText,
                            @"(?:At|SpanAt)\s*\(\s*(0x[0-9A-Fa-f]+|\d+)", RegexOptions.IgnoreCase);
                        if (!offsetMatch.Success)
                            continue;
                        FileLinePositionSpan propertySpan = property.GetLocation().GetLineSpan();
                        typeFields.Add(new
                        {
                            gitCommit,
                            sourcePath = relative,
                            sourceFileHash = hash,
                            sourceLine = propertySpan.StartLinePosition.Line + 1,
                            typeName = structure.Identifier.ValueText,
                            fieldName = property.Identifier.ValueText,
                            fieldType = property.Type.ToString(),
                            ordinal = (int?)null,
                            slotSpan = (int?)null,
                            offsetEvidence = offsetMatch.Groups[1].Value,
                            declaration = propertyText
                        });
                    }
                }
            }
        }

        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "ReverseEngineering", "structs"), "*.h"))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            byte[] bytes = File.ReadAllBytes(path);
            files.Add(new
            {
                gitCommit,
                path = relative,
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                bytes = bytes.Length
            });
        }

        WriteJsonLines(Path.Combine(outputDir, "source-files.jsonl"), files);
        WriteJsonLines(Path.Combine(outputDir, "patterns.jsonl"), patterns);
        WriteJsonLines(Path.Combine(outputDir, "delegates.jsonl"), delegates);
        WriteJsonLines(Path.Combine(outputDir, "source-types.jsonl"), types);
        WriteJsonLines(Path.Combine(outputDir, "type-fields.jsonl"), typeFields);
        WriteJsonLines(Path.Combine(outputDir, "vtable-members.jsonl"), vtableMembers);
        object summary = new
        {
            root,
            gitCommit,
            fileCount = files.Count,
            patternCount = patterns.Count,
            delegateCount = delegates.Count,
            typeCount = types.Count,
            typeFieldCount = typeFields.Count,
            vtableMemberCount = vtableMembers.Count
        };
        WriteJson(Path.Combine(outputDir, "source-summary.json"), summary);
        Console.WriteLine(JsonSerializer.Serialize(summary, JsonOptions));
        return 0;
    }

    private static IEnumerable<(int Offset, int Token, string Opcode)> EnumerateCalls(byte[] il)
    {
        int position = 0;
        while (position < il.Length)
        {
            int instructionOffset = position;
            ushort opcode = il[position++];
            if (opcode == 0xFE)
            {
                if (position >= il.Length) yield break;
                opcode = (ushort)(0xFE00 | il[position++]);
            }

            int operandStart = position;
            int operandSize = GetOperandSize(opcode, il, operandStart);
            if (operandStart + operandSize > il.Length)
            {
                yield break;
            }

            if (operandSize == 4 && IsCallOpcode(opcode))
            {
                int token = BitConverter.ToInt32(il, operandStart);
                yield return (instructionOffset, token, OpcodeName(opcode));
            }

            position += operandSize;
        }
    }

    private static int GetOperandSize(ushort opcode, byte[] il, int operandStart)
    {
        if (opcode == 0x45)
        {
            if (operandStart + 4 > il.Length) return il.Length - operandStart;
            int count = BitConverter.ToInt32(il, operandStart);
            return count < 0 ? 0 : 4 + count * 4;
        }

        if (opcode is 0x21 or 0x23) return 8;
        if (opcode == 0x22) return 4;
        if (opcode == 0x20 || opcode is >= 0x38 and <= 0x44 || opcode == 0xDD) return 4;
        if (opcode == 0x1F || opcode is >= 0x2B and <= 0x37 || opcode == 0xDE || opcode is 0xFE12 or 0xFE19) return 1;
        if (opcode is >= 0x0E and <= 0x13) return 1;
        if (opcode is >= 0xFE09 and <= 0xFE0E) return 2;
        if (HasMetadataTokenOperand(opcode)) return 4;
        return 0;
    }

    private static bool HasMetadataTokenOperand(ushort opcode) =>
        opcode is 0x27 or 0x28 or 0x29 or 0x6F or 0x70 or 0x71 or 0x72 or 0x73 or 0x74 or 0x75 or
            0x79 or 0x7B or 0x7C or 0x7D or 0x7E or 0x7F or 0x80 or 0x81 or
            0x8C or 0x8D or 0x8F or 0xA3 or 0xA4 or 0xA5 or 0xC2 or 0xC6 or 0xD0 or
            0xFE06 or 0xFE07 or 0xFE15 or 0xFE16 or 0xFE1C;

    private static bool IsCallOpcode(ushort opcode) => opcode is 0x27 or 0x28 or 0x29 or 0x6F or 0x73 or 0xFE06 or 0xFE07;

    private static string OpcodeName(ushort opcode) => opcode switch
    {
        0x27 => "jmp",
        0x28 => "call",
        0x29 => "calli",
        0x6F => "callvirt",
        0x73 => "newobj",
        0xFE06 => "ldftn",
        0xFE07 => "ldvirtftn",
        _ => $"0x{opcode:X}"
    };

    private static string ResolveMethod(MetadataReader reader, SignatureProvider provider, int token,
        IReadOnlyDictionary<int, string> methodNames)
    {
        if (methodNames.TryGetValue(token, out string? known))
        {
            return known;
        }

        EntityHandle handle;
        try { handle = MetadataTokens.EntityHandle(token); }
        catch (ArgumentException) { return HexToken(token); }

        try
        {
            if (handle.Kind == HandleKind.MemberReference)
            {
                MemberReference member = reader.GetMemberReference((MemberReferenceHandle)handle);
                string parent = GetParentName(reader, member.Parent);
                string name = reader.GetString(member.Name);
                if (member.GetKind() == MemberReferenceKind.Method)
                {
                    MethodSignature<string> signature = member.DecodeMethodSignature(provider, null);
                    return FormatMethod(parent, name, signature);
                }
                return $"{parent}::{name}";
            }

            if (handle.Kind == HandleKind.MethodSpecification)
            {
                MethodSpecification specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                return ResolveMethod(reader, provider, MetadataTokens.GetToken(specification.Method), methodNames);
            }
        }
        catch (BadImageFormatException)
        {
        }

        return HexToken(token);
    }

    private static string GetParentName(MetadataReader reader, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeName(reader, (TypeDefinitionHandle)handle),
        HandleKind.TypeReference => GetTypeName(reader, (TypeReferenceHandle)handle),
        HandleKind.TypeSpecification => "<TypeSpec>",
        HandleKind.MethodDefinition => reader.GetString(reader.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
        HandleKind.ModuleReference => reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)handle).Name),
        _ => $"<{handle.Kind}>"
    };

    private static string GetTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        string ns = reader.GetString(type.Namespace);
        string name = reader.GetString(type.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string GetTypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        TypeReference type = reader.GetTypeReference(handle);
        string ns = reader.GetString(type.Namespace);
        string name = reader.GetString(type.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string FormatMethod(string declaringType, string name, MethodSignature<string> signature) =>
        $"{declaringType}::{FormatSignature(name, signature)}";

    private static string FormatSignature(string name, MethodSignature<string> signature) =>
        $"{signature.ReturnType} {name}({string.Join(", ", signature.ParameterTypes)})";

    private static string HexToken(int token) => $"0x{token:X8}";

    private static void WriteJsonLines(string path, IEnumerable<object> records)
    {
        using StreamWriter writer = NewWriter(path);
        foreach (object record in records)
        {
            writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
        }
    }

    private static void WriteJson(string path, object value)
    {
        using StreamWriter writer = NewWriter(path);
        writer.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static StreamWriter NewWriter(string path) => new(path, false, new UTF8Encoding(false)) { NewLine = "\r\n" };

    private sealed record ExportRecord(string? Address, string? Rva, string? Name);

    private sealed record PInvokeInfo(
        string BinaryHash,
        string Token,
        string DeclaringType,
        string Name,
        string Display,
        string Module,
        string EntryPoint,
        string Signature,
        string ReturnType,
        string[] ParameterTypes,
        string[] ParameterNames,
        string? NativeAddress,
        string? NativeRva,
        bool Resolved);

    private sealed class SignatureProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly MetadataReader _reader;
        public SignatureProvider(MetadataReader reader) => _reader = reader;
        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";
        public string GetByReferenceType(string elementType) => $"{elementType}&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => $"fnptr<{FormatSignature("invoke", signature)}>";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(",", typeArguments)}>";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => $"{elementType} pinned";
        public string GetPointerType(string elementType) => $"{elementType}*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => GetTypeName(_reader, handle);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => GetTypeName(_reader, handle);
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        public string GetUnsupportedSignatureTypeKind(byte rawTypeKind) => $"unsupported(0x{rawTypeKind:X2})";
    }
}
