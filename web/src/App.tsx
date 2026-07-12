import { useQuery } from '@tanstack/react-query'
import axios from 'axios'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

type HealthResponse = {
  status: string
  service?: string
  database?: string
  utc?: string
}

async function fetchHealth(): Promise<HealthResponse> {
  const { data } = await axios.get<HealthResponse>(`${apiUrl}/health`)
  return data
}

export default function App() {
  const health = useQuery({
    queryKey: ['health'],
    queryFn: fetchHealth,
    refetchInterval: 15_000,
  })

  return (
    <div className="min-h-screen flex">
      <aside className="w-60 bg-[var(--kubix-navy)] text-white p-6 flex flex-col">
        <div className="text-lg font-bold tracking-tight">Kubix UTN</div>
        <div className="text-xs text-slate-400 mt-1">Consola Admin · esqueleto</div>
        <nav className="mt-8 text-sm text-slate-300 space-y-2">
          <div className="rounded bg-blue-600/30 px-3 py-2 text-white">Panel de Control</div>
          <div className="px-3 py-2 opacity-50">Próximamente en KBX-14+</div>
        </nav>
      </aside>

      <main className="flex-1 p-8">
        <h1 className="text-2xl font-semibold text-slate-900">Entorno local</h1>
        <p className="mt-2 text-slate-600 max-w-xl">
          Fase 1 (KBX-1) — monorepo cableado. URL de la API:{' '}
          <code className="text-sm bg-white border px-1.5 py-0.5 rounded">{apiUrl}</code>
        </p>

        <div className="mt-8 rounded-lg border bg-white p-5 max-w-lg shadow-sm">
          <div className="text-sm font-medium text-slate-500 uppercase tracking-wide">
            Health de la API
          </div>
          {health.isLoading && <p className="mt-3 text-slate-600">Comprobando…</p>}
          {health.isError && (
            <p className="mt-3 text-red-600">
              No se puede alcanzar la API. Iníciala con{' '}
              <code className="text-xs">docker compose up -d --build</code>
            </p>
          )}
          {health.data && (
            <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
              <div>
                <dt className="text-slate-500">Estado</dt>
                <dd className="font-semibold text-emerald-700">{health.data.status}</dd>
              </div>
              <div>
                <dt className="text-slate-500">Base de datos</dt>
                <dd className="font-semibold">{health.data.database ?? '—'}</dd>
              </div>
              <div className="col-span-2">
                <dt className="text-slate-500">UTC</dt>
                <dd className="font-mono text-xs">{health.data.utc ?? '—'}</dd>
              </div>
            </dl>
          )}
        </div>
      </main>
    </div>
  )
}
