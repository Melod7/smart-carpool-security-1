import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { adminApi } from '../../api/admin'
import type { TrackingViajeDto } from '../../api/types'
import {
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  apiErrorMessage,
} from '../super/ui'
import { TrackingMap } from './TrackingMap'

const POLL_MS = 10_000

function mapsApiKey(): string {
  return (import.meta.env.VITE_GOOGLE_MAPS_API_KEY ?? '').trim()
}

function statusLabel(status: string) {
  switch (status) {
    case 'in_progress':
      return 'En curso'
    case 'scheduled':
      return 'Programado'
    case 'completed':
      return 'Completado'
    case 'cancelled':
      return 'Cancelado'
    default:
      return status
  }
}

function tripSummary(trip: TrackingViajeDto) {
  const driver = trip.participants.find((p) => p.role === 'driver')
  const passengers = trip.participants.filter((p) => p.role === 'passenger')
  return {
    driverName: driver?.name?.trim() || 'Conductor sin nombre',
    passengerCount: passengers.length,
  }
}

export function TrackingPage() {
  const [searchParams] = useSearchParams()
  const tripIdFromUrl = searchParams.get('tripId')
  const [focusedTripId, setFocusedTripId] = useState<string | null>(tripIdFromUrl)
  const apiKey = mapsApiKey()

  const tracking = useQuery({
    queryKey: ['admin', 'tracking', 'active'],
    queryFn: adminApi.getActiveTracking,
    refetchInterval: POLL_MS,
  })

  const trips = tracking.data?.trips ?? []

  useEffect(() => {
    if (tripIdFromUrl) {
      setFocusedTripId(tripIdFromUrl)
    }
  }, [tripIdFromUrl])

  useEffect(() => {
    if (!focusedTripId || trips.length === 0) return
    if (!trips.some((t) => t.tripId === focusedTripId)) {
      setFocusedTripId(null)
    }
  }, [trips, focusedTripId])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Tracking en vivo"
        description="Viajes activos con ruta y ubicación de participantes. Se actualiza cada 10 segundos."
      />

      {tracking.isLoading && <LoadingState />}
      {tracking.isError && (
        <ErrorBanner
          message={apiErrorMessage(tracking.error, 'No se pudo cargar el tracking activo.')}
        />
      )}

      {tracking.data && (
        <div className="grid gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
          <aside className="rounded-xl border bg-white shadow-sm">
            <div className="border-b px-4 py-3">
              <h2 className="text-sm font-semibold text-slate-900">Viajes activos</h2>
              <p className="mt-0.5 text-xs text-slate-500">
                {trips.length === 0
                  ? 'Ninguno en este momento'
                  : `${trips.length} en curso`}
              </p>
            </div>

            {trips.length === 0 ? (
              <div className="px-4 py-6">
                <EmptyState label="No hay viajes activos para rastrear." />
              </div>
            ) : (
              <ul className="max-h-[560px] divide-y overflow-y-auto">
                {trips.map((trip) => {
                  const { driverName, passengerCount } = tripSummary(trip)
                  const selected = focusedTripId === trip.tripId
                  return (
                    <li key={trip.tripId}>
                      <button
                        type="button"
                        onClick={() => setFocusedTripId(trip.tripId)}
                        className={[
                          'w-full px-4 py-3 text-left transition-colors',
                          selected
                            ? 'bg-blue-50'
                            : 'hover:bg-slate-50',
                        ].join(' ')}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="min-w-0">
                            <div className="truncate text-sm font-medium text-slate-900">
                              {driverName}
                            </div>
                            <div className="mt-0.5 text-xs text-slate-500">
                              {passengerCount === 1
                                ? '1 pasajero'
                                : `${passengerCount} pasajeros`}
                              {' · '}
                              <span className="font-mono text-[11px]">
                                {trip.tripId.slice(0, 8)}…
                              </span>
                            </div>
                          </div>
                          <span className="inline-flex shrink-0 rounded-md bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
                            {statusLabel(trip.status)}
                          </span>
                        </div>
                        {!trip.polyline && (
                          <p className="mt-1 text-[11px] text-amber-700">
                            Sin polyline — línea recta aproximada
                          </p>
                        )}
                      </button>
                    </li>
                  )
                })}
              </ul>
            )}

            <div className="border-t px-4 py-3 text-xs text-slate-500">
              <p>
                <span
                  className="mr-1 inline-block h-2.5 w-2.5 rounded-full bg-[var(--kubix-navy)]"
                  aria-hidden
                />
                Conductor
                <span
                  className="ml-3 mr-1 inline-block h-2.5 w-2.5 rounded-full bg-[#2E7D32]"
                  aria-hidden
                />
                Pasajero
              </p>
              <p className="mt-2">
                <Link to="/admin" className="text-[var(--kubix-blue)] hover:underline">
                  Volver al panel
                </Link>
              </p>
            </div>
          </aside>

          <section className="min-h-[420px]">
            {!apiKey ? (
              <div className="flex h-full min-h-[420px] flex-col justify-center rounded-xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-950">
                <p className="font-semibold">Falta la clave de Google Maps</p>
                <p className="mt-2 text-amber-900">
                  Configura <code className="rounded bg-amber-100 px-1 py-0.5 text-xs">VITE_GOOGLE_MAPS_API_KEY</code>{' '}
                  en <code className="rounded bg-amber-100 px-1 py-0.5 text-xs">web/.env</code> y reinicia el
                  servidor de desarrollo para ver el mapa. La lista de viajes sigue disponible a la
                  izquierda.
                </p>
              </div>
            ) : (
              <TrackingMap
                apiKey={apiKey}
                trips={trips}
                focusedTripId={focusedTripId}
              />
            )}
          </section>
        </div>
      )}
    </div>
  )
}
