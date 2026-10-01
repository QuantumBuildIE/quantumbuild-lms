export const MAX_LOGO_BYTES = 1024 * 1024; // 1MB, mirrors the server limit (server stays authoritative)
export const ALLOWED_LOGO_TYPES = ["image/png", "image/jpeg"];
export const MIN_LOGO_HEIGHT = 80;
export const MIN_ASPECT_RATIO = 1; // width:height
export const MAX_ASPECT_RATIO = 5;

/** Hard client-side checks. Returns an error message, or null when the file may be uploaded. */
export function validateLogoFile(file: { type: string; size: number }): string | null {
  if (!ALLOWED_LOGO_TYPES.includes(file.type)) {
    return "Logo must be a PNG or JPEG image.";
  }
  if (file.size > MAX_LOGO_BYTES) {
    return "Logo must be 1 MB or smaller.";
  }
  return null;
}

/** Soft warnings (never block an upload) based on the image's pixel dimensions. */
export function getLogoWarnings(width: number, height: number): string[] {
  const warnings: string[] = [];
  if (height < MIN_LOGO_HEIGHT) {
    warnings.push(
      `This image is ${height}px tall. Logos under ${MIN_LOGO_HEIGHT}px tall may look blurry in the header.`
    );
  }
  const ratio = height > 0 ? width / height : 0;
  if (ratio < MIN_ASPECT_RATIO || ratio > MAX_ASPECT_RATIO) {
    warnings.push(
      "The aspect ratio is outside 1:1 to 5:1. A horizontal logo works best in the header."
    );
  }
  return warnings;
}
