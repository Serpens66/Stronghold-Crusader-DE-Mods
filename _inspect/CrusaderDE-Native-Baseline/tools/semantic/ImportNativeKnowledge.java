// Imports/verifies metadata-only views; never applies a data type to game memory or a function.
//@category SerpsMods
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.security.MessageDigest;
import java.util.*;
import com.google.gson.*;
import ghidra.app.script.GhidraScript;
import ghidra.framework.options.Options;
import ghidra.program.model.data.*;

public class ImportNativeKnowledge extends GhidraScript {
    private JsonObject plan;
    private DataTypeManager manager;
    private String category;
    private static final String MARKER = "NativeKnowledge owned schema=1";

    @Override protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 3) throw new IllegalArgumentException("Expected plan.json apply|verify report.json");
        plan = JsonParser.parseString(Files.readString(new File(args[0]).toPath(), StandardCharsets.UTF_8)).getAsJsonObject();
        if (plan.get("schemaVersion").getAsInt() != 1 ||
            !plan.get("nativeHash").getAsString().equalsIgnoreCase(currentProgram.getExecutableSHA256()))
            throw new IllegalArgumentException("NativeKnowledge hash does not match Ghidra program; no changes applied.");
        category = plan.get("category").getAsString();
        if (!category.equals("/NativeKnowledge/" + plan.get("nativeHash").getAsString().substring(0, 8)))
            throw new IllegalArgumentException("Unexpected owned category");
        manager = currentProgram.getDataTypeManager();
        boolean apply = args[1].equals("apply");
        boolean guardtest = args[1].equals("guardtest");
        if (!apply && !guardtest && !args[1].equals("verify")) throw new IllegalArgumentException("Unknown mode");
        int transaction = currentProgram.startTransaction("NativeKnowledge metadata import");
        boolean commit = false;
        try {
        Options ownership = currentProgram.getOptions("NativeKnowledge import ownership");
        if (guardtest) {
            String path = JsonParser.parseString(ownership.getString("paths", "[]")).getAsJsonArray().get(0).getAsString();
            ((Composite)manager.getDataType(path)).setDescription("Simulated manual annotation; rolled back by test.");
            boolean refused = false;
            try { assertUnmodified(ownership); }
            catch (IllegalStateException expected) { refused = true; }
            if (!refused) throw new IllegalStateException("Manual-change guard failed");
            Files.writeString(new File(args[2]).toPath(), "{\"status\":\"ok\",\"manualChangeRefused\":true}\r\n", StandardCharsets.UTF_8);
            println("NATIVE_KNOWLEDGE_GUARDS_COMPLETE manual-change-refused");
            return; // commit remains false: simulation never reaches the saved project.
        }
        // Check the entire previous owned inventory before any mutation, including removed entries.
        if (apply) assertUnmodified(ownership);
        Set<String> paths = new TreeSet<>();
        for (JsonElement element : plan.getAsJsonArray("types")) {
            JsonObject spec = element.getAsJsonObject();
            String path = spec.get("path").getAsString();
            if (!path.startsWith(category + "/") || !paths.add(path) || !spec.get("description").getAsString().startsWith(MARKER))
                throw new IllegalArgumentException("Invalid or duplicate owned type: " + path);
            DataType old = manager.getDataType(path);
            if (apply && old != null && !ownership.contains(path))
                throw new IllegalStateException("Unowned type collision; import refused: " + path);
        }
        if (apply && ownership.contains("paths")) {
            for (JsonElement path : JsonParser.parseString(ownership.getString("paths", "[]")).getAsJsonArray())
                if (!paths.contains(path.getAsString()))
                    throw new IllegalStateException("Removed owned type requires explicit migration: " + path.getAsString());
        }
        int changed = 0;
        for (JsonElement element : plan.getAsJsonArray("types")) {
            monitor.checkCancelled();
            JsonObject spec = element.getAsJsonObject();
            String path = spec.get("path").getAsString();
            if (apply && !matches(spec)) {
                int split = path.lastIndexOf('/');
                CategoryPath folder = new CategoryPath(path.substring(0, split));
                String name = path.substring(split + 1);
                Composite type = spec.get("kind").getAsString().equals("union")
                    ? new UnionDataType(folder, name, manager)
                    : new StructureDataType(folder, name, spec.get("length").getAsInt(), manager);
                type.setDescription(spec.get("description").getAsString());
                for (JsonElement member : spec.getAsJsonArray("components")) {
                    JsonObject m = member.getAsJsonObject();
                    DataType scalar = memberType(m);
                    int length = m.get("length").getAsInt() * m.get("count").getAsInt();
                    if (type instanceof Structure structure)
                        structure.replaceAtOffset(m.get("offset").getAsInt(), scalar, length,
                            m.get("name").getAsString(), m.get("comment").getAsString());
                    else ((Union)type).add(scalar, length, m.get("name").getAsString(), m.get("comment").getAsString());
                }
                Composite stored = (Composite)manager.addDataType(type, DataTypeConflictHandler.REPLACE_HANDLER);
                // Ghidra may reuse a layout-equivalent type even when descriptions or
                // component comments differ. Transfer those annotations explicitly.
                stored.setDescription(type.getDescription());
                DataTypeComponent[] saved = stored.getDefinedComponents();
                JsonArray members = spec.getAsJsonArray("components");
                if (saved.length != members.size()) throw new IllegalStateException("Unexpected component merge: " + path);
                for (int i = 0; i < saved.length; i++) {
                    JsonObject m = members.get(i).getAsJsonObject();
                    saved[i].setFieldName(m.get("name").getAsString());
                    saved[i].setComment(m.get("comment").getAsString());
                }
                changed++;
            }
            if (!matches(spec)) throw new IllegalStateException("Stored type differs from catalog: " + path);
        }
        if (apply) {
            for (String path : paths) ownership.setString(path, fingerprint(manager.getDataType(path)));
            ownership.setString("paths", new Gson().toJson(paths));
            ownership.setString("catalogSha256", plan.get("catalogSha256").getAsString());
        }
        JsonObject report = new JsonObject();
        report.addProperty("status", "ok"); report.addProperty("mode", args[1]);
        report.addProperty("nativeHash", plan.get("nativeHash").getAsString());
        report.addProperty("types", paths.size()); report.addProperty("changed", changed);
        report.add("fieldIds", plan.getAsJsonArray("fieldIds"));
        report.addProperty("catalogSha256", plan.get("catalogSha256").getAsString());
        Files.writeString(new File(args[2]).toPath(), new GsonBuilder().setPrettyPrinting().create().toJson(report).replace("\n", "\r\n") + "\r\n", StandardCharsets.UTF_8);
        println("NATIVE_KNOWLEDGE_COMPLETE mode=" + args[1] + " fields=" + plan.getAsJsonArray("fieldIds").size() + " types=" + paths.size() + " changed=" + changed);
        commit = true;
        } finally { currentProgram.endTransaction(transaction, commit); }
    }

    private void assertUnmodified(Options ownership) throws Exception {
        if (!ownership.contains("paths")) return;
        for (JsonElement path : JsonParser.parseString(ownership.getString("paths", "[]")).getAsJsonArray()) {
            String value = path.getAsString();
            DataType old = manager.getDataType(value);
            if (old == null || !fingerprint(old).equals(ownership.getString(value, "")))
                throw new IllegalStateException("Manually changed/missing owned type; import refused: " + value);
        }
    }

    private DataType memberType(JsonObject member) {
        String name = member.get("type").getAsString();
        int width = member.get("length").getAsInt();
        DataType value = switch(name) {
            case "UInt8" -> UnsignedCharDataType.dataType;
            case "Int8" -> SignedByteDataType.dataType;
            case "UInt16" -> UnsignedShortDataType.dataType;
            case "Int16" -> ShortDataType.dataType;
            case "UInt32" -> UnsignedIntegerDataType.dataType;
            case "Int32" -> IntegerDataType.dataType;
            case "UInt64" -> UnsignedLongLongDataType.dataType;
            case "Int64" -> LongLongDataType.dataType;
            case "Float32" -> FloatDataType.dataType;
            case "Float64" -> DoubleDataType.dataType;
            case "Undefined" -> Undefined.getUndefinedDataType(width);
            default -> manager.getDataType(name);
        };
        if (value == null || value.getLength() != width) throw new IllegalArgumentException("Missing or wrong-size member type: " + name);
        int count = member.get("count").getAsInt();
        return count == 1 ? value : new ArrayDataType(value, count, width, manager);
    }

    private boolean matches(JsonObject spec) {
        DataType stored = manager.getDataType(spec.get("path").getAsString());
        if (!(stored instanceof Composite composite) || stored.getLength() != spec.get("length").getAsInt() ||
            !stored.getDescription().equals(spec.get("description").getAsString()) ||
            ((stored instanceof Union) != spec.get("kind").getAsString().equals("union"))) return false;
        DataTypeComponent[] members = composite.getDefinedComponents();
        JsonArray expected = spec.getAsJsonArray("components");
        if (members.length != expected.size()) return false;
        for (int i = 0; i < members.length; i++) {
            JsonObject e = expected.get(i).getAsJsonObject();
            DataTypeComponent m = members[i];
            if (m.getOffset() != e.get("offset").getAsInt() || !Objects.equals(m.getFieldName(), e.get("name").getAsString()) ||
                !Objects.equals(m.getComment() == null ? "" : m.getComment(), e.get("comment").getAsString()) ||
                !m.getDataType().isEquivalent(memberType(e))) return false;
        }
        return true;
    }

    private String fingerprint(DataType type) throws Exception {
        StringBuilder text = new StringBuilder(type.getPathName()).append('\n').append(type.getLength()).append('\n').append(type.getDescription());
        if (type instanceof Composite composite) for (DataTypeComponent c : composite.getDefinedComponents())
            text.append('\n').append(c.getOffset()).append('|').append(c.getLength()).append('|').append(c.getFieldName()).append('|').append(c.getDataType().getPathName()).append('|').append(c.getComment());
        byte[] bytes = MessageDigest.getInstance("SHA-256").digest(text.toString().getBytes(StandardCharsets.UTF_8));
        return HexFormat.of().formatHex(bytes);
    }
}
