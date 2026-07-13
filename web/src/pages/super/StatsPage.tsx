import { useQuery } from '@tanstack/react-query'
import { superAdminApi } from '../../api/superAdmin'
import { ErrorBanner, LoadingState, PageHeader } from './ui'

const kpiCards = [
  { key: 'universitiesCount' as const, label: 'Universidades' },
  { key: 'totalUsers' as const, label: 'Usuarios totales' },
  { key: 'tripsToday' as const, label: 'Viajes hoy' },
  { key: 'activeSosCount' as const, label: 'SOS activos' },
]

export function StatsPage() {
  const stats = useQuery({
    queryKey: ['super', 'stats'],
    queryFn: superAdminApi.getStats,
  })

  return (
    <div>
      <PageHeader
        title="Estadísticas globales"
        description="Resumen de la plataforma across todas las universidades."
      />

      {stats.isLoading && <LoadingState />}
      {stats.isError && (
        <ErrorBanner message="No se pudieron cargar las estadísticas." />
      )}

      {stats.data && (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {kpiCards.map((card) => (
            <div
              key={card.key}
              className="rounded-xl border bg-white p-5 shadow-sm"
            >
              <div className="text-sm font-medium text-slate-500">{card.label}</div>
              <div className="mt-2 text-3xl font-semibold tracking-tight text-[var(--kubix-navy)]">
                {stats.data[card.key]}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
