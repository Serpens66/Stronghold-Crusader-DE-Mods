// Applies hash-validated semantic labels, P/Invoke prototypes, and copied type headers.
//@category SerpsMods

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Base64;
import java.util.Iterator;
import java.util.List;

import ghidra.app.script.GhidraScript;
import ghidra.app.util.cparser.C.CParserUtils;
import ghidra.program.model.address.Address;
import ghidra.program.model.data.AbstractIntegerDataType;
import ghidra.program.model.data.BooleanDataType;
import ghidra.program.model.data.ByteDataType;
import ghidra.program.model.data.DataType;
import ghidra.program.model.data.DataTypeConflictHandler;
import ghidra.program.model.data.DoubleDataType;
import ghidra.program.model.data.FileDataTypeManager;
import ghidra.program.model.data.FloatDataType;
import ghidra.program.model.data.IntegerDataType;
import ghidra.program.model.data.LongDataType;
import ghidra.program.model.data.PointerDataType;
import ghidra.program.model.data.ShortDataType;
import ghidra.program.model.data.UnsignedCharDataType;
import ghidra.program.model.data.UnsignedIntegerDataType;
import ghidra.program.model.data.UnsignedLongDataType;
import ghidra.program.model.data.UnsignedShortDataType;
import ghidra.program.model.data.VoidDataType;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Function.FunctionUpdateType;
import ghidra.program.model.listing.Parameter;
import ghidra.program.model.listing.ParameterImpl;
import ghidra.program.model.symbol.SourceType;

public class ApplyCrusaderSemantics extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 5) {
            throw new IllegalArgumentException("Expected labels.tsv prototypes.tsv types.h output.gdt applied.jsonl");
        }
        File labels = new File(args[0]);
        File prototypes = new File(args[1]);
        File header = new File(args[2]);
        File gdt = new File(args[3]);
        File output = new File(args[4]);
        int importedTypes;
        try {
            importedTypes = importTypes(header, gdt);
        }
        catch (Exception typeError) {
            importedTypes = -1;
            printerr("SEMANTIC_TYPES_FAILED " + typeError);
        }
        int prototypeCount = applyPrototypes(prototypes);
        int labelCount = applyLabels(labels, output);
        println("SEMANTIC_APPLY_COMPLETE labels=" + labelCount + " prototypes=" + prototypeCount + " importedTypes=" + importedTypes);
    }

    private int importTypes(File header, File gdt) throws Exception {
        if (!header.isFile()) {
            println("SEMANTIC_TYPES_SKIPPED missing=" + header);
            return 0;
        }
        if (gdt.exists() && !gdt.delete()) {
            throw new IllegalStateException("Could not replace " + gdt);
        }
        File lock = new File(gdt.getAbsolutePath() + ".ulock");
        if (lock.exists()) lock.delete();
        FileDataTypeManager archive = CParserUtils.parseHeaderFiles(
            null,
            new String[] { header.getAbsolutePath() },
            new String[] { header.getParentFile().getAbsolutePath() },
            new String[] { "-D__int64=long long" },
            gdt.getAbsolutePath(),
            currentProgram.getLanguageID().getIdAsString(),
            currentProgram.getCompilerSpec().getCompilerSpecID().getIdAsString(),
            monitor);
        int count = 0;
        try {
            Iterator<DataType> iterator = archive.getAllDataTypes();
            while (iterator.hasNext() && !monitor.isCancelled()) {
                DataType type = iterator.next();
                currentProgram.getDataTypeManager().addDataType(type, DataTypeConflictHandler.KEEP_HANDLER);
                count++;
            }
        }
        finally {
            archive.close();
        }
        return count;
    }

    private int applyPrototypes(File file) throws Exception {
        int applied = 0;
        try (BufferedReader reader = reader(file)) {
            String line = reader.readLine();
            while ((line = reader.readLine()) != null && !monitor.isCancelled()) {
                String[] fields = line.split("\\t", -1);
                if (fields.length < 6) continue;
                Address address = currentProgram.getImageBase().add(parseHex(fields[0]));
                Function function = currentProgram.getFunctionManager().getFunctionAt(address);
                if (function == null) continue;
                function.setReturnType(mapType(fields[2]), SourceType.USER_DEFINED);
                String[] typeNames = fields[3].isEmpty() ? new String[0] : fields[3].split("\\|", -1);
                String[] parameterNames = fields[4].isEmpty() ? new String[0] : fields[4].split("\\|", -1);
                List<Parameter> parameters = new ArrayList<>();
                for (int i = 0; i < typeNames.length; i++) {
                    String name = i < parameterNames.length && !parameterNames[i].isEmpty() ? parameterNames[i] : "param" + (i + 1);
                    parameters.add(new ParameterImpl(name, mapType(typeNames[i]), currentProgram));
                }
                function.replaceParameters(FunctionUpdateType.DYNAMIC_STORAGE_ALL_PARAMS, true,
                    SourceType.USER_DEFINED, parameters.toArray(new Parameter[0]));
                appendComment(function, "[semantic:confirmed] Managed P/Invoke signature: " + fields[5]);
                applied++;
            }
        }
        return applied;
    }

    private int applyLabels(File file, File output) throws Exception {
        int applied = 0;
        try (BufferedReader reader = reader(file); BufferedWriter writer = writer(output)) {
            writer.write("{\"records\":[\r\n");
            reader.readLine();
            boolean first = true;
            String line;
            while ((line = reader.readLine()) != null && !monitor.isCancelled()) {
                String[] fields = line.split("\\t", -1);
                if (fields.length < 5) continue;
                long rva = parseHex(fields[0]);
                Address match = currentProgram.getImageBase().add(rva);
                Function function = currentProgram.getFunctionManager().getFunctionContaining(match);
                if (function == null) continue;
                boolean exact = function.getEntryPoint().equals(match);
                String requested = sanitize(fields[1]);
                String finalName = exact ? requested : "prob_" + requested;
                if (finalName.isEmpty() || finalName.equals("unknown")) continue;
                String oldName = function.getName();
                try {
                    function.setName(finalName, SourceType.USER_DEFINED);
                }
                catch (Exception collision) {
                    finalName = finalName + "_" + String.format("%X", function.getEntryPoint().subtract(currentProgram.getImageBase()));
                    function.setName(finalName, SourceType.USER_DEFINED);
                }
                String confidence = exact ? "confirmed" : "probable";
                appendComment(function, "[semantic:" + confidence + "] Evidence " + fields[4] +
                    " from " + fields[2] + ":" + fields[3] + "; matched " + match);
                if (!first) writer.write(",\r\n");
                first = false;
                writer.write("  {\"address\":\"" + function.getEntryPoint() + "\",\"rva\":\"0x" +
                    Long.toHexString(function.getEntryPoint().subtract(currentProgram.getImageBase())).toUpperCase() +
                    "\",\"oldName\":" + quote(oldName) + ",\"name\":" + quote(finalName) +
                    ",\"confidence\":" + quote(confidence) + ",\"matchAddress\":\"" + match +
                    "\",\"source\":" + quote(fields[2] + ":" + fields[3]) + "}");
                applied++;
            }
            writer.write("\r\n]}\r\n");
        }
        return applied;
    }

    private DataType mapType(String input) {
        String value = input.trim();
        boolean pointer = value.endsWith("*") || value.endsWith("&") || value.endsWith("[]") || value.equals("IntPtr") || value.equals("UIntPtr");
        while (value.endsWith("*") || value.endsWith("&")) value = value.substring(0, value.length() - 1).trim();
        if (value.endsWith("[]")) value = value.substring(0, value.length() - 2).trim();
        DataType base = switch (value) {
            case "Void" -> VoidDataType.dataType;
            case "Boolean", "Byte" -> UnsignedCharDataType.dataType;
            case "SByte" -> ByteDataType.dataType;
            case "Int16" -> ShortDataType.dataType;
            case "UInt16", "Char" -> UnsignedShortDataType.dataType;
            case "Int32" -> IntegerDataType.dataType;
            case "UInt32" -> UnsignedIntegerDataType.dataType;
            case "Int64" -> LongDataType.dataType;
            case "UInt64" -> UnsignedLongDataType.dataType;
            case "Single" -> FloatDataType.dataType;
            case "Double" -> DoubleDataType.dataType;
            default -> {
                DataType found = currentProgram.getDataTypeManager().getDataType("/" + value.replace('.', '/'));
                yield found == null ? VoidDataType.dataType : found;
            }
        };
        return pointer ? new PointerDataType(base, currentProgram.getDataTypeManager()) : base;
    }

    private static void appendComment(Function function, String value) {
        String current = function.getComment();
        if (current == null || current.isEmpty()) function.setComment(value);
        else if (!current.contains(value)) function.setComment(current + "\n" + value);
    }

    private static String sanitize(String value) {
        return value.replaceAll("[^A-Za-z0-9_:$@?]", "_");
    }

    private static long parseHex(String value) {
        return Long.parseUnsignedLong(value.replaceFirst("^0[xX]", ""), 16);
    }

    private static BufferedReader reader(File file) throws Exception {
        return new BufferedReader(new InputStreamReader(new FileInputStream(file), StandardCharsets.UTF_8));
    }

    private static BufferedWriter writer(File file) throws Exception {
        return new BufferedWriter(new OutputStreamWriter(new FileOutputStream(file, false), StandardCharsets.UTF_8));
    }

    private static String quote(String value) {
        if (value == null) return "null";
        return "\"" + value.replace("\\", "\\\\").replace("\"", "\\\"").replace("\r", "\\r").replace("\n", "\\n") + "\"";
    }
}
