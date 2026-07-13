import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '../../api/admin'
import type { AdminDashboard, AlertaSosAdmin, XpPorCarrera } from '../../api/types'
import {
  DangerButton,
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  apiErrorMessage,
} from '../super/ui'

const POLL_MS = 10_000

const kpiCards = [
  { key: 'tripsToday' as const, label: 'Viajes hoy', format: (v: number) => String(v) },
  { key: 'blockedUsers' as const, label: 'Usuarios bloqueados', format: (v: number) => String(v) },
  {
    key: 'co2Saved' as const,
    label: 'CO₂ ahorrado',
    format: (v: number) => `${formatNumber(v)} kg`,
  },
  {
    key: 'adoptionRate' as const,
    label: 'Tasa de adopción',
    format: (v: number) => `${formatNumber(v)}%`,
  },
]

function formatNumber(value: number) {
  return new Intl.NumberFormat('es-EC', { maximumFractionDigits: 2 }).format(value)
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-EC', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function DashboardPage() {
  const queryClient = useQueryClient()

  const dashboard = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: adminApi.getDashboard,
    refetchInterval: POLL_MS,
  })

  const resolveMutation = useMutation({
    mutationFn: (id: string) => adminApi.resolveSos(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] })
    },
  })

  return (
    <div className="space-y-8">
      <PageHeader
        title="Panel de Control"
        description="KPIs del día, monitoreo SOS y EcoTokens por carrera."
      />

      {dashboard.isLoading && <LoadingState />}
      {dashboard.isError && (
        <ErrorBanner
          message={apiErrorMessage(dashboard.error, 'No se pudo cargar el panel de control.')}
        />
      )}

      {dashboard.data && (
        <>
          <KpiGrid data={dashboard.data} />
          <SosSection
            alerts={dashboard.data.activeSos}
            resolvingId={resolveMutation.isPending ? resolveMutation.variables : null}
            resolveError={
              resolveMutation.isError
                ? apiErrorMessage(resolveMutation.error, 'No se pudo resolver la alerta SOS.')
                : null
            }
            onResolve={(id) => resolveMutation.mutate(id)}
          />
          <XpByCareerSection
            enabled={dashboard.data.gamificationEnabled}
            note={dashboard.data.xpByCareerNote}
            items={dashboard.data.xpByCareer}
          />
        </>
      )}
    </div>
  )
}

function KpiGrid({ data }: { data: AdminDashboard }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {kpiCards.map((card) => (
        <div key={card.key} className="rounded-xl border bg-white p-5 shadow-sm">
          <div className="text-sm font-medium text-slate-500">{card.label}</div>
          <div className="mt-2 text-3xl font-semibold tracking-tight text-[var(--kubix-navy)]">
            {card.format(data[card.key])}
          </div>
        </div>
      ))}
    </div>
  )
}

function SosSection({
  alerts,
  resolvingId,
  resolveError,
  onResolve,
}: {
  alerts: AlertaSosAdmin[]
  resolvingId: string | null | undefined
  resolveError: string | null
  onResolve: (id: string) => void
}) {
  return (
    <section>
      <div className="mb-3">
        <h2 className="text-lg font-semibold text-slate-900">Monitoreo SOS</h2>
        <p className="mt-1 text-sm text-slate-600">
          Alertas activas. Se actualiza automáticamente cada 10 segundos.
        </p>
      </div>

      {resolveError && (
        <div className="mb-3">
          <ErrorBanner message={resolveError} />
        </div>
      )}

      {alerts.length === 0 ? (
        <EmptyState label="No hay alertas SOS activas." />
      ) : (
        <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b bg-slate-50 text-slate-600">
              <tr>
                <th className="px-4 py-3 font-medium">Estudiante</th>
                <th className="px-4 py-3 font-medium">Viaje</th>
                <th className="px-4 py-3 font-medium">Conductor</th>
                <th className="px-4 py-3 font-medium">Ubicación</th>
                <th className="px-4 py-3 font-medium">Disparada</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {alerts.map((alert) => (
                <tr key={alert.id} className="border-b last:border-0">
                  <td className="px-4 py-3">
                    <div className="font-medium text-slate-900">
                      {alert.student?.name ?? '—'}
                    </div>
                    <div className="text-xs text-slate-500">{alert.student?.email}</div>
                  </td>
                  <td className="px-4 py-3 text-slate-700">
                    {alert.trip ? alert.trip.originText : 'Sin viaje'}
                  </td>
                  <td className="px-4 py-3 text-slate-700">{alert.driver?.name ?? '—'}</td>
                  <td className="px-4 py-3 text-slate-700">
                    {alert.lat.toFixed(4)}, {alert.lng.toFixed(4)}
                  </td>
                  <td className="px-4 py-3 text-slate-700">{formatDateTime(alert.firedAt)}</td>
                  <td className="px-4 py-3">
                    <span className="inline-flex rounded-md bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700 ring-1 ring-inset ring-red-600/20">
                      {alert.status === 'active' ? 'Activa' : alert.status}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <DangerButton
                      disabled={resolvingId === alert.id}
                      onClick={() => onResolve(alert.id)}
                    >
                      {resolvingId === alert.id ? 'Resolviendo…' : 'Resolver'}
                    </DangerButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function XpByCareerSection({
  enabled,
  note,
  items,
}: {
  enabled: boolean
  note?: string | null
  items: XpPorCarrera[]
}) {
  const maxXp = Math.max(0, ...items.map((item) => item.xp))

  return (
    <section>
      <div className="mb-3">
        <h2 className="text-lg font-semibold text-slate-900">
          Puntos por carrera · semana actual
        </h2>
        <p className="mt-1 text-sm text-slate-600">
          EcoTokens positivos acumulados en la semana (timezone de la universidad).
        </p>
      </div>

      <div className="rounded-xl border bg-white p-5 shadow-sm">
        {!enabled ? (
          <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
            <p className="font-medium">Gamificación deshabilitada</p>
            <p className="mt-1 text-amber-800">
              {note ??
                'La gamificación está desactivada para esta universidad; no hay datos de XP por carrera.'}
            </p>
          </div>
        ) : items.length === 0 ? (
          <EmptyState label="Aún no hay puntos registrados esta semana." />
        ) : (
          <ul className="space-y-4">
            {items.map((item) => {
              const width = maxXp > 0 ? Math.max(4, (item.xp / maxXp) * 100) : 0
              return (
                <li key={item.career}>
                  <div className="mb-1 flex items-baseline justify-between gap-3 text-sm">
                    <span className="font-medium text-slate-800">{item.career}</span>
                    <span className="tabular-nums text-slate-600">{item.xp} XP</span>
                  </div>
                  <div className="h-2.5 overflow-hidden rounded-full bg-slate-100">
                    <div
                      className="h-full rounded-full bg-[var(--kubix-blue)]"
                      style={{ width: `${width}%` }}
                      role="progressbar"
                      aria-valuenow={item.xp}
                      aria-valuemin={0}
                      aria-valuemax={maxXp}
                      aria-label={`${item.career}: ${item.xp} XP`}
                    />
                  </div>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </section>
  )
}
