import type { ButtonHTMLAttributes, ReactNode } from 'react'

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900">{title}</h1>
        {description && <p className="mt-1 text-sm text-slate-600">{description}</p>}
      </div>
      {actions}
    </div>
  )
}

export function StatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase()
  const tone =
    normalized === 'active'
      ? 'bg-emerald-50 text-emerald-700 ring-emerald-600/20'
      : normalized === 'suspended'
        ? 'bg-amber-50 text-amber-800 ring-amber-600/20'
        : 'bg-slate-100 text-slate-700 ring-slate-500/20'

  const label =
    normalized === 'active'
      ? 'Activa'
      : normalized === 'suspended'
        ? 'Suspendida'
        : normalized === 'blocked'
          ? 'Bloqueado'
          : status

  return (
    <span className={`inline-flex rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${tone}`}>
      {label}
    </span>
  )
}

export function PrimaryButton({
  children,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type={type}
      {...props}
      className={[
        'rounded-md bg-[var(--kubix-blue)] px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60',
        props.className ?? '',
      ].join(' ')}
    >
      {children}
    </button>
  )
}

export function SecondaryButton({
  children,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type={type}
      {...props}
      className={[
        'rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60',
        props.className ?? '',
      ].join(' ')}
    >
      {children}
    </button>
  )
}

export function DangerButton({
  children,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type={type}
      {...props}
      className={[
        'rounded-md bg-[var(--kubix-danger)] px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-60',
        props.className ?? '',
      ].join(' ')}
    >
      {children}
    </button>
  )
}

export function Dialog({
  title,
  children,
  onClose,
  footer,
}: {
  title: string
  children: ReactNode
  onClose: () => void
  footer?: ReactNode
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
      <button
        type="button"
        className="absolute inset-0 bg-slate-900/40"
        aria-label="Cerrar"
        onClick={onClose}
      />
      <div className="relative w-full max-w-lg rounded-xl border bg-white shadow-lg">
        <div className="flex items-center justify-between border-b px-5 py-4">
          <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-500 hover:text-slate-800"
            aria-label="Cerrar diálogo"
          >
            ✕
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t px-5 py-4">{footer}</div>}
      </div>
    </div>
  )
}

export function Field({
  label,
  children,
  error,
  htmlFor,
}: {
  label: string
  children: ReactNode
  error?: string
  htmlFor?: string
}) {
  return (
    <label className="block" htmlFor={htmlFor}>
      <span className="text-sm font-medium text-slate-700">{label}</span>
      <div className="mt-1">{children}</div>
      {error && (
        <p className="mt-1 text-sm text-red-600" role="alert">
          {error}
        </p>
      )}
    </label>
  )
}

export const inputClassName =
  'w-full rounded-md border px-3 py-2 text-sm outline-none focus:border-[var(--kubix-blue)] focus:ring-2 focus:ring-blue-100'

export function ErrorBanner({ message }: { message: string }) {
  return (
    <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
      {message}
    </p>
  )
}

export function LoadingState({ label = 'Cargando…' }: { label?: string }) {
  return <p className="text-sm text-slate-600">{label}</p>
}

export function EmptyState({ label }: { label: string }) {
  return <p className="text-sm text-slate-500">{label}</p>
}

export function apiErrorMessage(err: unknown, fallback: string) {
  const detail = (err as { response?: { data?: { detail?: string; title?: string } } })?.response
    ?.data
  return detail?.detail ?? detail?.title ?? fallback
}
