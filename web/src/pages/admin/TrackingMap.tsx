import { GoogleMap, Marker, Polyline, useJsApiLoader } from '@react-google-maps/api'
import { Fragment, useEffect, useMemo, useRef } from 'react'
import type { TrackingViajeDto } from '../../api/types'
import { decodePolyline, type LatLng } from '../../lib/decodePolyline'

const MAP_CONTAINER_STYLE = { width: '100%', height: '100%' } as const
const DEFAULT_CENTER = { lat: -0.1807, lng: -78.4678 }
const DEFAULT_ZOOM = 12

const SOLID_ROUTE = {
  strokeColor: '#003087',
  strokeOpacity: 0.85,
  strokeWeight: 4,
}

const DASHED_ROUTE = {
  strokeColor: '#003087',
  strokeOpacity: 0,
  strokeWeight: 4,
  icons: [
    {
      icon: {
        path: 'M 0,-1 0,1',
        strokeOpacity: 0.9,
        strokeColor: '#003087',
        scale: 3,
      },
      offset: '0',
      repeat: '16px',
    },
  ],
}

function tripRoutePath(trip: TrackingViajeDto): { path: LatLng[]; dashed: boolean } {
  if (trip.polyline) {
    const decoded = decodePolyline(trip.polyline)
    if (decoded.length >= 2) {
      return { path: decoded, dashed: false }
    }
  }

  const points = trip.participants
    .filter((p) => Number.isFinite(p.lat) && Number.isFinite(p.lng))
    .map((p) => ({ lat: p.lat, lng: p.lng }))

  if (points.length >= 2) {
    return { path: [points[0], points[1]], dashed: true }
  }

  return { path: points, dashed: true }
}

function collectBounds(trips: TrackingViajeDto[]): LatLng[] {
  const points: LatLng[] = []
  for (const trip of trips) {
    const { path } = tripRoutePath(trip)
    points.push(...path)
    for (const p of trip.participants) {
      if (Number.isFinite(p.lat) && Number.isFinite(p.lng)) {
        points.push({ lat: p.lat, lng: p.lng })
      }
    }
  }
  return points
}

function markerIcon(role: string): google.maps.Symbol | undefined {
  if (typeof google === 'undefined' || !google.maps) return undefined
  const isDriver = role === 'driver'
  return {
    path: google.maps.SymbolPath.CIRCLE,
    scale: isDriver ? 9 : 7,
    fillColor: isDriver ? '#003087' : '#2E7D32',
    fillOpacity: 1,
    strokeColor: '#ffffff',
    strokeWeight: 2,
  }
}

export type TrackingMapProps = {
  apiKey: string
  trips: TrackingViajeDto[]
  focusedTripId: string | null
}

export function TrackingMap({ apiKey, trips, focusedTripId }: TrackingMapProps) {
  const { isLoaded, loadError } = useJsApiLoader({
    id: 'kubix-admin-tracking',
    googleMapsApiKey: apiKey,
  })

  const mapRef = useRef<google.maps.Map | null>(null)

  const focusedTrip = useMemo(
    () => trips.find((t) => t.tripId === focusedTripId) ?? null,
    [trips, focusedTripId],
  )

  useEffect(() => {
    const map = mapRef.current
    if (!map || typeof google === 'undefined') return

    const target = focusedTrip ? [focusedTrip] : trips
    const points = collectBounds(target)
    if (points.length === 0) return

    if (points.length === 1) {
      map.panTo(points[0])
      map.setZoom(14)
      return
    }

    const bounds = new google.maps.LatLngBounds()
    for (const p of points) bounds.extend(p)
    map.fitBounds(bounds, 48)
  }, [focusedTrip, trips])

  if (loadError) {
    return (
      <div className="flex h-full items-center justify-center rounded-xl border bg-white p-6 text-sm text-red-700">
        No se pudo cargar Google Maps. Verifica la clave y las restricciones HTTP-referrer.
      </div>
    )
  }

  if (!isLoaded) {
    return (
      <div className="flex h-full items-center justify-center rounded-xl border bg-slate-50 text-sm text-slate-600">
        Cargando mapa…
      </div>
    )
  }

  return (
    <div className="h-full min-h-[420px] overflow-hidden rounded-xl border bg-white shadow-sm">
      <GoogleMap
        mapContainerStyle={MAP_CONTAINER_STYLE}
        center={DEFAULT_CENTER}
        zoom={DEFAULT_ZOOM}
        options={{
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: true,
        }}
        onLoad={(map) => {
          mapRef.current = map
        }}
        onUnmount={() => {
          mapRef.current = null
        }}
      >
        {trips.map((trip) => {
          const { path, dashed } = tripRoutePath(trip)
          const isFocused = focusedTripId === trip.tripId
          return (
            <Fragment key={trip.tripId}>
              {path.length >= 2 && (
                <Polyline
                  path={path}
                  options={{
                    ...(dashed ? DASHED_ROUTE : SOLID_ROUTE),
                    strokeWeight: isFocused ? 5 : 4,
                    strokeOpacity: dashed ? 0 : isFocused ? 1 : 0.75,
                  }}
                />
              )}
              {trip.participants.map((participant) => (
                <Marker
                  key={`${trip.tripId}-${participant.userId}`}
                  position={{ lat: participant.lat, lng: participant.lng }}
                  title={
                    participant.name
                      ? `${participant.name} (${participant.role === 'driver' ? 'conductor' : 'pasajero'})`
                      : participant.role === 'driver'
                        ? 'Conductor'
                        : 'Pasajero'
                  }
                  icon={markerIcon(participant.role)}
                  zIndex={participant.role === 'driver' ? 2 : 1}
                />
              ))}
            </Fragment>
          )
        })}
      </GoogleMap>
    </div>
  )
}
