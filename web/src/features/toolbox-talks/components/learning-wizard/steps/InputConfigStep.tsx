'use client';

// TODO (Phase 5.3): Implement Step 1 — Input & Config.
// BLOCKER: CreateToolboxTalkCommandValidator.RuleFor(x => x.Sections).NotEmpty()
//   fires when VideoUrl is null/empty, which blocks creating a talk from this wizard
//   (sections aren't available until Step 2 / AI parse). The validator must be loosened
//   or a dedicated wizard-create command added before this step can save.

export function InputConfigStep() {
  return (
    <div className="rounded-lg border border-dashed p-10 text-center text-muted-foreground">
      <p className="text-sm font-medium">Step 1 — Input &amp; Config</p>
      <p className="text-xs mt-1">Placeholder — Phase 5.3</p>
    </div>
  );
}
