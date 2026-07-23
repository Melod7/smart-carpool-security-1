import { useQuery } from '@tanstack/react-query'
import { adminApi } from '../../api/admin'
import {
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  apiErrorMessage,
} from '../super/ui'

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-EC', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function CanjesPage() {
  const canjes = useQuery({
    queryKey: ['admin', 'eco', 'redemptions'],
    queryFn: () => adminApi.listPrizeRedemptions(50),
  })

  return (
    <div className="space-y-8">
      <PageHeader
        title="Canjes EcoTokensUTN"
        description="Premios canjeados por estudiantes. Entrégalos en el campus (gorra, camiseta o mochila UTN)."
      />

      {canjes.isLoading && <LoadingState />}
      {canjes.isError && (
        <ErrorBanner
          message={apiErrorMessage(canjes.error, 'No se pudieron cargar los canjes.')}
        />
      )}

      {canjes.data && canjes.data.length === 0 && (
        <EmptyState label="Aún no hay canjes de premios." />
      )}

      {canjes.data && canjes.data.length > 0 && (
        <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b bg-slate-50 text-slate-600">
              <tr>
                <th className="px-4 py-3 font-medium">Fecha</th>
                <th className="px-4 py-3 font-medium">Estudiante</th>
                <th className="px-4 py-3 font-medium">Premio</th>
                <th className="px-4 py-3 font-medium">Costo</th>
              </tr>
            </thead>
            <tbody>
              {canjes.data.map((item) => (
                <tr key={item.id} className="border-b last:border-0">
                  <td className="px-4 py-3 whitespace-nowrap text-slate-700">
                    {formatDateTime(item.createdAt)}
                  </td>
                  <td className="px-4 py-3 font-medium text-slate-900">{item.userName}</td>
                  <td className="px-4 py-3 text-slate-700">{item.prizeName}</td>
                  <td className="px-4 py-3 tabular-nums text-slate-700">{item.cost} ECT</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
        <p className="font-medium text-slate-800">Catálogo fijo</p>
        <ul className="mt-2 list-disc space-y-1 pl-5">
          <li>Gorra UTN — 40 ECT</li>
          <li>Camiseta UTN — 80 ECT</li>
          <li>Mochila UTN — 120 ECT</li>
        </ul>
      </div>
    </div>
  )
}
