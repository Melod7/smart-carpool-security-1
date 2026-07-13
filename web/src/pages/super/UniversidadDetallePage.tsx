import { useMemo, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { superAdminApi } from '../../api/superAdmin'
import type { Campus, CampusPayload, Coordinador } from '../../api/types'
import { TemporaryPasswordModal } from './TemporaryPasswordModal'
import {
  DangerButton,
  Dialog,
  EmptyState,
  ErrorBanner,
  Field,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  StatusBadge,
  apiErrorMessage,
  inputClassName,
} from './ui'

type TempPasswordState = {
  title: string
  email?: string
  temporaryPassword: string
}

type CampusFormState = {
  mode: 'create' | 'edit'
  campus?: Campus
}

export function UniversidadDetallePage() {
  const { id = '' } = useParams<{ id: string }>()
  const queryClient = useQueryClient()

  const [campusForm, setCampusForm] = useState<CampusFormState | null>(null)
  const [deletingCampus, setDeletingCampus] = useState<Campus | null>(null)
  const [coordFormOpen, setCoordFormOpen] = useState(false)
  const [tempPassword, setTempPassword] = useState<TempPasswordState | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const universities = useQuery({
    queryKey: ['super', 'universities'],
    queryFn: superAdminApi.listUniversities,
  })

  const university = useMemo(
    () => universities.data?.find((u) => u.id === id) ?? null,
    [universities.data, id],
  )

  const campuses = useQuery({
    queryKey: ['super', 'universities', id, 'campuses'],
    queryFn: () => superAdminApi.listCampuses(id),
    enabled: Boolean(id),
  })

  const coordinadores = useQuery({
    queryKey: ['super', 'universities', id, 'coordinadores'],
    queryFn: () => superAdminApi.listCoordinadores(id),
    enabled: Boolean(id),
  })

  const createCampus = useMutation({
    mutationFn: (payload: CampusPayload) => superAdminApi.createCampus(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities', id, 'campuses'] })
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
      setCampusForm(null)
      setFormError(null)
    },
    onError: (err) => setFormError(apiErrorMessage(err, 'No se pudo crear el campus.')),
  })

  const updateCampus = useMutation({
    mutationFn: ({ campusId, payload }: { campusId: string; payload: CampusPayload }) =>
      superAdminApi.updateCampus(id, campusId, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities', id, 'campuses'] })
      setCampusForm(null)
      setFormError(null)
    },
    onError: (err) => setFormError(apiErrorMessage(err, 'No se pudo actualizar el campus.')),
  })

  const deleteCampus = useMutation({
    mutationFn: (campusId: string) => superAdminApi.deleteCampus(id, campusId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities', id, 'campuses'] })
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
      setDeletingCampus(null)
    },
  })

  const createCoordinador = useMutation({
    mutationFn: (payload: { name: string; email: string }) =>
      superAdminApi.createCoordinador(id, payload),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({
        queryKey: ['super', 'universities', id, 'coordinadores'],
      })
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
      setCoordFormOpen(false)
      setFormError(null)
      setTempPassword({
        title: 'Contraseña temporal del coordinador',
        email: created.email,
        temporaryPassword: created.temporaryPassword,
      })
    },
    onError: (err) => setFormError(apiErrorMessage(err, 'No se pudo crear el coordinador.')),
  })

  const resetPassword = useMutation({
    mutationFn: (coordinadorId: string) => superAdminApi.resetCoordinadorPassword(coordinadorId),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({
        queryKey: ['super', 'universities', id, 'coordinadores'],
      })
      setTempPassword({
        title: 'Contraseña restablecida',
        email: result.email,
        temporaryPassword: result.temporaryPassword,
      })
    },
  })

  if (universities.isLoading) {
    return <LoadingState />
  }

  if (!university) {
    return (
      <div>
        <ErrorBanner message="Universidad no encontrada." />
        <Link to="/super/universidades" className="mt-4 inline-block text-sm text-[var(--kubix-blue)]">
          Volver al listado
        </Link>
      </div>
    )
  }

  return (
    <div className="space-y-8">
      <div>
        <Link
          to="/super/universidades"
          className="text-sm text-[var(--kubix-blue)] hover:underline"
        >
          ← Universidades
        </Link>
        <PageHeader
          title={university.name}
          description={`Slug: ${university.slug}`}
          actions={<StatusBadge status={university.status} />}
        />
      </div>

      <section>
        <div className="mb-3 flex items-center justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900">Campuses</h2>
          <PrimaryButton
            onClick={() => {
              setFormError(null)
              setCampusForm({ mode: 'create' })
            }}
          >
            Nuevo campus
          </PrimaryButton>
        </div>

        {campuses.isLoading && <LoadingState />}
        {campuses.isError && <ErrorBanner message="No se pudieron cargar los campuses." />}
        {campuses.data && campuses.data.length === 0 && (
          <EmptyState label="No hay campuses en esta universidad." />
        )}
        {campuses.data && campuses.data.length > 0 && (
          <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
            <table className="min-w-full text-left text-sm">
              <thead className="border-b bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-3 font-medium">Nombre</th>
                  <th className="px-4 py-3 font-medium">Dirección</th>
                  <th className="px-4 py-3 font-medium">Lat</th>
                  <th className="px-4 py-3 font-medium">Lng</th>
                  <th className="px-4 py-3 font-medium">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {campuses.data.map((campus) => (
                  <tr key={campus.id} className="border-b last:border-0">
                    <td className="px-4 py-3 font-medium">{campus.name}</td>
                    <td className="px-4 py-3 text-slate-600">{campus.address}</td>
                    <td className="px-4 py-3">{campus.lat}</td>
                    <td className="px-4 py-3">{campus.lng}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-2">
                        <SecondaryButton
                          onClick={() => {
                            setFormError(null)
                            setCampusForm({ mode: 'edit', campus })
                          }}
                        >
                          Editar
                        </SecondaryButton>
                        <DangerButton onClick={() => setDeletingCampus(campus)}>
                          Eliminar
                        </DangerButton>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <div className="mb-3 flex items-center justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900">Coordinadores</h2>
          <PrimaryButton
            onClick={() => {
              setFormError(null)
              setCoordFormOpen(true)
            }}
          >
            Nuevo coordinador
          </PrimaryButton>
        </div>

        {coordinadores.isLoading && <LoadingState />}
        {coordinadores.isError && (
          <ErrorBanner message="No se pudieron cargar los coordinadores." />
        )}
        {coordinadores.data && coordinadores.data.length === 0 && (
          <EmptyState label="No hay coordinadores en esta universidad." />
        )}
        {coordinadores.data && coordinadores.data.length > 0 && (
          <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
            <table className="min-w-full text-left text-sm">
              <thead className="border-b bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-3 font-medium">Nombre</th>
                  <th className="px-4 py-3 font-medium">Correo</th>
                  <th className="px-4 py-3 font-medium">Estado</th>
                  <th className="px-4 py-3 font-medium">Cambio de clave</th>
                  <th className="px-4 py-3 font-medium">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {coordinadores.data.map((coord) => (
                  <CoordinadorRow
                    key={coord.id}
                    coordinador={coord}
                    resetting={resetPassword.isPending}
                    onReset={() => resetPassword.mutate(coord.id)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
        {resetPassword.isError && (
          <p className="mt-2 text-sm text-red-600" role="alert">
            {apiErrorMessage(resetPassword.error, 'No se pudo restablecer la contraseña.')}
          </p>
        )}
      </section>

      {campusForm && (
        <CampusFormDialog
          mode={campusForm.mode}
          initial={campusForm.campus}
          submitting={createCampus.isPending || updateCampus.isPending}
          error={formError}
          onClose={() => setCampusForm(null)}
          onSubmit={async (payload) => {
            if (campusForm.mode === 'create') {
              await createCampus.mutateAsync(payload)
            } else if (campusForm.campus) {
              await updateCampus.mutateAsync({
                campusId: campusForm.campus.id,
                payload,
              })
            }
          }}
        />
      )}

      {deletingCampus && (
        <Dialog
          title="Eliminar campus"
          onClose={() => setDeletingCampus(null)}
          footer={
            <>
              <SecondaryButton
                onClick={() => setDeletingCampus(null)}
                disabled={deleteCampus.isPending}
              >
                Cancelar
              </SecondaryButton>
              <DangerButton
                disabled={deleteCampus.isPending}
                onClick={() => deleteCampus.mutate(deletingCampus.id)}
              >
                {deleteCampus.isPending ? 'Eliminando…' : 'Eliminar'}
              </DangerButton>
            </>
          }
        >
          <p className="text-sm text-slate-600">
            ¿Eliminar el campus{' '}
            <span className="font-medium text-slate-900">{deletingCampus.name}</span>?
          </p>
          {deleteCampus.isError && (
            <p className="mt-3 text-sm text-red-600" role="alert">
              {apiErrorMessage(deleteCampus.error, 'No se pudo eliminar el campus.')}
            </p>
          )}
        </Dialog>
      )}

      {coordFormOpen && (
        <CoordinadorFormDialog
          submitting={createCoordinador.isPending}
          error={formError}
          onClose={() => setCoordFormOpen(false)}
          onSubmit={(payload) => createCoordinador.mutateAsync(payload)}
        />
      )}

      {tempPassword && (
        <TemporaryPasswordModal
          title={tempPassword.title}
          email={tempPassword.email}
          temporaryPassword={tempPassword.temporaryPassword}
          onClose={() => setTempPassword(null)}
        />
      )}
    </div>
  )
}

function CoordinadorRow({
  coordinador,
  resetting,
  onReset,
}: {
  coordinador: Coordinador
  resetting: boolean
  onReset: () => void
}) {
  return (
    <tr className="border-b last:border-0">
      <td className="px-4 py-3 font-medium">{coordinador.name}</td>
      <td className="px-4 py-3 text-slate-600">{coordinador.email}</td>
      <td className="px-4 py-3">
        <StatusBadge status={coordinador.status} />
      </td>
      <td className="px-4 py-3 text-slate-600">
        {coordinador.mustChangePassword ? 'Pendiente' : 'Actualizada'}
      </td>
      <td className="px-4 py-3">
        <SecondaryButton disabled={resetting} onClick={onReset}>
          Resetear contraseña
        </SecondaryButton>
      </td>
    </tr>
  )
}

function CampusFormDialog({
  mode,
  initial,
  submitting,
  error,
  onClose,
  onSubmit,
}: {
  mode: 'create' | 'edit'
  initial?: Campus
  submitting?: boolean
  error?: string | null
  onClose: () => void
  onSubmit: (payload: CampusPayload) => void | Promise<unknown>
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [address, setAddress] = useState(initial?.address ?? '')
  const [lat, setLat] = useState(initial ? String(initial.lat) : '')
  const [lng, setLng] = useState(initial ? String(initial.lng) : '')
  const [errors, setErrors] = useState<Record<string, string>>({})

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next: Record<string, string> = {}
    if (!name.trim()) next.name = 'El nombre es obligatorio.'
    if (!address.trim()) next.address = 'La dirección es obligatoria.'
    const latNum = Number(lat)
    const lngNum = Number(lng)
    if (lat === '' || Number.isNaN(latNum)) next.lat = 'Latitud inválida.'
    if (lng === '' || Number.isNaN(lngNum)) next.lng = 'Longitud inválida.'
    setErrors(next)
    if (Object.keys(next).length > 0) return

    await onSubmit({
      name: name.trim(),
      address: address.trim(),
      lat: latNum,
      lng: lngNum,
    })
  }

  return (
    <Dialog
      title={mode === 'create' ? 'Nuevo campus' : 'Editar campus'}
      onClose={onClose}
      footer={
        <>
          <SecondaryButton onClick={onClose} disabled={submitting}>
            Cancelar
          </SecondaryButton>
          <PrimaryButton type="submit" form="campus-form" disabled={submitting}>
            {submitting ? 'Guardando…' : mode === 'create' ? 'Crear' : 'Guardar'}
          </PrimaryButton>
        </>
      }
    >
      <form id="campus-form" onSubmit={(e) => void handleSubmit(e)} className="space-y-4" noValidate>
        <Field label="Nombre" htmlFor="campus-name" error={errors.name}>
          <input
            id="campus-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className={inputClassName}
          />
        </Field>
        <Field label="Dirección" htmlFor="campus-address" error={errors.address}>
          <input
            id="campus-address"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            className={inputClassName}
          />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Latitud" htmlFor="campus-lat" error={errors.lat}>
            <input
              id="campus-lat"
              value={lat}
              onChange={(e) => setLat(e.target.value)}
              className={inputClassName}
              inputMode="decimal"
            />
          </Field>
          <Field label="Longitud" htmlFor="campus-lng" error={errors.lng}>
            <input
              id="campus-lng"
              value={lng}
              onChange={(e) => setLng(e.target.value)}
              className={inputClassName}
              inputMode="decimal"
            />
          </Field>
        </div>
        {error && (
          <p className="text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
      </form>
    </Dialog>
  )
}

function CoordinadorFormDialog({
  submitting,
  error,
  onClose,
  onSubmit,
}: {
  submitting?: boolean
  error?: string | null
  onClose: () => void
  onSubmit: (payload: { name: string; email: string }) => void | Promise<unknown>
}) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next: Record<string, string> = {}
    if (!name.trim()) next.name = 'El nombre es obligatorio.'
    if (!email.trim()) next.email = 'El correo es obligatorio.'
    setErrors(next)
    if (Object.keys(next).length > 0) return
    await onSubmit({ name: name.trim(), email: email.trim() })
  }

  return (
    <Dialog
      title="Nuevo coordinador"
      onClose={onClose}
      footer={
        <>
          <SecondaryButton onClick={onClose} disabled={submitting}>
            Cancelar
          </SecondaryButton>
          <PrimaryButton type="submit" form="coord-form" disabled={submitting}>
            {submitting ? 'Creando…' : 'Crear'}
          </PrimaryButton>
        </>
      }
    >
      <form id="coord-form" onSubmit={(e) => void handleSubmit(e)} className="space-y-4" noValidate>
        <Field label="Nombre" htmlFor="coord-name" error={errors.name}>
          <input
            id="coord-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className={inputClassName}
          />
        </Field>
        <Field label="Correo" htmlFor="coord-email" error={errors.email}>
          <input
            id="coord-email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className={inputClassName}
          />
        </Field>
        {error && (
          <p className="text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
      </form>
    </Dialog>
  )
}
