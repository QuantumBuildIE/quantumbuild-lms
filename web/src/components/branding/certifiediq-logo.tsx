import { cn } from "@/lib/utils";

interface CertifiedIqLogoProps {
  /** md = header size (default); sm = compact, for the 'Powered by' badge. */
  size?: "md" | "sm";
  className?: string;
}

export function CertifiedIqLogo({ size = "md", className }: CertifiedIqLogoProps) {
  const isSmall = size === "sm";
  return (
    <div className={cn("flex items-center gap-2", className)}>
      <svg viewBox="0 0 46 46" fill="none" className={isSmall ? "w-5 h-5" : "w-8 h-8"}>
        <circle cx="23" cy="23" r="21" fill="#4d8eff" fillOpacity="0.1" stroke="#4d8eff" strokeWidth="1.5" strokeOpacity="0.3"/>
        <path d="M23 10V23L30 30" stroke="#4d8eff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/>
        <circle cx="23" cy="23" r="3" fill="#4d8eff"/>
        <circle cx="23" cy="7" r="2" fill="#4d8eff" opacity="0.6"/>
        <circle cx="39" cy="23" r="2" fill="#4d8eff" opacity="0.6"/>
        <circle cx="23" cy="39" r="2" fill="#4d8eff" opacity="0.6"/>
        <circle cx="7" cy="23" r="2" fill="#4d8eff" opacity="0.6"/>
      </svg>
      <span className={cn("font-bold tracking-tight", isSmall ? "text-sm" : "text-lg")}>
        Certified<span className="text-primary font-extrabold">IQ</span>
      </span>
    </div>
  );
}
