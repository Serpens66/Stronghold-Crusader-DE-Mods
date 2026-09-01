// Validate that a saved CrusaderDE Ghidra project can be reopened read-only.
//@category SerpsMods

import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.util.DefinedDataIterator;

public class ValidateCrusaderBaseline extends GhidraScript {
    @Override
    protected void run() throws Exception {
        long functions = currentProgram.getFunctionManager().getFunctionCount();
        long strings = 0;
        for (Data ignored : DefinedDataIterator.byDataInstance(currentProgram, Data::hasStringValue)) {
            strings++;
        }
        long xrefs = 0;
        ReferenceIterator references = currentProgram.getReferenceManager()
            .getReferenceIterator(currentProgram.getMinAddress());
        while (references.hasNext()) {
            references.next();
            xrefs++;
        }
        Function entryFunction = currentProgram.getFunctionManager()
            .getFunctionContaining(currentProgram.getImageBase());
        println("VALIDATION_OK program=" + currentProgram.getName() +
            " imageBase=" + currentProgram.getImageBase() +
            " minAddress=" + currentProgram.getMinAddress() +
            " maxAddress=" + currentProgram.getMaxAddress() +
            " functions=" + functions +
            " strings=" + strings +
            " xrefs=" + xrefs +
            " imageBaseFunction=" + (entryFunction == null ? "none" : entryFunction.getName()));
    }
}
