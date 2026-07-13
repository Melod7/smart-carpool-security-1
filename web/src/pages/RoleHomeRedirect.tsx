import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function RoleHomeRedirect() {
  const { isAuthenticated, isBootstrapping, mustChangePassword, user } = useAuth()

  if (isBootstrapping) {
    return (
      <div className="min-h-screen flex items-center justify-center text-slate-600">
        Cargando…
      </div>
    )
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />
  }

  if (mustChangePassword) {
    return <Navigate to="/change-password" replace />
  }

  if (user.role === 'super_admin') {
    return <Navigate to="/super" replace />
  }

  if (user.role === 'coordinador') {
    return <Navigate to="/admin" replace />
  }

  return <Navigate to="/login" replace />
}
