import { useMemo, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { adminApi } from '../../api/admin'
import type { AuditEvent, AuditLogFilter } from '../../api/types'
import {
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  SecondaryButton,
  apiErrorMessage,
  inputClassName,
} from '../super/ui'

const PAGE_SIZE = 20

const TYPE_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'sos', label: 'SOS' },
  { value: 'auth', label: 'Autenticación' },
  { value: 'admin', label: 'Admin' },
  { value: 'system', label: 'Sistema' },
]

const SEVERITY_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'Todas' },
  { value: 'high', label: 'Alta' },
  { value: 'medium', label: 'Media' },
  { value: 'low', label: 'Baja' },
]

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-EC', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function typeLabel(type: string) {
  switch (type.toLowerCase()) {
    case 'sos':
      return 'SOS'
    case 'auth':
      return 'Autenticación'
    case 'admin':
      return 'Admin'
    case 'system':
      return 'Sistema'
    default:
      return type
  }
}

function severityLabel(severity: string) {
  switch (severity.toLowerCase()) {
    case 'high':
      return 'Alta'
    case 'medium':
      return 'Media'
    case 'low':
      return 'Baja'
    default:
      return severity
  }
}

function severityTone(severity: string) {
  switch (severity.toLowerCase()) {
    case 'high':
      return 'bg-red-50 text-red-700 ring-red-600/20'
    case 'medium':
      return 'bg-amber-50 text-amber-800 ring-amber-600/20'
    case 'low':
      return 'bg-slate-100 text-slate-700 ring-slate-500/20'
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

function summaryFromItems(items: AuditEvent[], totalCount: number) {
  const high = items.filter((i) => i.severity.toLowerCase() === 'high').length
  const medium = items.filter((i) => i.severity.toLowerCase() === 'medium').length
  const uniqueUsers = new Set(
    items.map((i) => i.userId).filter((id): id is string => Boolean(id)),
  ).size
  return { totalCount, high, medium, uniqueUsers }
}

export function AuditoriaPage() {
  const [type, setType] = useState('')
  const [severity, setSeverity] = useState('')
  const [page, setPage] = useState(1)

  const filters: AuditLogFilter = useMemo(
    () => ({
      type: type || undefined,
      severity: severity || undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
    [type, severity, page],
  )

  const audit = useQuery({
    queryKey: ['admin', 'audit-log', filters],
    queryFn: () => adminApi.getAuditLog(filters),
  })

  const exportMutation = useMutation({
    mutationFn: () =>
      adminApi.exportAuditLog({
        type: type || undefined,
        severity: severity || undefined,
      }),
    onSuccess: (result) => {
      const stamp = new Date().toISOString().slice(0, 10)
      downloadBlob(result.blob, result.filename ?? `kubix_auditoria_${stamp}.pdf`)
    },
  })

  const items = audit.data?.items ?? []
  const totalCount = audit.data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))
  const summary = summaryFromItems(items, totalCount)

  function updateFilter(setter: (value: string) => void, value: string) {
    setter(value)
    setPage(1)
  }

  return (
    <div className="space-y-8">
      <PageHeader
        title="Auditoría de Seguridad"
        description="Registro de eventos de seguridad con filtros por tipo y severidad."
        actions={
          <SecondaryButton
            disabled={totalCount === 0 || exportMutation.isPending}
            onClick={() => exportMutation.mutate()}
          >
            {exportMutation.isPending ? 'Generando PDF…' : 'Exportar PDF'}
          </SecondaryButton>
        }
      />

      {audit.isLoading && <LoadingState />}
      {audit.isError && (
        <ErrorBanner
          message={apiErrorMessage(audit.error, 'No se pudo cargar el registro de auditoría.')}
        />
      )}
      {exportMutation.isError && (
        <ErrorBanner
          message={apiErrorMessage(
            exportMutation.error,
            'No se pudo generar el PDF de auditoría.',
          )}
        />
      )}

      {audit.data && (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard label="Total eventos" value={String(summary.totalCount)} />
            <StatCard
              label="Severidad alta"
              value={String(summary.high)}
              hint="En la página actual"
            />
            <StatCard
              label="Severidad media"
              value={String(summary.medium)}
              hint="En la página actual"
            />
            <StatCard
              label="Usuarios únicos"
              value={String(summary.uniqueUsers)}
              hint="En la página actual"
            />
          </div>

          <div className="flex flex-wrap gap-4">
            <label className="block min-w-[10rem] text-sm">
              <span className="font-medium text-slate-700">Tipo</span>
              <select
                className={`${inputClassName} mt-1`}
                value={type}
                onChange={(e) => updateFilter(setType, e.target.value)}
                aria-label="Filtrar por tipo"
              >
                {TYPE_OPTIONS.map((opt) => (
                  <option key={opt.value || 'all-type'} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="block min-w-[10rem] text-sm">
              <span className="font-medium text-slate-700">Severidad</span>
              <select
                className={`${inputClassName} mt-1`}
                value={severity}
                onChange={(e) => updateFilter(setSeverity, e.target.value)}
                aria-label="Filtrar por severidad"
              >
                {SEVERITY_OPTIONS.map((opt) => (
                  <option key={opt.value || 'all-sev'} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <section>
            <div className="mb-3">
              <h2 className="text-lg font-semibold text-slate-900">Registro de auditoría</h2>
              <p className="mt-1 text-sm text-slate-600">
                {totalCount} evento{totalCount === 1 ? '' : 's'}
                {type || severity ? ' con los filtros aplicados' : ''}.
              </p>
            </div>

            {items.length === 0 ? (
              <EmptyState label="No hay eventos de auditoría con estos filtros." />
            ) : (
              <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
                <table className="min-w-full text-left text-sm">
                  <thead className="border-b bg-slate-50 text-slate-600">
                    <tr>
                      <th className="px-4 py-3 font-medium">Fecha</th>
                      <th className="px-4 py-3 font-medium">Acción</th>
                      <th className="px-4 py-3 font-medium">Tipo</th>
                      <th className="px-4 py-3 font-medium">Severidad</th>
                      <th className="px-4 py-3 font-medium">Usuario</th>
                      <th className="px-4 py-3 font-medium">IP</th>
                      <th className="px-4 py-3 font-medium">Dispositivo</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((event) => (
                      <tr key={event.id} className="border-b last:border-0">
                        <td className="px-4 py-3 whitespace-nowrap text-slate-700">
                          {formatDateTime(event.createdAt)}
                        </td>
                        <td className="px-4 py-3 font-medium text-slate-900">{event.action}</td>
                        <td className="px-4 py-3 text-slate-700">{typeLabel(event.type)}</td>
                        <td className="px-4 py-3">
                          <span
                            className={`inline-flex rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${severityTone(event.severity)}`}
                          >
                            {severityLabel(event.severity)}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-slate-700">{event.userName || '—'}</td>
                        <td className="px-4 py-3 font-mono text-xs text-slate-600">
                          {event.ip || '—'}
                        </td>
                        <td className="px-4 py-3 text-slate-700">{event.device || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {totalCount > 0 && (
              <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
                <p className="text-sm text-slate-600">
                  Página {page} de {totalPages}
                </p>
                <div className="flex gap-2">
                  <SecondaryButton
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    Anterior
                  </SecondaryButton>
                  <SecondaryButton
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Siguiente
                  </SecondaryButton>
                </div>
              </div>
            )}
          </section>
        </>
      )}
    </div>
  )
}

function StatCard({
  label,
  value,
  hint,
}: {
  label: string
  value: string
  hint?: string
}) {
  return (
    <div className="rounded-xl border bg-white p-5 shadow-sm">
      <div className="text-sm font-medium text-slate-500">{label}</div>
      <div className="mt-2 text-3xl font-semibold tracking-tight text-[var(--kubix-navy)]">
        {value}
      </div>
      {hint && <div className="mt-1 text-xs text-slate-400">{hint}</div>}
    </div>
  )
}
