import { GoogleMap, Marker, useJsApiLoader } from '@react-google-maps/api'
import { useCallback, useMemo, useState } from 'react'

const MAP_STYLE = { width: '100%', height: '100%' } as const
const DEFAULT_CENTER = { lat: -0.1807, lng: -78.4678 }
const DEFAULT_ZOOM = 13

export type MapLatLng = { lat: number; lng: number }

type LocationMapPickerProps = {
  apiKey: string
  value: MapLatLng | null
  onChange: (point: MapLatLng) => void
  address?: string
  onAddressChange?: (address: string) => void
  /** Centro inicial si aún no hay valor (p. ej. al crear). */
  defaultCenter?: MapLatLng
  className?: string
}

/**
 * Mapa clicable para elegir lat/lng (campus, etc.).
 * Requiere `GOOGLE_MAPS_API_KEY` en el `.env` raíz (`make sync-env`).
 */
export function LocationMapPicker({
  apiKey,
  value,
  onChange,
  address = '',
  onAddressChange,
  defaultCenter = DEFAULT_CENTER,
  className,
}: LocationMapPickerProps) {
  const [searching, setSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)
  const { isLoaded, loadError } = useJsApiLoader({
    id: 'kubix-google-maps',
    googleMapsApiKey: apiKey,
  })

  const center = value ?? defaultCenter

  const selectPoint = useCallback(
    async (point: MapLatLng, resolveAddress: boolean) => {
      onChange(point)
      setSearchError(null)
      if (!resolveAddress || !onAddressChange) return
      try {
        const results = await new google.maps.Geocoder().geocode({ location: point })
        const formatted = results.results[0]?.formatted_address
        if (formatted) onAddressChange(formatted)
      } catch {
        setSearchError('Se marcó el punto, pero no se pudo obtener la dirección.')
      }
    },
    [onAddressChange, onChange],
  )

  const onMapClick = useCallback(
    (e: google.maps.MapMouseEvent) => {
      const lat = e.latLng?.lat()
      const lng = e.latLng?.lng()
      if (lat == null || lng == null) return
      void selectPoint({ lat, lng }, true)
    },
    [selectPoint],
  )

  const onMarkerDragEnd = useCallback(
    (e: google.maps.MapMouseEvent) => {
      const lat = e.latLng?.lat()
      const lng = e.latLng?.lng()
      if (lat == null || lng == null) return
      void selectPoint({ lat, lng }, true)
    },
    [selectPoint],
  )

  async function searchAddress() {
    const query = address.trim()
    if (!query) {
      setSearchError('Escribe una dirección para buscar.')
      return
    }

    setSearching(true)
    setSearchError(null)
    try {
      const results = await new google.maps.Geocoder().geocode({
        address: query,
        region: 'EC',
      })
      const result = results.results[0]
      if (!result) {
        setSearchError('No encontramos esa dirección.')
        return
      }
      const point = {
        lat: result.geometry.location.lat(),
        lng: result.geometry.location.lng(),
      }
      onChange(point)
      onAddressChange?.(result.formatted_address)
    } catch {
      setSearchError('No se pudo buscar la dirección. Revisa Geocoding API.')
    } finally {
      setSearching(false)
    }
  }

  const markerIcon = useMemo(() => {
    if (!isLoaded || typeof google === 'undefined' || !google.maps) return undefined
    return {
      path: google.maps.SymbolPath.CIRCLE,
      scale: 10,
      fillColor: '#ce2727',
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
        Configura <code className="mx-1 rounded bg-amber-100 px-1">GOOGLE_MAPS_API_KEY</code> en{' '}
        <code className="mx-1 rounded bg-amber-100 px-1">.env</code> (raíz) y corre{' '}
        <code className="mx-1 rounded bg-amber-100 px-1">make sync-env</code>.
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
      {onAddressChange && (
        <div className="flex gap-2 border-b bg-white p-3">
          <input
            value={address}
            onChange={(event) => onAddressChange(event.target.value)}
            onKeyDown={(event) => {
              if (event.key !== 'Enter') return
              event.preventDefault()
              void searchAddress()
            }}
            placeholder="Busca una dirección en Ecuador"
            aria-label="Dirección del campus"
            className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-600 focus:ring-2 focus:ring-blue-100"
          />
          <button
            type="button"
            disabled={searching}
            onClick={() => void searchAddress()}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {searching ? 'Buscando…' : 'Buscar'}
          </button>
        </div>
      )}
      {searchError && (
        <p className="border-b bg-amber-50 px-3 py-2 text-xs text-amber-900" role="alert">
          {searchError}
        </p>
      )}
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
