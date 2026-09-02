// Exports semantic function evidence and stable fingerprints for search and version matching.
//@category SerpsMods

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.Iterator;
import java.util.List;
import java.util.Set;
import java.util.TreeSet;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.block.BasicBlockModel;
import ghidra.program.model.block.CodeBlock;
import ghidra.program.model.block.CodeBlockIterator;
import ghidra.program.model.data.DataType;
import ghidra.program.model.data.Enum;
import ghidra.program.model.data.StringDataInstance;
import ghidra.program.model.data.Structure;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;

public class ExportCrusaderSemantics extends GhidraScript {
    private static final int DECOMPILE_TIMEOUT_SECONDS = 120;
    private String binaryHash;
    private Address imageBase;

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 2) throw new IllegalArgumentException("Expected export-dir binary-hash");
        File output = new File(args[0]);
        if (!output.isDirectory()) throw new IllegalArgumentException("Missing output directory " + output);
        binaryHash = args[1].toUpperCase();
        imageBase = currentProgram.getImageBase();
        exportFunctions(new File(output, "semantic-functions.jsonl"));
        exportGlobals(new File(output, "globals.jsonl"));
        exportTypes(new File(output, "data-types.jsonl"));
        exportRtti(new File(output, "rtti-vtables.jsonl"));
        exportDecompilation(new File(output, "semantic-decompiled-functions.c"), new File(output, "semantic-decompile-status.jsonl"));
        println("SEMANTIC_EXPORT_COMPLETE directory=" + output);
    }

    private void exportFunctions(File file) throws Exception {
        BasicBlockModel blocks = new BasicBlockModel(currentProgram);
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            FunctionIterator functions = currentProgram.getFunctionManager().getFunctions(true);
            while (functions.hasNext() && !monitor.isCancelled()) {
                Function function = functions.next();
                if (function.isExternal()) continue;
                MessageDigest raw = MessageDigest.getInstance("SHA-256");
                MessageDigest mnemonic = MessageDigest.getInstance("SHA-256");
                MessageDigest normalized = MessageDigest.getInstance("SHA-256");
                TreeSet<String> strings = new TreeSet<>();
                TreeSet<String> imports = new TreeSet<>();
                TreeSet<String> dataRvas = new TreeSet<>();
                InstructionIterator instructions = currentProgram.getListing().getInstructions(function.getBody(), true);
                while (instructions.hasNext()) {
                    Instruction instruction = instructions.next();
                    raw.update(instruction.getBytes());
                    digestText(mnemonic, instruction.getMnemonicString() + "\n");
                    StringBuilder normalizedInstruction = new StringBuilder(instruction.getMnemonicString());
                    for (int operand = 0; operand < instruction.getNumOperands(); operand++) {
                        normalizedInstruction.append(':').append(instruction.getOperandType(operand));
                    }
                    digestText(normalized, normalizedInstruction.append('\n').toString());
                    Reference[] references = instruction.getReferencesFrom();
                    for (Reference reference : references) {
                        Address target = reference.getToAddress();
                        if (target != null && target.isMemoryAddress() &&
                            target.getAddressSpace().equals(imageBase.getAddressSpace()) &&
                            reference.getReferenceType().isData() && !function.getBody().contains(target)) {
                            dataRvas.add(rva(target));
                        }
                        Data data = currentProgram.getListing().getDefinedDataAt(target);
                        if (data != null && data.hasStringValue()) {
                            StringDataInstance instance = StringDataInstance.getStringDataInstance(data);
                            String value = instance == null ? String.valueOf(data.getValue()) : instance.getStringValue();
                            if (value != null) strings.add(value);
                        }
                    }
                }
                int blockCount = 0;
                int edgeCount = 0;
                CodeBlockIterator blockIterator = blocks.getCodeBlocksContaining(function.getBody(), monitor);
                while (blockIterator.hasNext()) {
                    CodeBlock block = blockIterator.next();
                    blockCount++;
                    edgeCount += block.getNumDestinations(monitor);
                }
                List<Function> called = new ArrayList<>(function.getCalledFunctions(monitor));
                Collections.sort(called, (left, right) -> left.getEntryPoint().compareTo(right.getEntryPoint()));
                List<String> callees = new ArrayList<>();
                List<String> calleeRvas = new ArrayList<>();
                for (Function callee : called) {
                    callees.add(callee.getName(true));
                    calleeRvas.add(callee.isExternal() ? null : rva(callee.getEntryPoint()));
                    if (callee.isExternal()) imports.add(callee.getName(true));
                }
                String comment = function.getComment();
                String confidence = comment != null && comment.contains("[semantic:confirmed]") ? "confirmed" :
                    comment != null && comment.contains("[semantic:probable]") ? "probable" :
                    function.getName().startsWith("DLL_") ? "confirmed" : "candidate";
                writeLine(writer, "{\"binaryHash\":" + quote(binaryHash) +
                    ",\"address\":" + quote(hex(function.getEntryPoint())) + ",\"rva\":" + quote(rva(function.getEntryPoint())) +
                    ",\"name\":" + quote(function.getName()) + ",\"confidence\":" + quote(confidence) +
                    ",\"size\":" + function.getBody().getNumAddresses() + ",\"signature\":" + quote(function.getSignature().getPrototypeString()) +
                    ",\"comment\":" + quote(comment) + ",\"blockCount\":" + blockCount + ",\"edgeCount\":" + edgeCount +
                    ",\"rawHash\":" + quote(toHex(raw.digest())) + ",\"mnemonicHash\":" + quote(toHex(mnemonic.digest())) +
                    ",\"normalizedHash\":" + quote(toHex(normalized.digest())) + ",\"strings\":" + stringArray(strings) +
                    ",\"imports\":" + stringArray(imports) + ",\"dataRvas\":" + stringArray(dataRvas) + ",\"callees\":" + stringArray(callees) +
                    ",\"calleeRvas\":" + nullableStringArray(calleeRvas) + "}");
                count++;
            }
        }
        println("SEMANTIC_FUNCTIONS count=" + count);
    }

    private void exportGlobals(File file) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            SymbolIterator symbols = currentProgram.getSymbolTable().getAllSymbols(true);
            while (symbols.hasNext() && !monitor.isCancelled()) {
                Symbol symbol = symbols.next();
                Address address = symbol.getAddress();
                if (!address.isMemoryAddress() || currentProgram.getFunctionManager().getFunctionAt(address) != null || symbol.isExternal()) continue;
                int refs = 0;
                ReferenceIterator iterator = currentProgram.getReferenceManager().getReferencesTo(address);
                while (iterator.hasNext()) { iterator.next(); refs++; }
                if (refs == 0) continue;
                Data data = currentProgram.getListing().getDefinedDataAt(address);
                writeLine(writer, "{\"binaryHash\":" + quote(binaryHash) + ",\"address\":" + quote(hex(address)) +
                    ",\"rva\":" + quote(rva(address)) + ",\"name\":" + quote(symbol.getName(true)) +
                    ",\"dataType\":" + quote(data == null ? null : data.getDataType().getDisplayName()) +
                    ",\"referenceCount\":" + refs + "}");
                count++;
            }
        }
        println("SEMANTIC_GLOBALS count=" + count);
    }

    private void exportTypes(File file) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            Iterator<DataType> iterator = currentProgram.getDataTypeManager().getAllDataTypes();
            while (iterator.hasNext() && !monitor.isCancelled()) {
                DataType type = iterator.next();
                if (!(type instanceof Structure) && !(type instanceof Enum)) continue;
                writeLine(writer, "{\"binaryHash\":" + quote(binaryHash) + ",\"name\":" + quote(type.getName()) +
                    ",\"kind\":" + quote(type.getClass().getSimpleName()) + ",\"length\":" + type.getLength() +
                    ",\"category\":" + quote(type.getCategoryPath().getPath()) + ",\"declaration\":" + quote(type.toString()) +
                    ",\"sourcePath\":null}");
                count++;
            }
        }
        println("SEMANTIC_TYPES count=" + count);
    }

    private void exportRtti(File file) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            SymbolIterator symbols = currentProgram.getSymbolTable().getAllSymbols(true);
            while (symbols.hasNext() && !monitor.isCancelled()) {
                Symbol symbol = symbols.next();
                String lower = symbol.getName(true).toLowerCase();
                if (!(lower.contains("rtti") || lower.contains("vftable") || lower.contains("vtable") || lower.contains("type_info"))) continue;
                writeLine(writer, "{\"binaryHash\":" + quote(binaryHash) + ",\"address\":" + quote(hex(symbol.getAddress())) +
                    ",\"rva\":" + quote(rva(symbol.getAddress())) + ",\"name\":" + quote(symbol.getName(true)) + "}");
                count++;
            }
        }
        println("SEMANTIC_RTTI count=" + count);
    }

    private void exportDecompilation(File codeFile, File statusFile) throws Exception {
        DecompInterface decompiler = new DecompInterface();
        decompiler.toggleCCode(true);
        decompiler.openProgram(currentProgram);
        int attempted = 0, completed = 0, failed = 0;
        try (BufferedWriter code = writer(codeFile); BufferedWriter status = writer(statusFile)) {
            FunctionIterator functions = currentProgram.getFunctionManager().getFunctions(true);
            while (functions.hasNext() && !monitor.isCancelled()) {
                Function function = functions.next();
                if (function.isExternal()) continue;
                attempted++;
                DecompileResults result = decompiler.decompileFunction(function, DECOMPILE_TIMEOUT_SECONDS, monitor);
                boolean ok = result != null && result.decompileCompleted() && result.getDecompiledFunction() != null;
                if (ok) {
                    writeLine(code, "/* FUNCTION " + function.getName() + " VA=" + hex(function.getEntryPoint()) + " RVA=" + rva(function.getEntryPoint()) + " */");
                    writeCrLf(code, result.getDecompiledFunction().getC());
                    writeLine(code, "");
                    completed++;
                }
                else failed++;
                writeLine(status, "{\"binaryHash\":" + quote(binaryHash) + ",\"address\":" + quote(hex(function.getEntryPoint())) +
                    ",\"rva\":" + quote(rva(function.getEntryPoint())) + ",\"name\":" + quote(function.getName()) +
                    ",\"completed\":" + ok + ",\"message\":" + quote(result == null ? "null result" : result.getErrorMessage()) + "}");
            }
        }
        decompiler.dispose();
        println("SEMANTIC_DECOMPILATION attempted=" + attempted + " completed=" + completed + " failed=" + failed);
    }

    private String rva(Address address) {
        if (address == null || !address.getAddressSpace().equals(imageBase.getAddressSpace())) return null;
        return String.format("0x%X", address.subtract(imageBase));
    }
    private static String hex(Address address) { return address == null ? null : String.format("0x%X", address.getOffset()); }
    private static void digestText(MessageDigest digest, String value) { digest.update(value.getBytes(StandardCharsets.UTF_8)); }
    private static String toHex(byte[] bytes) { StringBuilder b = new StringBuilder(); for (byte value : bytes) b.append(String.format("%02X", value)); return b.toString(); }
    private static BufferedWriter writer(File file) throws Exception { return new BufferedWriter(new OutputStreamWriter(new FileOutputStream(file, false), StandardCharsets.UTF_8)); }
    private static void writeLine(BufferedWriter writer, String value) throws Exception { writer.write(value == null ? "" : value); writer.write("\r\n"); }
    private static void writeCrLf(BufferedWriter writer, String value) throws Exception { if (value == null) return; for (String line : value.replace("\r\n", "\n").replace('\r', '\n').split("\n", -1)) writeLine(writer, line); }
    private static String quote(String value) { if (value == null) return "null"; return "\"" + value.replace("\\", "\\\\").replace("\"", "\\\"").replace("\b", "\\b").replace("\f", "\\f").replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t") + "\""; }
    private static String stringArray(Iterable<String> values) { List<String> items = new ArrayList<>(); for (String value : values) items.add(quote(value)); return "[" + String.join(",", items) + "]"; }
    private static String nullableStringArray(Iterable<String> values) { List<String> items = new ArrayList<>(); for (String value : values) items.add(value == null ? "null" : quote(value)); return "[" + String.join(",", items) + "]"; }
}
