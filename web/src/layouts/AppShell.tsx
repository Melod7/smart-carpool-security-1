import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { notificationsApi } from '../api/cliente'

type NavItem = { to: string; label: string }

const adminNav: NavItem[] = [
  { to: '/admin', label: 'Panel' },
  { to: '/admin/usuarios', label: 'Usuarios' },
  { to: '/admin/reportes', label: 'Reportes' },
  { to: '/admin/auditoria', label: 'Auditoría' },
  { to: '/admin/configuracion', label: 'Configuración' },
  { to: '/admin/tracking', label: 'Tracking' },
]

const superNav: NavItem[] = [
  { to: '/super', label: 'Estadísticas' },
  { to: '/super/universidades', label: 'Universidades' },
]

function navClass({ isActive }: { isActive: boolean }) {
  return [
    'block rounded-md px-3 py-2 text-sm transition-colors',
    isActive ? 'bg-blue-600/30 text-white' : 'text-slate-300 hover:bg-white/5 hover:text-white',
  ].join(' ')
}

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const isCoordinador = user?.role === 'coordinador'
  const nav = isCoordinador ? adminNav : superNav

  const notifications = useQuery({
    queryKey: ['admin', 'notifications'],
    queryFn: notificationsApi.list,
    enabled: isCoordinador,
    refetchInterval: 30_000,
  })

  const unreadCount = notifications.data?.filter((n) => !n.read).length ?? 0

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-screen flex bg-[var(--kubix-bg)]">
      <aside className="w-60 bg-[var(--kubix-navy)] text-white p-6 flex flex-col shrink-0">
        <div className="text-lg font-bold tracking-tight">Kubix UTN</div>
        <div className="text-xs text-slate-400 mt-1">
          {isCoordinador ? 'Consola Coordinador' : 'Consola Super Admin'}
        </div>

        <nav className="mt-8 space-y-1" aria-label="Navegación principal">
          {nav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/admin' || item.to === '/super'}
              className={navClass}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="mt-auto pt-6 border-t border-white/10 text-xs text-slate-400">
          {user?.email}
        </div>
      </aside>

      <div className="flex-1 flex flex-col min-w-0">
        <header className="h-14 border-b bg-white px-6 flex items-center justify-between gap-4">
          <div className="text-sm text-slate-500 truncate">
            {user?.name ?? 'Usuario'}
          </div>

          <div className="flex items-center gap-3">
            {isCoordinador && (
              <button
                type="button"
                className="relative rounded-md border px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
                aria-label="Notificaciones"
                title="Ver auditoría de seguridad"
                onClick={() => navigate('/admin/auditoria')}
              >
                Notificaciones
                {unreadCount > 0 && (
                  <span className="absolute -top-1.5 -right-1.5 min-w-5 h-5 px-1 rounded-full bg-red-600 text-white text-[11px] leading-5 text-center font-semibold">
                    {unreadCount > 99 ? '99+' : unreadCount}
                  </span>
                )}
              </button>
            )}

            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-[var(--kubix-blue)] text-white text-xs font-semibold flex items-center justify-center">
                {(user?.name ?? 'U').slice(0, 1).toUpperCase()}
              </div>
              <button
                type="button"
                onClick={() => void handleLogout()}
                className="text-sm text-slate-600 hover:text-slate-900"
              >
                Cerrar sesión
              </button>
            </div>
          </div>
        </header>

        <main className="flex-1 p-6 md:p-8 overflow-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
