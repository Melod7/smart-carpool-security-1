import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { AxiosError } from 'axios'
import { useAuth } from '../auth/AuthContext'

export function ChangePasswordPage() {
  const { changePassword, user, mustChangePassword } = useAuth()
  const navigate = useNavigate()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)

    if (newPassword.length < 8) {
      setError('La nueva contraseña debe tener al menos 8 caracteres.')
      return
    }
    if (newPassword !== confirmPassword) {
      setError('Las contraseñas no coinciden.')
      return
    }

    setSubmitting(true)
    try {
      await changePassword(currentPassword, newPassword)
      const home = user?.role === 'super_admin' ? '/super' : '/admin'
      navigate(home, { replace: true })
    } catch (err) {
      const axiosErr = err as AxiosError<{ detail?: string; title?: string }>
      setError(
        axiosErr.response?.data?.detail
          ?? axiosErr.response?.data?.title
          ?? 'No se pudo cambiar la contraseña.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-[var(--kubix-bg)] px-4">
      <div className="w-full max-w-md rounded-xl border bg-white p-8 shadow-sm">
        <h1 className="text-2xl font-bold text-[var(--kubix-navy)]">Cambiar contraseña</h1>
        <p className="mt-2 text-sm text-slate-500">
          {mustChangePassword
            ? 'Debes cambiar tu contraseña temporal antes de continuar.'
            : 'Actualiza tu contraseña de acceso.'}
        </p>

        <form onSubmit={onSubmit} className="mt-6 space-y-4">
          <label className="block">
            <span className="text-sm font-medium text-slate-700">Contraseña actual</span>
            <input
              type="password"
              autoComplete="current-password"
              required
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              className="mt-1 w-full rounded-md border px-3 py-2 text-sm outline-none focus:border-[var(--kubix-blue)] focus:ring-2 focus:ring-blue-100"
            />
          </label>

          <label className="block">
            <span className="text-sm font-medium text-slate-700">Nueva contraseña</span>
            <input
              type="password"
              autoComplete="new-password"
              required
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="mt-1 w-full rounded-md border px-3 py-2 text-sm outline-none focus:border-[var(--kubix-blue)] focus:ring-2 focus:ring-blue-100"
            />
          </label>

          <label className="block">
            <span className="text-sm font-medium text-slate-700">Confirmar nueva contraseña</span>
            <input
              type="password"
              autoComplete="new-password"
              required
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
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
            {submitting ? 'Guardando…' : 'Guardar contraseña'}
          </button>
        </form>
      </div>
    </div>
  )
}
