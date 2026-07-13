import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '../../api/admin'
import type { AdminSettings } from '../../api/types'
import {
  ErrorBanner,
  Field,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  apiErrorMessage,
  inputClassName,
} from '../super/ui'

export type SettingsFormValues = {
  timezone: string
  supportEmail: string
  allowedEmailDomain: string
  maxDailyTrips: string
  minDriverRating: string
  co2FactorKgKm: string
  gamificationEnabled: boolean
  co2TrackingEnabled: boolean
  notifySos: boolean
  notifyBlock: boolean
  notifyWeeklyReport: boolean
}

export type SettingsFormErrors = {
  timezone?: string
  supportEmail?: string
  maxDailyTrips?: string
  minDriverRating?: string
  co2FactorKgKm?: string
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function toFormValues(settings: AdminSettings): SettingsFormValues {
  return {
    timezone: settings.timezone,
    supportEmail: settings.supportEmail,
    allowedEmailDomain: settings.allowedEmailDomain ?? '',
    maxDailyTrips: String(settings.maxDailyTrips),
    minDriverRating: String(settings.minDriverRating),
    co2FactorKgKm: String(settings.co2FactorKgKm),
    gamificationEnabled: settings.gamificationEnabled,
    co2TrackingEnabled: settings.co2TrackingEnabled,
    notifySos: settings.notifySos,
    notifyBlock: settings.notifyBlock,
    notifyWeeklyReport: settings.notifyWeeklyReport,
  }
}

function serializeForm(values: SettingsFormValues) {
  return JSON.stringify(values)
}

export function validateSettingsForm(values: SettingsFormValues): SettingsFormErrors {
  const errors: SettingsFormErrors = {}

  if (!values.timezone.trim()) {
    errors.timezone = 'La zona horaria es obligatoria.'
  }

  const email = values.supportEmail.trim()
  if (!email) {
    errors.supportEmail = 'El correo de soporte es obligatorio.'
  } else if (!EMAIL_RE.test(email)) {
    errors.supportEmail = 'Ingresa un correo electrónico válido.'
  }

  const maxTrips = Number(values.maxDailyTrips)
  if (values.maxDailyTrips.trim() === '' || Number.isNaN(maxTrips)) {
    errors.maxDailyTrips = 'Ingresa un número válido.'
  } else if (maxTrips <= 0) {
    errors.maxDailyTrips = 'Debe ser mayor que 0.'
  } else if (!Number.isInteger(maxTrips)) {
    errors.maxDailyTrips = 'Debe ser un número entero.'
  }

  const minRating = Number(values.minDriverRating)
  if (values.minDriverRating.trim() === '' || Number.isNaN(minRating)) {
    errors.minDriverRating = 'Ingresa un número válido.'
  } else if (minRating < 0 || minRating > 5) {
    errors.minDriverRating = 'Debe estar entre 0 y 5.'
  }

  const co2Factor = Number(values.co2FactorKgKm)
  if (values.co2FactorKgKm.trim() === '' || Number.isNaN(co2Factor)) {
    errors.co2FactorKgKm = 'Ingresa un número válido.'
  } else if (co2Factor < 0) {
    errors.co2FactorKgKm = 'No puede ser negativo.'
  }

  return errors
}

function toPayload(values: SettingsFormValues): AdminSettings {
  return {
    timezone: values.timezone.trim(),
    supportEmail: values.supportEmail.trim(),
    allowedEmailDomain: values.allowedEmailDomain.trim() || null,
    maxDailyTrips: Number(values.maxDailyTrips),
    minDriverRating: Number(values.minDriverRating),
    co2FactorKgKm: Number(values.co2FactorKgKm),
    gamificationEnabled: values.gamificationEnabled,
    co2TrackingEnabled: values.co2TrackingEnabled,
    notifySos: values.notifySos,
    notifyBlock: values.notifyBlock,
    notifyWeeklyReport: values.notifyWeeklyReport,
  }
}

function Section({
  title,
  description,
  children,
}: {
  title: string
  description?: string
  children: ReactNode
}) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4">
        <h2 className="text-base font-semibold text-slate-900">{title}</h2>
        {description && <p className="mt-1 text-sm text-slate-600">{description}</p>}
      </div>
      <div className="space-y-4">{children}</div>
    </section>
  )
}

function ToggleField({
  id,
  label,
  description,
  checked,
  onChange,
}: {
  id: string
  label: string
  description?: string
  checked: boolean
  onChange: (value: boolean) => void
}) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div>
        <label htmlFor={id} className="text-sm font-medium text-slate-700">
          {label}
        </label>
        {description && <p className="mt-0.5 text-sm text-slate-500">{description}</p>}
      </div>
      <input
        id={id}
        type="checkbox"
        role="switch"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="mt-1 h-4 w-4 rounded border-slate-300 text-[var(--kubix-blue)] focus:ring-[var(--kubix-blue)]"
        aria-checked={checked}
      />
    </div>
  )
}

export function ConfiguracionPage() {
  const queryClient = useQueryClient()
  const settingsQuery = useQuery({
    queryKey: ['admin', 'settings'],
    queryFn: () => adminApi.getSettings(),
  })

  const [form, setForm] = useState<SettingsFormValues | null>(null)
  const [baseline, setBaseline] = useState<SettingsFormValues | null>(null)
  const [errors, setErrors] = useState<SettingsFormErrors>({})
  const [saveError, setSaveError] = useState<string | null>(null)
  const [savedMessage, setSavedMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!settingsQuery.data) return
    const values = toFormValues(settingsQuery.data)
    setForm(values)
    setBaseline(values)
    setErrors({})
    setSaveError(null)
  }, [settingsQuery.data])

  const dirty = useMemo(() => {
    if (!form || !baseline) return false
    return serializeForm(form) !== serializeForm(baseline)
  }, [form, baseline])

  useEffect(() => {
    if (!dirty) return
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [dirty])

  const saveMutation = useMutation({
    mutationFn: (payload: AdminSettings) => adminApi.updateSettings(payload),
    onSuccess: (data) => {
      const values = toFormValues(data)
      setForm(values)
      setBaseline(values)
      setErrors({})
      setSaveError(null)
      setSavedMessage('Configuración guardada.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] })
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] })
    },
    onError: (err) => {
      setSavedMessage(null)
      setSaveError(apiErrorMessage(err, 'No se pudo guardar la configuración.'))
    },
  })

  function updateField<K extends keyof SettingsFormValues>(key: K, value: SettingsFormValues[K]) {
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev))
    setSavedMessage(null)
    setSaveError(null)
  }

  function handleCancel() {
    if (!baseline) return
    setForm(baseline)
    setErrors({})
    setSaveError(null)
    setSavedMessage(null)
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!form) return

    const nextErrors = validateSettingsForm(form)
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return

    setSaveError(null)
    await saveMutation.mutateAsync(toPayload(form))
  }

  if (settingsQuery.isLoading || (!form && !settingsQuery.isError)) {
    return (
      <div>
        <PageHeader
          title="Configuración"
          description="Ajustes generales, reglas de registro y notificaciones de la universidad."
        />
        <LoadingState label="Cargando configuración…" />
      </div>
    )
  }

  if (settingsQuery.isError || !form) {
    return (
      <div>
        <PageHeader
          title="Configuración"
          description="Ajustes generales, reglas de registro y notificaciones de la universidad."
        />
        <ErrorBanner
          message={apiErrorMessage(settingsQuery.error, 'No se pudo cargar la configuración.')}
        />
      </div>
    )
  }

  return (
    <div>
      <PageHeader
        title="Configuración"
        description="Ajustes generales, reglas de registro y notificaciones de la universidad."
      />

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-6" noValidate>
        <Section title="General" description="Información básica de la universidad.">
          <Field label="Zona horaria" htmlFor="settings-timezone" error={errors.timezone}>
            <input
              id="settings-timezone"
              name="timezone"
              value={form.timezone}
              onChange={(e) => updateField('timezone', e.target.value)}
              className={inputClassName}
              placeholder="America/Guayaquil"
              aria-invalid={Boolean(errors.timezone)}
            />
          </Field>

          <Field
            label="Correo de soporte"
            htmlFor="settings-support-email"
            error={errors.supportEmail}
          >
            <input
              id="settings-support-email"
              name="supportEmail"
              type="email"
              value={form.supportEmail}
              onChange={(e) => updateField('supportEmail', e.target.value)}
              className={inputClassName}
              placeholder="soporte@universidad.edu.ec"
              aria-invalid={Boolean(errors.supportEmail)}
            />
          </Field>

          <Field label="Dominio de correo permitido" htmlFor="settings-email-domain">
            <input
              id="settings-email-domain"
              name="allowedEmailDomain"
              value={form.allowedEmailDomain}
              onChange={(e) => updateField('allowedEmailDomain', e.target.value)}
              className={inputClassName}
              placeholder="universidad.edu.ec (opcional)"
            />
          </Field>
        </Section>

        <Section
          title="Reglas de registro"
          description="Límites operativos para conductores y cálculo de CO₂."
        >
          <Field
            label="Máximo de viajes diarios por conductor"
            htmlFor="settings-max-trips"
            error={errors.maxDailyTrips}
          >
            <input
              id="settings-max-trips"
              name="maxDailyTrips"
              type="number"
              min={1}
              step={1}
              value={form.maxDailyTrips}
              onChange={(e) => updateField('maxDailyTrips', e.target.value)}
              className={inputClassName}
              aria-invalid={Boolean(errors.maxDailyTrips)}
            />
          </Field>

          <Field
            label="Calificación mínima del conductor"
            htmlFor="settings-min-rating"
            error={errors.minDriverRating}
          >
            <input
              id="settings-min-rating"
              name="minDriverRating"
              type="number"
              min={0}
              max={5}
              step={0.1}
              value={form.minDriverRating}
              onChange={(e) => updateField('minDriverRating', e.target.value)}
              className={inputClassName}
              aria-invalid={Boolean(errors.minDriverRating)}
            />
          </Field>

          <Field
            label="Factor CO₂ (kg/km)"
            htmlFor="settings-co2-factor"
            error={errors.co2FactorKgKm}
          >
            <input
              id="settings-co2-factor"
              name="co2FactorKgKm"
              type="number"
              min={0}
              step={0.001}
              value={form.co2FactorKgKm}
              onChange={(e) => updateField('co2FactorKgKm', e.target.value)}
              className={inputClassName}
              aria-invalid={Boolean(errors.co2FactorKgKm)}
            />
          </Field>
        </Section>

        <Section title="Funciones y notificaciones">
          <ToggleField
            id="settings-gamification"
            label="Gamificación (EcoTokens)"
            description="Si se desactiva, no se acumulan EcoTokens."
            checked={form.gamificationEnabled}
            onChange={(v) => updateField('gamificationEnabled', v)}
          />
          <ToggleField
            id="settings-co2-tracking"
            label="Seguimiento de CO₂"
            description="Calcula CO₂ ahorrado al completar viajes."
            checked={form.co2TrackingEnabled}
            onChange={(v) => updateField('co2TrackingEnabled', v)}
          />
          <ToggleField
            id="settings-notify-sos"
            label="Notificar alertas SOS"
            checked={form.notifySos}
            onChange={(v) => updateField('notifySos', v)}
          />
          <ToggleField
            id="settings-notify-block"
            label="Notificar bloqueos de usuarios"
            checked={form.notifyBlock}
            onChange={(v) => updateField('notifyBlock', v)}
          />
          <ToggleField
            id="settings-notify-weekly"
            label="Notificar reporte semanal"
            checked={form.notifyWeeklyReport}
            onChange={(v) => updateField('notifyWeeklyReport', v)}
          />
        </Section>

        {saveError && <ErrorBanner message={saveError} />}
        {savedMessage && (
          <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
            {savedMessage}
          </p>
        )}

        <div className="flex flex-wrap items-center gap-3">
          <PrimaryButton type="submit" disabled={saveMutation.isPending || !dirty}>
            {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
          </PrimaryButton>
          <SecondaryButton type="button" onClick={handleCancel} disabled={!dirty || saveMutation.isPending}>
            Cancelar
          </SecondaryButton>
          {dirty && <span className="text-sm text-slate-500">Hay cambios sin guardar</span>}
        </div>
      </form>
    </div>
  )
}
