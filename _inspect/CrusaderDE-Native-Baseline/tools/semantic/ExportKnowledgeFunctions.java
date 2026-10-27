// Refresh name-dependent exports and decompile only the annotated functions and their callers.
//@category SerpsMods
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;
import com.google.gson.*;
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;

public class ExportKnowledgeFunctions extends GhidraScript {
    private String rva(Function f) { return String.format("0x%X",f.getEntryPoint().subtract(currentProgram.getImageBase())); }
    private String crlf(String text) { return text.replace("\r\n","\n").replace('\r','\n').replace("\n","\r\n"); }
    @Override protected void run() throws Exception {
        String[] args=getScriptArgs();
        if (args.length!=3) throw new IllegalArgumentException("Expected plan.json export-dir report.json");
        JsonObject plan=JsonParser.parseString(Files.readString(Path.of(args[0]),StandardCharsets.UTF_8)).getAsJsonObject();
        String hash=plan.get("nativeHash").getAsString();
        if (!hash.equalsIgnoreCase(currentProgram.getExecutableSHA256())) throw new IllegalArgumentException("Program hash mismatch");
        Path output=Path.of(args[1]);
        Path codePath=output.resolve("semantic-decompiled-functions.c"), statusPath=output.resolve("semantic-decompile-status.jsonl");
        String oldCode=Files.readString(codePath,StandardCharsets.UTF_8);
        Matcher matcher=Pattern.compile("(?m)^/\\* FUNCTION .*? RVA=(0x[0-9A-Fa-f]+) \\*/\\r?$").matcher(oldCode);
        List<Integer> starts=new ArrayList<>();List<String> keys=new ArrayList<>();
        while(matcher.find()) { starts.add(matcher.start());keys.add("0x"+Long.toHexString(Long.parseUnsignedLong(matcher.group(1).substring(2),16)).toUpperCase()); }
        if(starts.isEmpty()) throw new IllegalStateException("No existing function blocks");
        Map<String,String> chunks=new LinkedHashMap<>();
        for(int i=0;i<keys.size();i++) chunks.put(keys.get(i),oldCode.substring(starts.get(i),i+1<starts.size()?starts.get(i+1):oldCode.length()));
        Map<String,JsonObject> status=new LinkedHashMap<>();
        for(String line:Files.readAllLines(statusPath,StandardCharsets.UTF_8)) if(!line.isBlank()) {
            JsonObject row=JsonParser.parseString(line).getAsJsonObject();status.put(row.get("rva").getAsString(),row);
        }
        Set<Function> selected=new TreeSet<>(Comparator.comparing(Function::getEntryPoint));
        if(plan.has("functions")) for(JsonElement e:plan.getAsJsonArray("functions")) {
            Address address=currentProgram.getImageBase().add(Long.parseUnsignedLong(e.getAsJsonObject().get("rva").getAsString().substring(2),16));
            Function f=currentProgram.getFunctionManager().getFunctionAt(address);
            if(f==null) throw new IllegalStateException("Missing annotated function");
            selected.add(f);selected.addAll(f.getCallingFunctions(monitor));
        }
        DecompInterface decompiler=new DecompInterface();decompiler.openProgram(currentProgram);
        try {
            for(Function f:selected) {
                monitor.checkCancelled();
                DecompileResults result=decompiler.decompileFunction(f,60,monitor);
                if(result==null || !result.decompileCompleted() || result.getDecompiledFunction()==null)
                    throw new IllegalStateException("Targeted decompilation failed: "+rva(f));
                String key=rva(f);
                chunks.put(key,"/* FUNCTION "+f.getName()+" VA=0x"+f.getEntryPoint().toString().toUpperCase()+" RVA="+key+" */\r\n\r\n"+crlf(result.getDecompiledFunction().getC())+"\r\n\r\n");
                JsonObject row=status.get(key);
                if(row==null) throw new IllegalStateException("Missing original status row: "+key);
                row.addProperty("name",f.getName());row.addProperty("completed",true);row.addProperty("message",result.getErrorMessage());
                println("KNOWLEDGE_FUNCTION_EXPORT "+key+" "+f.getName());
            }
        } finally { decompiler.dispose(); }
        // No whole-program reanalysis and no regeneration of historical machine facts.
        runScript("ExportCrusaderSemantics.java",new String[]{args[1],hash,"metadata-only"});
        StringBuilder code=new StringBuilder(oldCode.substring(0,starts.get(0)));
        chunks.entrySet().stream().sorted(Comparator.comparingLong(e->Long.parseUnsignedLong(e.getKey().substring(2),16))).forEach(e->code.append(e.getValue()));
        StringBuilder statuses=new StringBuilder();Gson gson=new Gson();
        for(JsonObject row:status.values()) statuses.append(gson.toJson(row)).append("\r\n");
        Files.writeString(codePath,code.toString(),StandardCharsets.UTF_8);
        Files.writeString(statusPath,statuses.toString(),StandardCharsets.UTF_8);
        JsonObject report=new JsonObject();report.addProperty("status","ok");report.addProperty("refreshedFunctions",selected.size());
        JsonArray refreshed=new JsonArray();for(Function f:selected)refreshed.add(rva(f));report.add("refreshedRvas",refreshed);
        Files.writeString(Path.of(args[2]),new GsonBuilder().setPrettyPrinting().create().toJson(report).replace("\n","\r\n")+"\r\n",StandardCharsets.UTF_8);
        println("KNOWLEDGE_FUNCTION_EXPORT_COMPLETE count="+selected.size());
    }
}
