import { useState, useCallback } from 'react';

export interface GeolocationData {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  timestamp: Date;
}

export interface UseGeolocationResult {
  getLocation: () => Promise<GeolocationData | null>;
  isLoading: boolean;
  error: string | null;
}

export function useGeolocation(): UseGeolocationResult {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getLocation = useCallback(async (): Promise<GeolocationData | null> => {
    if (!navigator.geolocation) {
      setError('Geolocation not supported');
      return null;
    }

    setIsLoading(true);
    setError(null);

    return new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) => {
          setIsLoading(false);
          resolve({
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracyMeters: position.coords.accuracy,
            timestamp: new Date(position.timestamp),
          });
        },
        (err) => {
          setIsLoading(false);
          setError(err.message);
          resolve(null); // Don't reject - allow completion without location
        },
        {
          enableHighAccuracy: true,
          timeout: 10000,
          maximumAge: 60000,
        }
      );
    });
  }, []);

  return { getLocation, isLoading, error };
}
