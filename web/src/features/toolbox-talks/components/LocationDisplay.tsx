import { MapPin } from 'lucide-react';
import { format } from 'date-fns';

interface LocationDisplayProps {
  latitude?: number | null;
  longitude?: number | null;
  accuracyMeters?: number | null;
  timestamp?: string | null;
  label: string;
}

export function LocationDisplay({
  latitude,
  longitude,
  accuracyMeters,
  timestamp,
  label,
}: LocationDisplayProps) {
  if (!latitude || !longitude) {
    return (
      <div className="text-sm text-muted-foreground">
        <span className="font-medium">{label}:</span> Not provided
      </div>
    );
  }

  const googleMapsUrl = `https://www.google.com/maps?q=${latitude},${longitude}`;

  return (
    <div className="text-sm">
      <span className="font-medium">{label}:</span>{' '}
      <a
        href={googleMapsUrl}
        target="_blank"
        rel="noopener noreferrer"
        className="text-blue-600 hover:underline inline-flex items-center gap-1"
      >
        <MapPin className="h-3 w-3" />
        {latitude.toFixed(6)}, {longitude.toFixed(6)}
      </a>
      {accuracyMeters != null && (
        <span className="text-muted-foreground"> ({Math.round(accuracyMeters)}m)</span>
      )}
      {timestamp && (
        <span className="text-muted-foreground">
          {' '}at {format(new Date(timestamp), 'HH:mm')}
        </span>
      )}
    </div>
  );
}
