import { GoogleMap, Marker, useJsApiLoader } from '@react-google-maps/api'
import { useCallback, useMemo } from 'react'

const MAP_STYLE = { width: '100%', height: '100%' } as const
const DEFAULT_CENTER = { lat: -0.1807, lng: -78.4678 }
const DEFAULT_ZOOM = 13

export type MapLatLng = { lat: number; lng: number }

type LocationMapPickerProps = {
  apiKey: string
  value: MapLatLng | null
  onChange: (point: MapLatLng) => void
  /** Centro inicial si aún no hay valor (p. ej. al crear). */
  defaultCenter?: MapLatLng
  className?: string
}

/**
 * Mapa clicable para elegir lat/lng (campus, etc.).
 * Requiere `VITE_GOOGLE_MAPS_API_KEY`.
 */
export function LocationMapPicker({
  apiKey,
  value,
  onChange,
  defaultCenter = DEFAULT_CENTER,
  className,
}: LocationMapPickerProps) {
  const { isLoaded, loadError } = useJsApiLoader({
    id: 'kubix-google-maps',
    googleMapsApiKey: apiKey,
  })

  const center = value ?? defaultCenter

  const onMapClick = useCallback(
    (e: google.maps.MapMouseEvent) => {
      const lat = e.latLng?.lat()
      const lng = e.latLng?.lng()
      if (lat == null || lng == null) return
      onChange({ lat, lng })
    },
    [onChange],
  )

  const onMarkerDragEnd = useCallback(
    (e: google.maps.MapMouseEvent) => {
      const lat = e.latLng?.lat()
      const lng = e.latLng?.lng()
      if (lat == null || lng == null) return
      onChange({ lat, lng })
    },
    [onChange],
  )

  const markerIcon = useMemo(() => {
    if (!isLoaded || typeof google === 'undefined' || !google.maps) return undefined
    return {
      path: google.maps.SymbolPath.CIRCLE,
      scale: 10,
      fillColor: '#003087',
      fillOpacity: 1,
      strokeColor: '#ffffff',
      strokeWeight: 2,
    } satisfies google.maps.Symbol
  }, [isLoaded])

  if (!apiKey) {
    return (
      <div
        className={[
          'flex h-56 items-center justify-center rounded-lg border border-amber-200 bg-amber-50 px-4 text-center text-sm text-amber-900',
          className ?? '',
        ].join(' ')}
      >
        Configura <code className="mx-1 rounded bg-amber-100 px-1">VITE_GOOGLE_MAPS_API_KEY</code> en{' '}
        <code className="mx-1 rounded bg-amber-100 px-1">web/.env</code> para elegir el punto en el mapa.
      </div>
    )
  }

  if (loadError) {
    return (
      <div
        className={[
          'flex h-56 items-center justify-center rounded-lg border border-red-200 bg-red-50 px-4 text-sm text-red-700',
          className ?? '',
        ].join(' ')}
      >
        No se pudo cargar Google Maps. Revisa la API key (Maps JavaScript API).
      </div>
    )
  }

  if (!isLoaded) {
    return (
      <div
        className={[
          'flex h-56 items-center justify-center rounded-lg border bg-slate-50 text-sm text-slate-500',
          className ?? '',
        ].join(' ')}
      >
        Cargando mapa…
      </div>
    )
  }

  return (
    <div className={['overflow-hidden rounded-lg border border-slate-200', className ?? ''].join(' ')}>
      <div className="h-64 w-full sm:h-72">
        <GoogleMap
          mapContainerStyle={MAP_STYLE}
          center={center}
          zoom={value ? 15 : DEFAULT_ZOOM}
          onClick={onMapClick}
          options={{
            streetViewControl: false,
            mapTypeControl: false,
            fullscreenControl: false,
            clickableIcons: false,
          }}
        >
          {value && (
            <Marker position={value} draggable onDragEnd={onMarkerDragEnd} icon={markerIcon} />
          )}
        </GoogleMap>
      </div>
      <p className="border-t bg-slate-50 px-3 py-2 text-xs text-slate-600">
        {value
          ? `Punto: ${value.lat.toFixed(6)}, ${value.lng.toFixed(6)} · Arrastra el marcador o toca otro lugar.`
          : 'Toca el mapa para marcar la ubicación del campus.'}
      </p>
    </div>
  )
}

export function mapsApiKeyFromEnv(): string {
  return (import.meta.env.VITE_GOOGLE_MAPS_API_KEY ?? '').trim()
}
