// Read-only, resumable machine facts and High P-code. No labels or prototypes are edited.
//@category SerpsMods
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.security.MessageDigest;
import java.math.BigInteger;
import com.google.gson.*;
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.framework.Application;
import ghidra.program.model.address.*;
import ghidra.program.model.block.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.pcode.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.data.*;

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
        HighVariable h=v.getHigh();DataType type=h==null?null:h.getDataType();
        return obj("space",v.getAddress().getAddressSpace().getName(),"offset",String.format("0x%X",v.getOffset()),
            "size",v.getSize(),"constant",v.isConstant(),"definition",def==null?null:opId(def),"parameterOrdinal",ordinal,
            "typePath",type==null?null:type.getPathName(),"pointerTargetType",type instanceof Pointer p && p.getDataType()!=null?p.getDataType().getPathName():null);
    }
    private List<Object> typeFacts() {
        List<Object> result=new ArrayList<>();
        Iterator<DataType> it=currentProgram.getDataTypeManager().getAllDataTypes();
        while(it.hasNext()) {
            DataType t=it.next();Map<String,Object> row=obj("path",t.getPathName(),"name",t.getName(),"size",t.getLength(),
                "alignment",t.getAlignment(),"kind",t.getClass().getSimpleName(),"authority","ghidra-database-layout");
            if(t instanceof Composite c) {
                List<Object> fields=new ArrayList<>();DataTypeComponent[] components=c.getDefinedComponents();
                for(int i=0;i<Math.min(components.length,20000);i++) {
                    DataTypeComponent part=components[i];
                    fields.add(obj("name",part.getFieldName(),"offset",part.getOffset(),"size",part.getLength(),"typePath",part.getDataType().getPathName(),
                        "bitField",part.isBitFieldComponent(),"ordinal",part.getOrdinal()));
                }
                row.put("fields",fields);row.put("totalFields",components.length);row.put("truncated",components.length>fields.size());
            } else if(t instanceof Pointer p) row.put("target",p.getDataType()==null?null:p.getDataType().getPathName());
            else if(t instanceof Array a) row.put("array",obj("count",a.getNumElements(),"elementSize",a.getElementLength(),"typePath",a.getDataType().getPathName()));
            else if(t instanceof TypeDef td) row.put("target",td.getBaseDataType().getPathName());
            else if(t instanceof ghidra.program.model.data.Enum en) {
                List<Object> values=new ArrayList<>();String[] names=en.getNames();
                for(int i=0;i<Math.min(names.length,20000);i++)values.add(obj("name",names[i],"value",Long.toString(en.getValue(names[i]))));
                row.put("values",values);row.put("totalValues",names.length);row.put("truncated",names.length>values.size());
            }
            result.add(row);
        }
        return result;
    }
    private List<ClangCaseToken> caseTokens(ClangNode node) {
        List<ClangCaseToken> result=new ArrayList<>();
        if(node==null)return result;
        ArrayDeque<ClangNode> queue=new ArrayDeque<>();queue.push(node);
        while(!queue.isEmpty()) {
            ClangNode next=queue.pop();if(next instanceof ClangCaseToken token)result.add(token);
            for(int i=next.numChildren()-1;i>=0;i--)queue.push(next.Child(i));
        }
        return result;
    }
    private Map<String,Object> switchFacts(JumpTable table,List<ClangCaseToken> tokens) {
        List<String> destinations=new ArrayList<>();for(Address a:table.getCases())destinations.add(rva(a));
        List<Object> mappings=new ArrayList<>(),unknown=new ArrayList<>(),data=new ArrayList<>();Set<String> defaults=new LinkedHashSet<>();
        for(ClangCaseToken token:tokens) {
            PcodeOp caseOp=token.getPcodeOp();PcodeBlockBasic block=caseOp==null?null:caseOp.getParent();
            if(block==null)continue;
            List<PcodeOp> dispatches=new ArrayList<>();
            for(int i=0;i<block.getInSize();i++) {
                PcodeBlock predecessor=block.getIn(i);
                if(predecessor instanceof PcodeBlockBasic basic) {
                    PcodeOp last=basic.getLastOp();if(last!=null&&last.getOpcode()==PcodeOp.BRANCHIND&&!dispatches.contains(last))dispatches.add(last);
                }
            }
            boolean refers=dispatches.stream().anyMatch(op->op.getSeqnum().getTarget().equals(table.getSwitchAddress()));
            if(!refers)continue;
            String text=token.getText().trim(),target=rva(block.getStart());
            Map<String,Object> observation=obj("token",text,"encodedUnsignedValue",Long.toUnsignedString(token.getValue()),"target",target);
            if(dispatches.size()!=1 || !destinations.contains(target)) {observation.put("reason","ambiguous dispatch or case target");unknown.add(observation);continue;}
            if(text.equals("default")) {defaults.add(target);continue;}
            String numeric=text.replaceFirst("(?i)[ul]+$","");
            try {
                boolean negative=numeric.startsWith("-");String magnitude=negative?numeric.substring(1):numeric;
                int radix=magnitude.startsWith("0x")||magnitude.startsWith("0X")?16:10;
                if(radix==16)magnitude=magnitude.substring(2);
                if(!magnitude.matches(radix==16?"[0-9a-fA-F]+":"[0-9]+"))throw new NumberFormatException();
                BigInteger value=new BigInteger(magnitude,radix);if(negative)value=value.negate();
                BigInteger encoded=new BigInteger(Long.toUnsignedString(token.getValue()));
                // Clang case markup encodes the magnitude of a printed negative literal.
                // It is not the selector's two's-complement bit pattern (verified by signed fixture).
                boolean agrees=value.abs().equals(encoded);
                if(!agrees||value.abs().compareTo(BigInteger.valueOf(9007199254740991L))>0)throw new NumberFormatException();
                mappings.add(obj("value",value.longValue(),"target",target,"token",text,"authority","typed-decompiler-case-token"));
            } catch(NumberFormatException e) {observation.put("reason","nonliteral, inconsistent or nonexact numeric case");unknown.add(observation);}
        }
        List<Object> rawMappings=new ArrayList<>();Integer[] labels=table.getLabelValues();
        if(labels.length==destinations.size())for(int i=0;i<labels.length;i++)rawMappings.add(obj("encodedLabel32",String.format("0x%X",labels[i]),"target",destinations.get(i),"authority","ghidra-jumptable-observed-pair; original signedness/width not inferred"));
        else unknown.add(obj("reason","raw label and destination counts differ; positional bindings withheld","labels",labels.length,"destinations",destinations.size()));
        for(JumpTable.LoadTable load:table.getLoadTables())data.add(obj("address",rva(load.getAddress()),"entrySize",load.getSize(),"entryCount",load.getNum(),"authority","decompiler-load-table"));
        return obj("switch",rva(table.getSwitchAddress()),"destinations",destinations,"rawLabels",table.getLabelValues(),
            "mappings",mappings,"rawMappings",rawMappings,"defaultTargets",defaults,"unknownLabels",unknown,"dataSources",data,
            "labelMapping","typed-token-observations","defaultCoverage",defaults.isEmpty()?"not-observed":"observed",
            "limitations",List.of("Decompiler reconstruction, not runtime dispatch proof","Raw label pairs require equal counts; unsigned 32-bit labels retain original-width ambiguity","Load tables are not assigned to individual cases"));
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
        save(output.resolve("TYPES.json"),obj("binaryHash",hash,"profile",profile,"types",typeFacts()));
        DecompInterface d=new DecompInterface();d.toggleCCode(true);d.toggleSyntaxTree(true);d.toggleJumpLoads(true);d.openProgram(currentProgram);
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
                    "ranges",ranges(f.getBody()),"signature",f.getSignature().toString(),"signatureSource",f.getSignatureSource().toString(),
                    "variadic",f.hasVarArgs(),"callingConvention",f.getCallingConventionName());
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
                        HighFunction high=result.getHighFunction();List<Object> ops=new ArrayList<>(),params=new ArrayList<>(),switches=new ArrayList<>(),flowBlocks=new ArrayList<>();
                        Set<PcodeOp> members=Collections.newSetFromMap(new IdentityHashMap<>());
                        for(PcodeBlockBasic block:high.getBasicBlocks()) {
                            Iterator<PcodeOp> member=block.getIterator();while(member.hasNext())members.add(member.next());
                            List<Integer> successors=new ArrayList<>();for(int i=0;i<block.getOutSize();i++)successors.add(block.getOut(i).getIndex());
                            flowBlocks.add(obj("id",block.getIndex(),"start",rva(block.getStart()),"stop",rva(block.getStop()),"successors",successors));
                        }
                        for(int i=0;i<high.getFunctionPrototype().getNumParams();i++){
                            HighSymbol p=high.getFunctionPrototype().getParam(i);
                            DataType paramType=p.getDataType();
                            params.add(obj("ordinal",i,"name",p.getName(),"type",paramType.getName(),"typePath",paramType.getPathName(),
                                "pointerTargetType",paramType instanceof Pointer pointer&&pointer.getDataType()!=null?pointer.getDataType().getPathName():null,
                                "representative",node(p.getHighVariable()==null?null:p.getHighVariable().getRepresentative())));
                        }
                        Iterator<PcodeOpAST> oi=high.getPcodeOps();
                        while(oi.hasNext()){
                            PcodeOpAST op=oi.next();List<Object> inputs=new ArrayList<>();for(int i=0;i<op.getNumInputs();i++)inputs.add(node(op.getInput(i)));
                            String target=null;
                            if(op.getOpcode()==PcodeOp.CALL && op.getNumInputs()>0)target=rva(op.getInput(0).getAddress());
                            ops.add(obj("id",opId(op),"site",rva(op.getSeqnum().getTarget()),"opcode",op.getMnemonic(),"inputs",inputs,
                                "output",node(op.getOutput()),"dead",op.isDead(),"blockMembership",members.contains(op),"block",op.getParent()==null?null:op.getParent().getIndex(),"callTarget",target));
                        }
                        List<ClangCaseToken> tokens=caseTokens(result.getCCodeMarkup());
                        for(JumpTable table:high.getJumpTables())switches.add(switchFacts(table,tokens));
                        packet.put("flow",obj("status","complete","provenance","ghidra-high-pcode","parameters",params,"operations",ops,"switches",switches,"blocks",flowBlocks,
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
