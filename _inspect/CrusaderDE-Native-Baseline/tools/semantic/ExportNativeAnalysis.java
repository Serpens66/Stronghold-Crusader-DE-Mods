// Read-only, resumable machine facts and High P-code. No labels or prototypes are edited.
//@category SerpsMods
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.security.MessageDigest;
import com.google.gson.*;
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.framework.Application;
import ghidra.program.model.address.*;
import ghidra.program.model.block.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.pcode.*;
import ghidra.program.model.symbol.*;

public class ExportNativeAnalysis extends GhidraScript {
    private final Gson gson = new GsonBuilder().disableHtmlEscaping().serializeNulls().create();
    private Address base;
    private String hash, profile;
    private Path output;
    private final Set<String> selected = new HashSet<>();
    private final Map<Address, Function> entries = new HashMap<>();
    private String rva(Address a) {
        return a != null && a.getAddressSpace().equals(base.getAddressSpace()) ? String.format("0x%X", a.subtract(base)) : null;
    }
    private Map<String,Object> obj(Object... pairs) {
        Map<String,Object> m = new LinkedHashMap<>();
        for (int i=0;i<pairs.length;i+=2) m.put((String)pairs[i], pairs[i+1]);
        return m;
    }
    private void save(Path p, Object value) throws Exception {
        Path tmp = p.resolveSibling(p.getFileName()+".tmp");
        Files.writeString(tmp, gson.toJson(value)+"\r\n", StandardCharsets.UTF_8);
        Files.move(tmp,p,StandardCopyOption.REPLACE_EXISTING,StandardCopyOption.ATOMIC_MOVE);
        Files.writeString(p.resolveSibling(p.getFileName()+".sha256"),fileDigest(p)+"\r\n",StandardCharsets.UTF_8);
    }
    private String fileDigest(Path p) throws Exception {
        byte[] digest=MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(p));
        StringBuilder text=new StringBuilder();for(byte b:digest)text.append(String.format("%02X",b));return text.toString();
    }
    private List<Object> ranges(AddressSetView set) {
        List<Object> result=new ArrayList<>();
        for (AddressRange range:set.getAddressRanges()) result.add(obj("start",rva(range.getMinAddress()),"endInclusive",rva(range.getMaxAddress())));
        return result;
    }
    private String opId(PcodeOp op) { return rva(op.getSeqnum().getTarget())+"#"+op.getSeqnum().getTime(); }
    private Object node(Varnode v) {
        if(v==null)return null;
        PcodeOp def=v.getDef();
        Integer ordinal=null;
        if(v instanceof VarnodeAST ast && ast.getHigh() instanceof HighParam p) ordinal=p.getSlot();
        return obj("space",v.getAddress().getAddressSpace().getName(),"offset",String.format("0x%X",v.getOffset()),
            "size",v.getSize(),"constant",v.isConstant(),"definition",def==null?null:opId(def),"parameterOrdinal",ordinal);
    }
    @Override protected void run() throws Exception {
        String[] args=getScriptArgs();
        if(args.length!=4)throw new IllegalArgumentException("Expected output hash selection-file profile");
        output=Path.of(args[0]); Files.createDirectories(output);
        hash=args[1];profile=args[3];base=currentProgram.getImageBase();
        if(!hash.equalsIgnoreCase(currentProgram.getExecutableSHA256()))throw new IllegalStateException("Ghidra Program hash mismatch");
        selected.addAll(Files.readAllLines(Path.of(args[2]),StandardCharsets.UTF_8));
        List<Function> functions=new ArrayList<>();
        for(Function f:currentProgram.getFunctionManager().getFunctions(true))if(!f.isExternal()){functions.add(f);entries.put(f.getEntryPoint(),f);}
        DecompInterface d=new DecompInterface();d.toggleCCode(false);d.toggleSyntaxTree(true);d.openProgram(currentProgram);
        BasicBlockModel model=new BasicBlockModel(currentProgram);
        int count=0,reused=0,flows=0;
        try {
            for(Function f:functions){
                monitor.checkCancelled(); String key=rva(f.getEntryPoint()); Path file=output.resolve(key.substring(2)+".json");
                boolean wantFlow=selected.contains(key);
                Path checksum=file.resolveSibling(file.getFileName()+".sha256");
                if(Files.exists(file)&&Files.exists(checksum)&&Files.readString(checksum).trim().equals(fileDigest(file))){
                    JsonObject old=JsonParser.parseString(Files.readString(file)).getAsJsonObject();
                    if(old.get("profile").getAsString().equals(profile)&&old.get("binaryHash").getAsString().equals(hash)&&
                       (!wantFlow || old.getAsJsonObject("flow").get("status").getAsString().equals("complete"))){reused++;count++;continue;}
                }
                Map<String,Object> packet=obj("binaryHash",hash,"profile",profile,"functionRva",key,"name",f.getName(),
                    "ranges",ranges(f.getBody()),"signature",f.getSignature().toString(),"signatureSource",f.getSignatureSource().toString());
                List<Object> instructions=new ArrayList<>(),blocks=new ArrayList<>();
                for(Instruction ins:currentProgram.getListing().getInstructions(f.getBody(),true)){
                    List<Object> refs=new ArrayList<>();
                    for(Reference ref:ins.getReferencesFrom())refs.add(obj("target",rva(ref.getToAddress()),"space",ref.getToAddress().getAddressSpace().getName(),
                        "type",ref.getReferenceType().toString(),"source",ref.getSource().toString(),"operand",ref.getOperandIndex()));
                    List<String> targets=new ArrayList<>();for(Address target:ins.getFlows())targets.add(rva(target));
                    byte[] bytes=ins.getBytes();StringBuilder hex=new StringBuilder();for(byte b:bytes)hex.append(String.format("%02X",b));
                    instructions.add(obj("rva",rva(ins.getAddress()),"bytes",hex.toString(),"length",ins.getLength(),"assembly",ins.toString(),
                        "flowType",ins.getFlowType().toString(),"targets",targets,"fallthrough",rva(ins.getFallThrough()),"references",refs));
                }
                CodeBlockIterator bi=model.getCodeBlocksContaining(f.getBody(),monitor);
                while(bi.hasNext()){
                    CodeBlock block=bi.next();List<Object> edges=new ArrayList<>();
                    CodeBlockReferenceIterator ei=block.getDestinations(monitor);
                    while(ei.hasNext()){CodeBlockReference e=ei.next();edges.add(obj("site",rva(e.getReferent()),"target",rva(e.getReference()),"type",e.getFlowType().toString()));}
                    blocks.add(obj("start",rva(block.getFirstStartAddress()),"ranges",ranges(block),"edges",edges));
                }
                packet.put("instructions",instructions);packet.put("blocks",blocks);
                packet.put("flow",obj("status","not-selected"));
                if(wantFlow){
                    DecompileResults result=d.decompileFunction(f,120,monitor);
                    if(!result.decompileCompleted()||result.getHighFunction()==null){packet.put("flow",obj("status","failed","error",result.getErrorMessage()));}
                    else{
                        HighFunction high=result.getHighFunction();List<Object> ops=new ArrayList<>(),params=new ArrayList<>(),switches=new ArrayList<>();
                        for(int i=0;i<high.getFunctionPrototype().getNumParams();i++){
                            HighSymbol p=high.getFunctionPrototype().getParam(i);
                            params.add(obj("ordinal",i,"name",p.getName(),"type",p.getDataType().getName(),"representative",node(p.getHighVariable()==null?null:p.getHighVariable().getRepresentative())));
                        }
                        Iterator<PcodeOpAST> oi=high.getPcodeOps();
                        while(oi.hasNext()){
                            PcodeOpAST op=oi.next();List<Object> inputs=new ArrayList<>();for(int i=0;i<op.getNumInputs();i++)inputs.add(node(op.getInput(i)));
                            String target=null;
                            if(op.getOpcode()==PcodeOp.CALL && op.getNumInputs()>0)target=rva(op.getInput(0).getAddress());
                            ops.add(obj("id",opId(op),"site",rva(op.getSeqnum().getTarget()),"opcode",op.getMnemonic(),"inputs",inputs,
                                "output",node(op.getOutput()),"dead",op.isDead(),"block",op.getParent()==null?null:op.getParent().getIndex(),"callTarget",target));
                        }
                        for(JumpTable table:high.getJumpTables()){
                            List<String> dest=new ArrayList<>();for(Address a:table.getCases())dest.add(rva(a));
                            switches.add(obj("switch",rva(table.getSwitchAddress()),"destinations",dest,"labelMapping","not-inferred"));
                        }
                        packet.put("flow",obj("status","complete","provenance","ghidra-high-pcode","parameters",params,"operations",ops,"switches",switches,
                            "limitations",List.of("Decompiler-derived; not original source or runtime evidence","Memory aliasing and indirect dispatch are unresolved","Recovered parameters are not a verified ABI")));
                        flows++;
                    }
                }
                save(file,packet);count++;
                if(count%25==0 || count==functions.size())println("ANALYSIS_PROGRESS "+count+"/"+functions.size()+" reused="+reused+" flows="+flows+" rva="+key);
            }
        }finally{d.dispose();}
        save(output.resolve("COMPLETE.json"),obj("binaryHash",hash,"profile",profile,"functions",count,"ghidraVersion",Application.getApplicationVersion()));
        println("ANALYSIS_COMPLETE functions="+count+" reused="+reused);
    }
}
