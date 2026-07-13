import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { UserRole } from '../api/types'

type RequireAuthProps = {
  roles?: UserRole[]
  allowPasswordChange?: boolean
}

export function RequireAuth({ roles, allowPasswordChange = false }: RequireAuthProps) {
  const { isAuthenticated, isBootstrapping, mustChangePassword, user, accessToken } = useAuth()
  const location = useLocation()

  if (isBootstrapping) {
    return (
      <div className="min-h-screen flex items-center justify-center text-slate-600">
        Cargando sesión…
      </div>
    )
  }

  if (!isAuthenticated || !accessToken || !user) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (mustChangePassword && !allowPasswordChange) {
    return <Navigate to="/change-password" replace />
  }

  if (roles && roles.length > 0 && !roles.includes(user.role)) {
    const home = user.role === 'super_admin' ? '/super' : '/admin'
    return <Navigate to={home} replace />
  }

  return <Outlet />
}
