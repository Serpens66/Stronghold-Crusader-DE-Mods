// Export a Ghidra analysis into deterministic, line-oriented files for later searches.
//@category SerpsMods

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.data.StringDataInstance;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;
import ghidra.program.util.DefinedDataIterator;

public class ExportCrusaderBaseline extends GhidraScript {
    private static final int DECOMPILE_TIMEOUT_SECONDS = 120;

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 1) {
            throw new IllegalArgumentException("Expected one argument: absolute export directory");
        }

        File exportDir = new File(args[0]);
        if (!exportDir.isDirectory()) {
            throw new IllegalArgumentException("Export directory does not exist: " + exportDir);
        }

        Address imageBase = currentProgram.getImageBase();
        exportFunctions(new File(exportDir, "functions.jsonl"), imageBase);
        exportReferences(new File(exportDir, "xrefs.jsonl"), imageBase);
        exportStrings(new File(exportDir, "strings.jsonl"), imageBase);
        exportImports(new File(exportDir, "imports.jsonl"), imageBase);
        exportExports(new File(exportDir, "exports.jsonl"), imageBase);
        exportSections(new File(exportDir, "sections.json"), imageBase);
        exportDecompilation(
            new File(exportDir, "decompiled-functions.c"),
            new File(exportDir, "decompile-status.jsonl"),
            imageBase);
        println("EXPORT_COMPLETE directory=" + exportDir.getAbsolutePath());
    }

    private void exportFunctions(File file, Address imageBase) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            FunctionIterator functions = currentProgram.getFunctionManager().getFunctions(true);
            while (functions.hasNext() && !monitor.isCancelled()) {
                Function function = functions.next();
                Address entry = function.getEntryPoint();
                writeLine(writer,
                    "{\"address\":" + quote(hex(entry)) +
                    ",\"rva\":" + quote(rva(entry, imageBase)) +
                    ",\"name\":" + quote(function.getName()) +
                    ",\"namespace\":" + quote(function.getParentNamespace().getName(true)) +
                    ",\"size\":" + function.getBody().getNumAddresses() +
                    ",\"signature\":" + quote(function.getSignature().getPrototypeString()) +
                    ",\"callingConvention\":" + quote(function.getCallingConventionName()) +
                    ",\"thunk\":" + function.isThunk() +
                    ",\"external\":" + function.isExternal() + "}");
                count++;
            }
        }
        println("EXPORT_FUNCTIONS count=" + count);
    }

    private void exportReferences(File file, Address imageBase) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            ReferenceIterator references = currentProgram.getReferenceManager()
                .getReferenceIterator(currentProgram.getMinAddress());
            while (references.hasNext() && !monitor.isCancelled()) {
                Reference reference = references.next();
                Address from = reference.getFromAddress();
                Address to = reference.getToAddress();
                Function owner = currentProgram.getFunctionManager().getFunctionContaining(from);
                writeLine(writer,
                    "{\"fromAddress\":" + quote(hex(from)) +
                    ",\"fromRva\":" + quote(rva(from, imageBase)) +
                    ",\"toAddress\":" + quote(hex(to)) +
                    ",\"toRva\":" + quote(rva(to, imageBase)) +
                    ",\"type\":" + quote(reference.getReferenceType().getName()) +
                    ",\"sourceFunction\":" + quote(owner == null ? null : owner.getName()) + "}");
                count++;
            }
        }
        println("EXPORT_XREFS count=" + count);
    }

    private void exportStrings(File file, Address imageBase) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            for (Data data : DefinedDataIterator.byDataInstance(currentProgram, Data::hasStringValue)) {
                if (monitor.isCancelled()) {
                    break;
                }
                StringDataInstance instance = StringDataInstance.getStringDataInstance(data);
                String value = instance == null ? String.valueOf(data.getValue()) : instance.getStringValue();
                int xrefCount = 0;
                ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(data.getAddress());
                while (refs.hasNext()) {
                    refs.next();
                    xrefCount++;
                }
                writeLine(writer,
                    "{\"address\":" + quote(hex(data.getAddress())) +
                    ",\"rva\":" + quote(rva(data.getAddress(), imageBase)) +
                    ",\"length\":" + data.getLength() +
                    ",\"encoding\":" + quote(data.getDataType().getDisplayName()) +
                    ",\"value\":" + quote(value) +
                    ",\"xrefCount\":" + xrefCount + "}");
                count++;
            }
        }
        println("EXPORT_STRINGS count=" + count);
    }

    private void exportImports(File file, Address imageBase) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            SymbolIterator symbols = currentProgram.getSymbolTable().getExternalSymbols();
            while (symbols.hasNext() && !monitor.isCancelled()) {
                Symbol symbol = symbols.next();
                Address address = symbol.getAddress();
                writeLine(writer,
                    "{\"address\":" + quote(hex(address)) +
                    ",\"rva\":" + quote(rva(address, imageBase)) +
                    ",\"name\":" + quote(symbol.getName()) +
                    ",\"namespace\":" + quote(symbol.getParentNamespace().getName(true)) + "}");
                count++;
            }
        }
        println("EXPORT_IMPORTS count=" + count);
    }

    private void exportExports(File file, Address imageBase) throws Exception {
        int count = 0;
        try (BufferedWriter writer = writer(file)) {
            for (Address address : currentProgram.getSymbolTable().getExternalEntryPointIterator()) {
                Symbol symbol = currentProgram.getSymbolTable().getPrimarySymbol(address);
                writeLine(writer,
                    "{\"address\":" + quote(hex(address)) +
                    ",\"rva\":" + quote(rva(address, imageBase)) +
                    ",\"name\":" + quote(symbol == null ? null : symbol.getName()) + "}");
                count++;
            }
        }
        println("EXPORT_EXPORTS count=" + count);
    }

    private void exportSections(File file, Address imageBase) throws Exception {
        MemoryBlock[] blocks = currentProgram.getMemory().getBlocks();
        try (BufferedWriter writer = writer(file)) {
            writeLine(writer, "[");
            for (int i = 0; i < blocks.length; i++) {
                MemoryBlock block = blocks[i];
                String comma = i + 1 < blocks.length ? "," : "";
                writeLine(writer,
                    "  {\"name\":" + quote(block.getName()) +
                    ",\"startAddress\":" + quote(hex(block.getStart())) +
                    ",\"startRva\":" + quote(rva(block.getStart(), imageBase)) +
                    ",\"endAddress\":" + quote(hex(block.getEnd())) +
                    ",\"size\":" + block.getSize() +
                    ",\"read\":" + block.isRead() +
                    ",\"write\":" + block.isWrite() +
                    ",\"execute\":" + block.isExecute() +
                    ",\"initialized\":" + block.isInitialized() + "}" + comma);
            }
            writeLine(writer, "]");
        }
        println("EXPORT_SECTIONS count=" + blocks.length);
    }

    private void exportDecompilation(File codeFile, File statusFile, Address imageBase) throws Exception {
        int attempted = 0;
        int completed = 0;
        int failed = 0;
        DecompInterface decompiler = new DecompInterface();
        decompiler.toggleCCode(true);
        decompiler.toggleSyntaxTree(true);
        if (!decompiler.openProgram(currentProgram)) {
            throw new IllegalStateException("Decompiler failed to open current program");
        }

        try (BufferedWriter codeWriter = writer(codeFile);
             BufferedWriter statusWriter = writer(statusFile)) {
            FunctionIterator functions = currentProgram.getFunctionManager().getFunctions(true);
            while (functions.hasNext() && !monitor.isCancelled()) {
                Function function = functions.next();
                if (function.isExternal()) {
                    continue;
                }
                attempted++;
                Address entry = function.getEntryPoint();
                DecompileResults results = decompiler.decompileFunction(
                    function, DECOMPILE_TIMEOUT_SECONDS, monitor);
                boolean ok = results != null && results.decompileCompleted() &&
                    results.getDecompiledFunction() != null;
                String message = results == null ? "null result" : results.getErrorMessage();
                if (ok) {
                    writeLine(codeWriter, "/* FUNCTION " + function.getName() +
                        " VA=" + hex(entry) + " RVA=" + rva(entry, imageBase) + " */");
                    writeTextWithCrLf(codeWriter, results.getDecompiledFunction().getC());
                    writeLine(codeWriter, "");
                    completed++;
                }
                else {
                    failed++;
                }
                writeLine(statusWriter,
                    "{\"address\":" + quote(hex(entry)) +
                    ",\"rva\":" + quote(rva(entry, imageBase)) +
                    ",\"name\":" + quote(function.getName()) +
                    ",\"completed\":" + ok +
                    ",\"message\":" + quote(message) + "}");
            }
        }
        decompiler.dispose();
        println("EXPORT_DECOMPILATION attempted=" + attempted +
            " completed=" + completed + " failed=" + failed);
    }

    private static BufferedWriter writer(File file) throws Exception {
        return new BufferedWriter(new OutputStreamWriter(
            new FileOutputStream(file, false), StandardCharsets.UTF_8));
    }

    private static void writeLine(BufferedWriter writer, String value) throws Exception {
        writer.write(value == null ? "" : value);
        writer.write("\r\n");
    }

    private static void writeTextWithCrLf(BufferedWriter writer, String value) throws Exception {
        if (value == null || value.isEmpty()) {
            return;
        }
        String normalized = value.replace("\r\n", "\n").replace('\r', '\n');
        String[] lines = normalized.split("\n", -1);
        for (String line : lines) {
            writeLine(writer, line);
        }
    }

    private static String hex(Address address) {
        return address == null ? null : String.format("0x%X", address.getOffset());
    }

    private static String rva(Address address, Address imageBase) {
        if (address == null || imageBase == null ||
            !address.getAddressSpace().equals(imageBase.getAddressSpace())) {
            return null;
        }
        return String.format("0x%X", address.subtract(imageBase));
    }

    private static String quote(String value) {
        if (value == null) {
            return "null";
        }
        StringBuilder builder = new StringBuilder(value.length() + 16);
        builder.append('"');
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            switch (c) {
                case '"': builder.append("\\\""); break;
                case '\\': builder.append("\\\\"); break;
                case '\b': builder.append("\\b"); break;
                case '\f': builder.append("\\f"); break;
                case '\n': builder.append("\\n"); break;
                case '\r': builder.append("\\r"); break;
                case '\t': builder.append("\\t"); break;
                default:
                    if (c < 0x20) {
                        builder.append(String.format("\\u%04X", (int)c));
                    }
                    else {
                        builder.append(c);
                    }
            }
        }
        builder.append('"');
        return builder.toString();
    }
}
