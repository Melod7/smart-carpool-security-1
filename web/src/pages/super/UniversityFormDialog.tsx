import { useState, type FormEvent } from 'react'
import type { CreateUniversityPayload, UniversitySummary } from '../../api/types'
import { Dialog, Field, PrimaryButton, SecondaryButton, inputClassName } from './ui'

export type UniversityFormValues = {
  name: string
  slug: string
  allowedEmailDomain: string
}

export type UniversityFormErrors = {
  name?: string
  slug?: string
}

export function validateUniversityForm(
  values: Pick<UniversityFormValues, 'name' | 'slug'>,
): UniversityFormErrors {
  const errors: UniversityFormErrors = {}
  if (!values.name.trim()) {
    errors.name = 'El nombre es obligatorio.'
  }
  if (!values.slug.trim()) {
    errors.slug = 'El slug es obligatorio.'
  }
  return errors
}

export function UniversityFormDialog({
  mode,
  initial,
  submitting,
  error,
  onClose,
  onSubmit,
}: {
  mode: 'create' | 'edit'
  initial?: Pick<UniversitySummary, 'name' | 'slug'> & { allowedEmailDomain?: string | null }
  submitting?: boolean
  error?: string | null
  onClose: () => void
  onSubmit: (payload: CreateUniversityPayload) => void | Promise<unknown>
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [slug, setSlug] = useState(initial?.slug ?? '')
  const [allowedEmailDomain, setAllowedEmailDomain] = useState(
    initial?.allowedEmailDomain ?? '',
  )
  const [errors, setErrors] = useState<UniversityFormErrors>({})

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const nextErrors = validateUniversityForm({ name, slug })
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return

    const payload: CreateUniversityPayload = {
      name: name.trim(),
      slug: slug.trim(),
    }
    const domain = allowedEmailDomain.trim()
    if (mode === 'create' && domain) {
      payload.allowedEmailDomain = domain
    }
    await onSubmit(payload)
  }

  return (
    <Dialog
      title={mode === 'create' ? 'Nueva universidad' : 'Editar universidad'}
      onClose={onClose}
      footer={
        <>
          <SecondaryButton onClick={onClose} disabled={submitting}>
            Cancelar
          </SecondaryButton>
          <PrimaryButton
            type="submit"
            form="university-form"
            disabled={submitting}
          >
            {submitting ? 'Guardando…' : mode === 'create' ? 'Crear' : 'Guardar'}
          </PrimaryButton>
        </>
      }
    >
      <form id="university-form" onSubmit={(e) => void handleSubmit(e)} className="space-y-4" noValidate>
        <Field label="Nombre" htmlFor="university-name" error={errors.name}>
          <input
            id="university-name"
            name="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className={inputClassName}
            aria-invalid={Boolean(errors.name)}
          />
        </Field>

        <Field label="Slug" htmlFor="university-slug" error={errors.slug}>
          <input
            id="university-slug"
            name="slug"
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            className={inputClassName}
            aria-invalid={Boolean(errors.slug)}
            placeholder="ej. utn"
          />
        </Field>

        {mode === 'create' && (
          <Field label="Dominio de correo (opcional)" htmlFor="university-domain">
            <input
              id="university-domain"
              name="allowedEmailDomain"
              value={allowedEmailDomain}
              onChange={(e) => setAllowedEmailDomain(e.target.value)}
              className={inputClassName}
              placeholder="ej. utn.edu.ec"
            />
          </Field>
        )}

        {error && (
          <p className="text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
      </form>
    </Dialog>
  )
}
