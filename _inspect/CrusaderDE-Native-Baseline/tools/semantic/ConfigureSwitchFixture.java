// Only for the source-owned static fixture. Never apply to a game/reference project.
//@category SerpsMods
import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.IntegerDataType;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.SourceType;

public class ConfigureSwitchFixture extends GhidraScript {
    @Override protected void run() throws Exception {
        String[] args=getScriptArgs();
        if(args.length!=1 || !args[0].equalsIgnoreCase(currentProgram.getExecutableSHA256()) || !currentProgram.getName().equals("switch.exe"))
            throw new IllegalArgumentException("Source-owned fixture identity required");
        Function function=currentProgram.getFunctionManager().getFunctionAt(currentProgram.getImageBase().add(0x1000));
        if(function==null)throw new IllegalStateException("Fixture entry not analyzed");
        function.setReturnType(IntegerDataType.dataType,SourceType.USER_DEFINED);
        function.replaceParameters(Function.FunctionUpdateType.DYNAMIC_STORAGE_ALL_PARAMS,true,SourceType.USER_DEFINED,
            new ParameterImpl("selector",IntegerDataType.dataType,currentProgram));
    }
}
