# Original testmod comparison

PASS: CaptureAdapterEmitter.cs identical except namespace.
PASS: NativeDefinition.cs identical except namespace.
PASS: CaptureDecision.Apply identical; only main/feature switch conjunction added.
PASS: unchanged runtime block: NativeDefinition.ValidateLayout();
PASS: unchanged runtime block: private static void ValidateCommittedPatch
PASS: unchanged runtime block: X64SmartCPUContext* context =

Both original and integrated production source pass 648 cases with installed RedBird; the integrated source additionally passes switch/reactivation checks. Changes are namespace/host setting integration, atomic activation and reduced diagnostics.
