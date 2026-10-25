// Only confirmed, hash-bound function names/comments. No prototypes or code are changed.
//@category SerpsMods
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.security.MessageDigest;
import java.util.*;
import com.google.gson.*;
import ghidra.app.script.GhidraScript;
import ghidra.framework.options.Options;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.SourceType;

public class ImportNativeFunctionNames extends GhidraScript {
    private Function function(JsonObject row) {
        Address a = currentProgram.getImageBase().add(Long.parseUnsignedLong(row.get("rva").getAsString().substring(2),16));
        Function f = currentProgram.getFunctionManager().getFunctionAt(a);
        if (f == null || f.getBody().getNumAddresses() != row.get("size").getAsInt())
            throw new IllegalArgumentException("Not the exact function entry/body: " + row.get("rva"));
        return f;
    }
    private String fingerprint(Function f) throws Exception {
        return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest((f.getName()+"\n"+f.getComment()).getBytes(StandardCharsets.UTF_8)));
    }
    private void checkOwned(Options owned) throws Exception {
        if (!owned.contains("functions")) return;
        for (JsonElement e : JsonParser.parseString(owned.getString("functions","[]")).getAsJsonArray()) {
            JsonObject row=e.getAsJsonObject();
            if (!fingerprint(function(row)).equals(owned.getString(row.get("id").getAsString(),"")))
                throw new IllegalStateException("Manual function annotation changed: " + row.get("id"));
        }
    }
    @Override protected void run() throws Exception {
        String[] args=getScriptArgs();
        if (args.length!=3) throw new IllegalArgumentException("Expected plan.json apply|verify|guardtest report.json");
        JsonObject plan=JsonParser.parseString(Files.readString(new File(args[0]).toPath(),StandardCharsets.UTF_8)).getAsJsonObject();
        if (!plan.get("nativeHash").getAsString().equalsIgnoreCase(currentProgram.getExecutableSHA256()))
            throw new IllegalArgumentException("Function knowledge hash does not match Ghidra program");
        JsonArray rows=plan.has("functions") ? plan.getAsJsonArray("functions") : new JsonArray();
        boolean apply=args[1].equals("apply"), guard=args[1].equals("guardtest");
        if (!apply && !guard && !args[1].equals("verify")) throw new IllegalArgumentException("Unknown mode");
        int transaction=currentProgram.startTransaction("NativeKnowledge function annotations");
        boolean commit=false;
        try {
            Options owned=currentProgram.getOptions("NativeKnowledge function ownership");
            if (guard && rows.size()>0) {
                function(rows.get(0).getAsJsonObject()).setComment("Simulated manual change, never saved.");
                boolean refused=false;
                try { checkOwned(owned); } catch (IllegalStateException expected) { refused=true; }
                if (!refused) throw new IllegalStateException("Function annotation guard failed");
                Files.writeString(new File(args[2]).toPath(),"{\"status\":\"ok\",\"manualFunctionChangeRefused\":true}\r\n",StandardCharsets.UTF_8);
                return;
            }
            if (apply) checkOwned(owned);
            Map<String,JsonObject> previous=new HashMap<>();
            if (owned.contains("functions")) for (JsonElement e:JsonParser.parseString(owned.getString("functions","[]")).getAsJsonArray())
                previous.put(e.getAsJsonObject().get("id").getAsString(),e.getAsJsonObject());
            Set<String> ids=new HashSet<>();
            for (JsonElement e:rows) {
                JsonObject row=e.getAsJsonObject(); Function f=function(row);
                if (!ids.add(row.get("id").getAsString())) throw new IllegalArgumentException("Duplicate identity");
                JsonObject prior=previous.get(row.get("id").getAsString());
                if (prior!=null && (!prior.get("rva").equals(row.get("rva")) || !prior.get("rawHash").equals(row.get("rawHash"))))
                    throw new IllegalStateException("Function identity cannot be rebound within one native image");
                byte[] bytes=new byte[row.get("size").getAsInt()];
                currentProgram.getMemory().getBytes(f.getEntryPoint(),bytes);
                String hash=HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(bytes));
                if (!hash.equalsIgnoreCase(row.get("rawHash").getAsString())) throw new IllegalArgumentException("Function bytes differ: "+row.get("rva"));
                if (apply && !owned.contains(row.get("id").getAsString()) && !f.getName().equals(row.get("name").getAsString()) && f.getSymbol().getSource()!=SourceType.DEFAULT)
                    throw new IllegalStateException("Existing function name protected: "+f.getName());
                for (var symbol:currentProgram.getSymbolTable().getGlobalSymbols(row.get("name").getAsString()))
                    if (!symbol.getAddress().equals(f.getEntryPoint())) throw new IllegalStateException("Function name collision");
            }
            if (owned.contains("functions")) for (JsonElement e:JsonParser.parseString(owned.getString("functions","[]")).getAsJsonArray())
                if (!ids.contains(e.getAsJsonObject().get("id").getAsString())) throw new IllegalStateException("Removed function requires explicit migration");
            int changed=0;
            for (JsonElement e:rows) {
                JsonObject row=e.getAsJsonObject(); Function f=function(row);
                String id=row.get("id").getAsString(), begin="[NativeKnowledge:function "+id+"]", end="[/NativeKnowledge:function "+id+"]";
                String proof="[semantic:confirmed] Evidence curated:confirmed:"+row.get("nameOrigin").getAsString()+" from "+row.get("claimId").getAsString()+":0; matched "+f.getEntryPoint();
                String block=begin+"\n"+proof+"\n"+row.get("comment").getAsString()+"\n"+end;
                String current=f.getComment()==null?"":f.getComment();
                String expected=current;
                int start=current.indexOf(begin);
                if (start>=0) {
                    int finish=current.indexOf(end,start);
                    if (finish<0) throw new IllegalStateException("Incomplete owned comment block");
                    expected=current.substring(0,start)+block+current.substring(finish+end.length());
                } else expected=current+(current.isEmpty()?"":"\n")+block;
                if (apply && (!f.getName().equals(row.get("name").getAsString()) || !current.equals(expected))) {
                    f.setName(row.get("name").getAsString(),SourceType.USER_DEFINED); f.setComment(expected); changed++;
                }
                if (!f.getName().equals(row.get("name").getAsString()) || f.getComment()==null || !f.getComment().contains(block))
                    throw new IllegalStateException("Function annotation verification failed: "+id);
                if (apply) owned.setString(id,fingerprint(f));
            }
            if (apply) owned.setString("functions",rows.toString());
            JsonObject report=new JsonObject(); report.addProperty("status","ok");report.addProperty("functions",rows.size());report.addProperty("changed",changed);
            Files.writeString(new File(args[2]).toPath(),new GsonBuilder().setPrettyPrinting().create().toJson(report).replace("\n","\r\n")+"\r\n",StandardCharsets.UTF_8);
            println("NATIVE_FUNCTION_NAMES_COMPLETE functions="+rows.size()+" changed="+changed);
            commit=true;
        } finally { currentProgram.endTransaction(transaction,commit); }
    }
}
