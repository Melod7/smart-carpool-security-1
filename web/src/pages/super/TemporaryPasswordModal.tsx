import { useState } from 'react'
import { Dialog, PrimaryButton, SecondaryButton } from './ui'

export function TemporaryPasswordModal({
  title,
  email,
  temporaryPassword,
  onClose,
}: {
  title: string
  email?: string
  temporaryPassword: string
  onClose: () => void
}) {
  const [copied, setCopied] = useState(false)

  async function copyPassword() {
    try {
      await navigator.clipboard.writeText(temporaryPassword)
      setCopied(true)
    } catch {
      setCopied(false)
    }
  }

  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <PrimaryButton onClick={onClose}>Entendido</PrimaryButton>
      }
    >
      <div className="space-y-3">
        <p className="text-sm text-slate-600">
          Guarda esta contraseña temporal ahora. Solo se muestra una vez.
        </p>
        {email && (
          <p className="text-sm text-slate-700">
            Correo: <span className="font-medium">{email}</span>
          </p>
        )}
        <div className="flex items-center gap-2 rounded-md border bg-slate-50 px-3 py-2">
          <code className="flex-1 break-all font-mono text-sm text-slate-900">
            {temporaryPassword}
          </code>
          <SecondaryButton onClick={() => void copyPassword()}>
            {copied ? 'Copiado' : 'Copiar'}
          </SecondaryButton>
        </div>
      </div>
    </Dialog>
  )
}
