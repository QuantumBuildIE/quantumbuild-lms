interface WizardSectionDividerProps {
  number: string;
  label: string;
}

export function WizardSectionDivider({ number, label }: WizardSectionDividerProps) {
  return (
    <div className="flex items-center gap-2.5 -mb-3">
      <span className="shrink-0 font-mono text-xs font-semibold text-primary">{number}</span>
      <span className="shrink-0 text-xs uppercase tracking-widest text-muted-foreground">{label}</span>
      <div className="flex-1 border-t" />
    </div>
  );
}
