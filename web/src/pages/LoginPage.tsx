import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { AxiosError } from 'axios'
import { useAuth } from '../auth/AuthContext'
import type { UserRole } from '../api/types'

function homeForRole(role: UserRole) {
  return role === 'super_admin' ? '/super' : '/admin'
}

export function LoginPage() {
  const { login, isAuthenticated, mustChangePassword, user, isBootstrapping } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  if (isBootstrapping) {
    return (
      <div className="min-h-screen flex items-center justify-center text-slate-600">
        Cargando…
      </div>
    )
  }

  if (isAuthenticated && user) {
    if (mustChangePassword) {
      return <Navigate to="/change-password" replace />
    }
    const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname
    return <Navigate to={from && from !== '/login' ? from : homeForRole(user.role)} replace />
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const nextUser = await login(email.trim(), password)
      if (nextUser.mustChangePassword) {
        navigate('/change-password', { replace: true })
        return
      }
      navigate(homeForRole(nextUser.role), { replace: true })
    } catch (err) {
      const axiosErr = err as AxiosError<{ detail?: string; title?: string }>
      setError(
        axiosErr.response?.data?.detail
          ?? axiosErr.response?.data?.title
          ?? 'No se pudo iniciar sesión. Verifica tus credenciales.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-[var(--kubix-bg)] px-4">
      <div className="w-full max-w-md rounded-xl border bg-white p-8 shadow-sm">
        <div className="mb-8">
          <h1 className="text-2xl font-bold text-[var(--kubix-navy)]">kubix 2.0</h1>
          <p className="mt-1 text-sm text-slate-500">Inicia sesión en la consola administrativa</p>
        </div>

        <form onSubmit={onSubmit} className="space-y-4">
          <label className="block">
            <span className="text-sm font-medium text-slate-700">Correo</span>
            <input
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="mt-1 w-full rounded-md border px-3 py-2 text-sm outline-none focus:border-[var(--kubix-blue)] focus:ring-2 focus:ring-blue-100"
            />
          </label>

          <label className="block">
            <span className="text-sm font-medium text-slate-700">Contraseña</span>
            <input
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="mt-1 w-full rounded-md border px-3 py-2 text-sm outline-none focus:border-[var(--kubix-blue)] focus:ring-2 focus:ring-blue-100"
            />
          </label>

          {error && (
            <p className="text-sm text-red-600" role="alert">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-md bg-[var(--kubix-blue)] px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {submitting ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  )
}
