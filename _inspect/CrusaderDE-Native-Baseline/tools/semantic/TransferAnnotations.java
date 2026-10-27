// Transfers program annotations without executable memory contents.
//@category SerpsMods
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;
import com.google.gson.*;
import ghidra.app.util.headless.HeadlessScript;
import ghidra.app.util.NamespaceUtils;
import ghidra.app.util.SymbolPath;
import ghidra.app.util.xml.*;
import ghidra.framework.options.Options;
import ghidra.program.model.data.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.*;
import ghidra.program.model.symbol.SourceType;

public class TransferAnnotations extends HeadlessScript {
    private static final Gson JSON = new GsonBuilder().setPrettyPrinting().create();
    private static final String[] OWNERSHIP = {"NativeKnowledge import ownership", "NativeKnowledge function ownership"};

    @Override protected void run() throws Exception {
        String[] a = getScriptArgs();
        if (a.length != 3) throw new IllegalArgumentException("Expected export|import|verify bundle-directory report.json");
        File dir = new File(a[1]);
        File infoFile = new File(dir, "annotations.json");
        File xmlFile = new File(dir, "annotations.xml");
        File typesFile = new File(dir, "types.gdt");
        if (a[0].equals("export")) {
            if (infoFile.exists() || xmlFile.exists() || typesFile.exists()) throw new IllegalStateException("Export directory already contains annotations");
            Files.createDirectories(dir.toPath());
            JsonObject info = new JsonObject();
            info.addProperty("schemaVersion", 1);
            info.addProperty("nativeHash", currentProgram.getExecutableSHA256());
            info.addProperty("imageBase", currentProgram.getImageBase().toString());
            info.addProperty("language", currentProgram.getLanguageID().toString());
            info.addProperty("compiler", currentProgram.getCompilerSpec().getCompilerSpecID().toString());
            info.add("snapshot", snapshot());
            JsonObject owned = new JsonObject();
            for (String group : OWNERSHIP) {
                JsonObject values = new JsonObject();
                Options options = currentProgram.getOptions(group);
                for (String key : options.getOptionNames()) values.addProperty(key, options.getString(key, null));
                owned.add(group, values);
            }
            info.add("ownership", owned);
            FileDataTypeManager archive = FileDataTypeManager.createFileArchive(typesFile,
                currentProgram.getLanguageID(), currentProgram.getCompilerSpec().getCompilerSpecID());
            int tx = archive.startTransaction("Export annotations");
            try {
                archive.addDataTypes(allTypes(currentProgram.getDataTypeManager()), DataTypeConflictHandler.KEEP_HANDLER, monitor);
            } finally { archive.endTransaction(tx, true); }
            try { archive.save(); } finally { archive.close(); }
            println(new ProgramXmlMgr(xmlFile).write(currentProgram, currentProgram.getMemory(), monitor, xmlOptions()).toString());
            // The archive retains layouts/descriptions more faithfully than XML's datatype conversion.
            String xml = Files.readString(xmlFile.toPath(), StandardCharsets.UTF_8);
            xml = xml.replaceFirst("(?s)<DATATYPES>.*?</DATATYPES>", "");
            xml = xml.replaceFirst(" EXE_PATH=\"[^\"]*\"", "");
            xml = xml.replaceFirst("(<INFO_SOURCE[^>]*?) USER=\"[^\"]*\"", "$1");
            Files.writeString(xmlFile.toPath(), xml, StandardCharsets.UTF_8);
            Files.writeString(infoFile.toPath(), JSON.toJson(info) + "\r\n", StandardCharsets.UTF_8);
        } else {
            JsonObject info = JsonParser.parseString(Files.readString(infoFile.toPath(), StandardCharsets.UTF_8)).getAsJsonObject();
            if (info.get("schemaVersion").getAsInt() != 1 ||
                !info.get("nativeHash").getAsString().equalsIgnoreCase(currentProgram.getExecutableSHA256()) ||
                !info.get("imageBase").getAsString().equals(currentProgram.getImageBase().toString()) ||
                !info.get("language").getAsString().equals(currentProgram.getLanguageID().toString()) ||
                !info.get("compiler").getAsString().equals(currentProgram.getCompilerSpec().getCompilerSpecID().toString()))
                throw new IllegalArgumentException("Annotation native hash, image base or language does not match; no changes applied");
            if (a[0].equals("import")) {
                Options sharing = currentProgram.getOptions("Native annotation sharing");
                String bundleHash = HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(infoFile.toPath())));
                if (sharing.contains("bundleHash")) {
                    if (!sharing.getString("bundleHash", "").equals(bundleHash)) throw new IllegalStateException("Different bundle already imported; create a new project");
                    compare(info.getAsJsonObject("snapshot"), snapshot());
                    JsonObject report = new JsonObject();
                    report.addProperty("status", "ok"); report.addProperty("mode", "already-imported");
                    Files.writeString(Path.of(a[2]), JSON.toJson(report) + "\r\n", StandardCharsets.UTF_8);
                    return;
                }
                if (!isImporting() || isHeadlessAnalysisEnabled())
                    throw new IllegalStateException("Import requires a fresh DLL import without analysis; existing functions are protected");
                int tx = currentProgram.startTransaction("Import shared annotations");
                boolean commit = false;
                try {
                    FileDataTypeManager archive = FileDataTypeManager.openFileArchive(typesFile, false);
                    try { currentProgram.getDataTypeManager().addDataTypes(allTypes(archive), DataTypeConflictHandler.REPLACE_HANDLER, monitor); }
                    finally { archive.close(); }
                    restoreAliases(info.getAsJsonObject("snapshot").getAsJsonObject("types"));
                    XmlProgramOptions options = xmlOptions();
                    options.setAddToProgram(true);
                    options.setFunctions(false);
                    println(new ProgramXmlMgr(xmlFile).read(currentProgram, monitor, options).toString());
                    restoreFunctions(info.getAsJsonObject("snapshot").getAsJsonObject("functions"));
                    for (String group : OWNERSHIP) {
                        Options target = currentProgram.getOptions(group);
                        for (Map.Entry<String,JsonElement> e : info.getAsJsonObject("ownership").getAsJsonObject(group).entrySet())
                            target.setString(e.getKey(), e.getValue().getAsString());
                    }
                    compare(info.getAsJsonObject("snapshot"), snapshot());
                    sharing.setString("bundleHash", bundleHash);
                    commit = true;
                } finally { currentProgram.endTransaction(tx, commit); }
            } else if (!a[0].equals("verify")) throw new IllegalArgumentException("Unknown mode");
            compare(info.getAsJsonObject("snapshot"), snapshot());
        }
        JsonObject report = new JsonObject();
        report.addProperty("status", "ok");
        report.addProperty("mode", a[0]);
        report.addProperty("nativeHash", currentProgram.getExecutableSHA256());
        report.addProperty("functions", currentProgram.getFunctionManager().getFunctionCount());
        Files.writeString(Path.of(a[2]), JSON.toJson(report) + "\r\n", StandardCharsets.UTF_8);
        println("ANNOTATION_TRANSFER_COMPLETE " + a[0]);
    }

    private XmlProgramOptions xmlOptions() {
        XmlProgramOptions options = new XmlProgramOptions();
        options.setMemoryBlocks(false);
        options.setMemoryContents(false);
        options.setRelocationTable(false);
        options.setProperties(false);
        return options;
    }

    private List<DataType> allTypes(DataTypeManager manager) {
        List<DataType> types = new ArrayList<>();
        manager.getAllDataTypes().forEachRemaining(types::add);
        return types;
    }

    private JsonObject snapshot() {
        JsonObject result = new JsonObject();
        JsonObject types = new JsonObject();
        List<DataType> all = new ArrayList<>();
        currentProgram.getDataTypeManager().getAllDataTypes().forEachRemaining(all::add);
        all.sort(Comparator.comparing(DataType::getPathName));
        for (DataType type : all) {
            if (!(type instanceof Composite) && !(type instanceof ghidra.program.model.data.Enum) && !(type instanceof TypeDef)) continue;
            JsonObject t = new JsonObject();
            t.addProperty("length", type.getLength());
            t.addProperty("description", type.getDescription());
            if (type instanceof Composite c) {
                t.addProperty("union", c instanceof Union);
                JsonArray fields = new JsonArray();
                for (DataTypeComponent f : c.getDefinedComponents()) {
                    JsonObject field = new JsonObject();
                    field.addProperty("offset", f.getOffset());
                    field.addProperty("length", f.getLength());
                    field.addProperty("type", f.getDataType().getPathName());
                    field.addProperty("name", f.getFieldName());
                    field.addProperty("comment", f.getComment());
                    fields.add(field);
                }
                t.add("fields", fields);
            } else {
                t.addProperty("definition", type.toString());
                if (type instanceof TypeDef td) t.addProperty("baseType", td.getDataType().getPathName());
            }
            types.add(type.getPathName(), t);
        }
        result.add("types", types);
        JsonObject functions = new JsonObject();
        FunctionIterator iterator = currentProgram.getFunctionManager().getFunctions(true);
        while (iterator.hasNext()) {
            Function f = iterator.next();
            JsonObject v = new JsonObject();
            v.addProperty("name", f.getName(true));
            v.addProperty("ownName", f.getSymbol().getName());
            v.addProperty("ownSource", f.getSymbol().getSource().toString());
            v.addProperty("ownNamespace", f.getSymbol().getParentNamespace().getName(true));
            if (f.isThunk()) {
                Function target = f.getThunkedFunction(false);
                JsonObject thunk = new JsonObject();
                thunk.addProperty("external", target.isExternal());
                thunk.addProperty("entry", target.getEntryPoint().toString());
                thunk.addProperty("name", target.getName());
                thunk.addProperty("library", target.getParentNamespace().getName());
                v.add("thunk", thunk);
            }
            v.addProperty("prototype", f.getPrototypeString(true, true));
            v.addProperty("body", f.getBody().toString());
            JsonArray ranges = new JsonArray();
            for (AddressRange range : f.getBody()) {
                JsonArray pair = new JsonArray();
                pair.add(range.getMinAddress().toString()); pair.add(range.getMaxAddress().toString()); ranges.add(pair);
            }
            v.add("ranges", ranges);
            v.addProperty("callingConvention", f.getCallingConventionName());
            v.addProperty("customStorage", f.hasCustomVariableStorage());
            v.addProperty("source", f.getSignatureSource().toString());
            v.addProperty("varArgs", f.hasVarArgs());
            v.addProperty("noReturn", f.hasNoReturn());
            v.addProperty("inline", f.isInline());
            v.add("return", variable(f.getReturn()));
            JsonArray parameters = new JsonArray();
            for (Parameter p : f.getParameters()) if (!p.isAutoParameter()) parameters.add(variable(p));
            v.add("parameters", parameters);
            v.addProperty("comment", f.getComment());
            v.addProperty("repeatableComment", f.getRepeatableComment());
            functions.add(f.getEntryPoint().toString(), v);
        }
        result.add("functions", functions);
        return JsonParser.parseString(JSON.toJson(result)).getAsJsonObject();
    }

    private void restoreAliases(JsonObject types) throws Exception {
        DataTypeManager manager = currentProgram.getDataTypeManager();
        for (Map.Entry<String,JsonElement> e : types.entrySet()) {
            JsonObject v = e.getValue().getAsJsonObject();
            if (manager.getDataType(e.getKey()) != null || !v.has("baseType")) continue;
            DataType base = manager.getDataType(v.get("baseType").getAsString());
            if (base == null) throw new IllegalStateException("Missing typedef base: " + e.getKey());
            int split = e.getKey().lastIndexOf('/');
            // Archive resolution folds equivalent .conflict aliases. Preserve their original name explicitly.
            DataType alias = manager.addDataType(new TypedefDataType(new CategoryPath(e.getKey().substring(0, split)),
                "__annotation_alias", base, manager), DataTypeConflictHandler.KEEP_HANDLER);
            alias.setName(e.getKey().substring(split + 1));
        }
    }

    private JsonObject variable(Variable var) {
        JsonObject v = new JsonObject();
        v.addProperty("name", var.getName());
        v.addProperty("type", var.getDataType().getPathName());
        v.addProperty("storage", var.getVariableStorage().getSerializationString());
        v.addProperty("comment", var.getComment());
        return v;
    }

    private Parameter parameter(JsonObject v, boolean isReturn) throws Exception {
        DataType dt = currentProgram.getDataTypeManager().getDataType(v.get("type").getAsString());
        if (dt == null) dt = BuiltInDataTypeManager.getDataTypeManager().getDataType(v.get("type").getAsString());
        if (dt == null && v.get("type").getAsString().equals("/undefined")) dt = DataType.DEFAULT;
        if (dt == null && v.get("type").getAsString().equals("/void")) dt = VoidDataType.dataType;
        if (dt == null) throw new IllegalStateException("Missing parameter type: " + v.get("type"));
        VariableStorage storage = VariableStorage.deserialize(currentProgram, v.get("storage").getAsString());
        Parameter p = isReturn ? new ReturnParameterImpl(dt, storage, currentProgram)
            : new ParameterImpl(v.get("name").getAsString(), dt, storage, currentProgram);
        if (v.has("comment")) p.setComment(v.get("comment").getAsString());
        return p;
    }

    private void restoreFunctions(JsonObject functions) throws Exception {
        for (Map.Entry<String,JsonElement> e : functions.entrySet()) {
            monitor.checkCancelled();
            JsonObject v = e.getValue().getAsJsonObject();
            Address entry = toAddr(e.getKey());
            AddressSet body = new AddressSet();
            for (JsonElement r : v.getAsJsonArray("ranges")) {
                JsonArray pair = r.getAsJsonArray();
                body.addRange(toAddr(pair.get(0).getAsString()), toAddr(pair.get(1).getAsString()));
            }
            Function f = currentProgram.getFunctionManager().getFunctionAt(entry);
            if (f == null) f = currentProgram.getFunctionManager().createFunction(null, entry, body, SourceType.DEFAULT);
            else f.setBody(body);
            String name = v.get("ownName").getAsString();
            if (!v.has("thunk") && (!f.getSymbol().getName().equals(name) || !f.getSymbol().getParentNamespace().getName(true).equals(v.get("ownNamespace").getAsString()))) {
                var namespace = NamespaceUtils.createNamespaceHierarchy(v.get("ownNamespace").getAsString(),
                    currentProgram.getGlobalNamespace(), currentProgram, entry, SourceType.USER_DEFINED);
                f.getSymbol().setNameAndNamespace(name, namespace, SourceType.valueOf(v.get("ownSource").getAsString()));
            }
            List<Variable> parameters = new ArrayList<>();
            for (JsonElement p : v.getAsJsonArray("parameters")) parameters.add(parameter(p.getAsJsonObject(), false));
            Function.FunctionUpdateType update = v.get("customStorage").getAsBoolean()
                ? Function.FunctionUpdateType.CUSTOM_STORAGE : Function.FunctionUpdateType.DYNAMIC_STORAGE_ALL_PARAMS;
            f.updateFunction(v.get("callingConvention").getAsString(), parameter(v.getAsJsonObject("return"), true),
                parameters, update, true, SourceType.valueOf(v.get("source").getAsString()));
            if (!f.getPrototypeString(true, true).equals(v.get("prototype").getAsString())) {
                // Some older analyzed __thiscall records store an explicit this parameter with dynamic
                // storage. Reconstruct their exact names/types/registers using explicit storage.
                f.updateFunction(v.get("callingConvention").getAsString(), parameter(v.getAsJsonObject("return"), true),
                    parameters, Function.FunctionUpdateType.CUSTOM_STORAGE, true, SourceType.valueOf(v.get("source").getAsString()));
            }
            f.setVarArgs(v.get("varArgs").getAsBoolean());
            f.setNoReturn(v.get("noReturn").getAsBoolean());
            f.setInline(v.get("inline").getAsBoolean());
            f.setComment(v.has("comment") ? v.get("comment").getAsString() : null);
            f.setRepeatableComment(v.has("repeatableComment") ? v.get("repeatableComment").getAsString() : null);
        }
        for (Map.Entry<String,JsonElement> e : functions.entrySet()) {
            JsonObject v = e.getValue().getAsJsonObject();
            if (!v.has("thunk")) continue;
            JsonObject spec = v.getAsJsonObject("thunk");
            Function target;
            if (spec.get("external").getAsBoolean()) {
                var location = currentProgram.getExternalManager().getUniqueExternalLocation(spec.get("library").getAsString(), spec.get("name").getAsString());
                if (location == null) throw new IllegalStateException("Missing external thunk target: " + spec);
                target = location.getFunction() == null ? location.createFunction() : location.getFunction();
                Function original = currentProgram.getFunctionManager().getFunctionAt(toAddr(e.getKey()));
                target.updateFunction(original.getCallingConventionName(), new ReturnParameterImpl(original.getReturn(), currentProgram),
                    Arrays.asList(original.getParameters()), original.hasCustomVariableStorage() ? Function.FunctionUpdateType.CUSTOM_STORAGE : Function.FunctionUpdateType.DYNAMIC_STORAGE_ALL_PARAMS,
                    true, original.getSignatureSource());
            } else target = currentProgram.getFunctionManager().getFunctionAt(toAddr(spec.get("entry").getAsString()));
            currentProgram.getFunctionManager().getFunctionAt(toAddr(e.getKey())).setThunkedFunction(target);
        }
    }

    private void compare(JsonObject expected, JsonObject actual) {
        List<String> differences = new ArrayList<>();
        for (String section : List.of("types", "functions")) {
            JsonObject have = actual.getAsJsonObject(section);
            for (Map.Entry<String,JsonElement> e : expected.getAsJsonObject(section).entrySet())
                if (!equivalent(section, e.getValue(), have.get(e.getKey()))) differences.add(section + ":" + e.getKey());
        }
        if (!differences.isEmpty()) throw new IllegalStateException("Annotation differences (" + differences.size() + "): " + differences.subList(0, Math.min(25, differences.size())));
    }

    private boolean equivalent(String section, JsonElement expected, JsonElement actual) {
        if (actual == null) return false;
        if (!section.equals("functions")) return expected.equals(actual);
        JsonObject left = expected.getAsJsonObject().deepCopy(), right = actual.getAsJsonObject().deepCopy();
        // The storage mode is an import representation detail; every parameter's actual storage,
        // type, name and the full displayed prototype remain mandatory comparison inputs.
        left.remove("customStorage"); right.remove("customStorage");
        // Loader-assigned EXTERNAL offsets may differ across platforms; library/name identify imports.
        for (JsonObject f : List.of(left, right))
            if (f.has("thunk") && f.getAsJsonObject("thunk").get("external").getAsBoolean()) f.getAsJsonObject("thunk").remove("entry");
        return left.equals(right);
    }
}
