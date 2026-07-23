import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { adminApi } from '../../api/admin'
import type {
  AdminReport,
  ReportExportFormat,
  ReportKpis,
  ReportPeriod,
  ReportTrip,
  WeeklyChartPoint,
} from '../../api/types'
import {
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  SecondaryButton,
  apiErrorMessage,
  inputClassName,
} from '../super/ui'

const PERIODS: { value: ReportPeriod; label: string }[] = [
  { value: 'diario', label: 'Diario' },
  { value: 'semanal', label: 'Semanal' },
  { value: 'mensual', label: 'Mensual' },
  { value: 'trimestral', label: 'Trimestral' },
  { value: 'anual', label: 'Anual' },
]

const KPI_CARDS: {
  key: keyof ReportKpis
  label: string
  format: (v: number) => string
}[] = [
  { key: 'trips', label: 'Viajes', format: (v) => String(v) },
  {
    key: 'km',
    label: 'Kilómetros',
    format: (v) => `${formatNumber(v)} km`,
  },
  {
    key: 'co2Saved',
    label: 'CO₂ ahorrado',
    format: (v) => `${formatNumber(v)} kg`,
  },
  {
    key: 'blockedUsers',
    label: 'Usuarios bloqueados',
    format: (v) => String(v),
  },
  {
    key: 'adoptionRate',
    label: 'Tasa de adopción',
    format: (v) => `${formatNumber(v)}%`,
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

function formatDateOnly(value: string) {
  const d = new Date(value)
  const y = d.getUTCFullYear()
  const m = String(d.getUTCMonth() + 1).padStart(2, '0')
  const day = String(d.getUTCDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function tripStatusLabel(status: string) {
  switch (status.toLowerCase()) {
    case 'completed':
      return 'Completado'
    case 'cancelled':
      return 'Cancelado'
    case 'in_progress':
      return 'En curso'
    case 'published':
    case 'scheduled':
      return 'Programado'
    case 'full':
      return 'Lleno'
    default:
      return status
  }
}

function tripStatusTone(status: string) {
  switch (status.toLowerCase()) {
    case 'completed':
      return 'bg-emerald-50 text-emerald-700 ring-emerald-600/20'
    case 'cancelled':
      return 'bg-red-50 text-red-700 ring-red-600/20'
    case 'in_progress':
      return 'bg-blue-50 text-blue-700 ring-blue-600/20'
    default:
      return 'bg-slate-100 text-slate-700 ring-slate-500/20'
  }
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}

function fallbackExportFilename(
  period: ReportPeriod,
  from: string,
  to: string,
  format: ReportExportFormat,
) {
  return `kubix_reporte_${period}_${formatDateOnly(from)}_${formatDateOnly(to)}.${format}`
}

export function ReportesPage() {
  const [period, setPeriod] = useState<ReportPeriod>('semanal')
  const [exportSuccess, setExportSuccess] = useState<string | null>(null)

  const report = useQuery({
    queryKey: ['admin', 'reports', period],
    queryFn: () => adminApi.getReports(period),
  })

  const exportMutation = useMutation({
    mutationFn: (format: ReportExportFormat) => adminApi.exportReports(period, format),
    onSuccess: (result, format) => {
      const filename =
        result.filename ??
        fallbackExportFilename(
          period,
          report.data?.from ?? new Date().toISOString(),
          report.data?.to ?? new Date().toISOString(),
          format,
        )
      downloadBlob(result.blob, filename)
      setExportSuccess(`Descarga iniciada: ${filename}`)
    },
    onError: () => {
      setExportSuccess(null)
    },
  })

  return (
    <div className="space-y-8">
      <PageHeader
        title="Reportes de Viajes"
        description="Indicadores del periodo, detalle de viajes y exportación CSV / XLSX / PDF."
        actions={
          <div className="flex flex-wrap gap-2">
            {(['csv', 'xlsx', 'pdf'] as ReportExportFormat[]).map((format) => (
              <SecondaryButton
                key={format}
                disabled={exportMutation.isPending || report.isLoading}
                onClick={() => {
                  setExportSuccess(null)
                  exportMutation.mutate(format)
                }}
              >
                {exportMutation.isPending && exportMutation.variables === format
                  ? 'Exportando…'
                  : `Exportar ${format.toUpperCase()}`}
              </SecondaryButton>
            ))}
          </div>
        }
      />

      <label className="block max-w-xs text-sm">
        <span className="font-medium text-slate-700">Periodo</span>
        <select
          className={`${inputClassName} mt-1`}
          value={period}
          onChange={(e) => {
            setPeriod(e.target.value as ReportPeriod)
            setExportSuccess(null)
            exportMutation.reset()
          }}
          aria-label="Seleccionar periodo"
        >
          {PERIODS.map((p) => (
            <option key={p.value} value={p.value}>
              {p.label}
            </option>
          ))}
        </select>
      </label>

      {exportMutation.isError && (
        <ErrorBanner
          message={apiErrorMessage(exportMutation.error, 'No se pudo exportar el reporte.')}
        />
      )}
      {exportSuccess && (
        <p
          className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800"
          role="status"
        >
          {exportSuccess}
        </p>
      )}

      {report.isLoading && <LoadingState />}
      {report.isError && (
        <ErrorBanner
          message={apiErrorMessage(report.error, 'No se pudo cargar el reporte.')}
        />
      )}

      {report.data && (
        <>
          <RangeLabel report={report.data} />
          <KpiGrid kpis={report.data.kpis} />
          <WeeklyChartSection points={report.data.weeklyChart} />
          <TripsTable trips={report.data.trips} />
        </>
      )}
    </div>
  )
}

function RangeLabel({ report }: { report: AdminReport }) {
  return (
    <p className="text-sm text-slate-600">
      Rango:{' '}
      <span className="font-medium text-slate-800">
        {formatDateTime(report.from)} — {formatDateTime(report.to)}
      </span>
    </p>
  )
}

function KpiGrid({ kpis }: { kpis: ReportKpis }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
      {KPI_CARDS.map((card) => (
        <div key={card.key} className="rounded-xl border bg-white p-5 shadow-sm">
          <div className="text-sm font-medium text-slate-500">{card.label}</div>
          <div className="mt-2 text-3xl font-semibold tracking-tight text-[var(--kubix-navy)]">
            {card.format(kpis[card.key])}
          </div>
        </div>
      ))}
    </div>
  )
}

function WeeklyChartSection({ points }: { points: WeeklyChartPoint[] }) {
  const maxTrips = Math.max(0, ...points.map((p) => p.trips))
  const chartHeight = 160

  return (
    <section>
      <div className="mb-3">
        <h2 className="text-lg font-semibold text-slate-900">Actividad semanal</h2>
        <p className="mt-1 text-sm text-slate-600">
          Viajes por intervalo dentro del periodo seleccionado.
        </p>
      </div>

      <div className="rounded-xl border bg-white p-5 shadow-sm">
        {points.length === 0 ? (
          <EmptyState label="No hay datos de gráfica para este periodo." />
        ) : (
          <div className="overflow-x-auto">
            <svg
              role="img"
              aria-label="Gráfica de viajes por periodo"
              viewBox={`0 0 ${Math.max(points.length * 64, 320)} ${chartHeight + 48}`}
              className="h-56 w-full min-w-[20rem]"
            >
              {points.map((point, index) => {
                const barMax = chartHeight - 8
                const barHeight =
                  maxTrips > 0 ? Math.max(4, (point.trips / maxTrips) * barMax) : 0
                const x = index * 64 + 16
                const y = chartHeight - barHeight
                return (
                  <g key={`${point.label}-${index}`}>
                    <title>
                      {point.label}: {point.trips} viajes, {formatNumber(point.km)} km
                    </title>
                    <rect
                      x={x}
                      y={y}
                      width={32}
                      height={barHeight}
                      rx={4}
                      className="fill-[var(--kubix-blue)]"
                    />
                    <text
                      x={x + 16}
                      y={chartHeight + 16}
                      textAnchor="middle"
                      className="fill-slate-600 text-[10px]"
                    >
                      {point.label}
                    </text>
                    <text
                      x={x + 16}
                      y={y - 6}
                      textAnchor="middle"
                      className="fill-slate-800 text-[11px] font-medium"
                    >
                      {point.trips}
                    </text>
                  </g>
                )
              })}
            </svg>
            <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
              {points.map((point, index) => (
                <li key={`km-${point.label}-${index}`}>
                  {point.label}: {formatNumber(point.km)} km
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </section>
  )
}

function TripsTable({ trips }: { trips: ReportTrip[] }) {
  return (
    <section>
      <div className="mb-3">
        <h2 className="text-lg font-semibold text-slate-900">Viajes del periodo</h2>
        <p className="mt-1 text-sm text-slate-600">
          Detalle de origen, conductor, distancia y CO₂ ahorrado.
        </p>
      </div>

      {trips.length === 0 ? (
        <EmptyState label="No hay viajes en este periodo." />
      ) : (
        <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b bg-slate-50 text-slate-600">
              <tr>
                <th className="px-4 py-3 font-medium">Origen</th>
                <th className="px-4 py-3 font-medium">Conductor</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Salida</th>
                <th className="px-4 py-3 font-medium">Completado</th>
                <th className="px-4 py-3 font-medium">Km</th>
                <th className="px-4 py-3 font-medium">CO₂</th>
              </tr>
            </thead>
            <tbody>
              {trips.map((trip) => (
                <tr key={trip.id} className="border-b last:border-0">
                  <td className="px-4 py-3 font-medium text-slate-900">{trip.originText}</td>
                  <td className="px-4 py-3 text-slate-700">{trip.driverName || '—'}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${tripStatusTone(trip.status)}`}
                    >
                      {tripStatusLabel(trip.status)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-slate-700">
                    {formatDateTime(trip.departureAt)}
                  </td>
                  <td className="px-4 py-3 text-slate-700">
                    {trip.completedAt ? formatDateTime(trip.completedAt) : '—'}
                  </td>
                  <td className="px-4 py-3 tabular-nums text-slate-700">
                    {formatNumber(trip.distanceKm)}
                  </td>
                  <td className="px-4 py-3 tabular-nums text-slate-700">
                    {formatNumber(trip.co2SavedKg)} kg
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
