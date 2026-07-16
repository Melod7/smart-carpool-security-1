import { useMemo, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '../../api/admin'
import type { RegistrationRequest, UsuarioAdmin, VehicleJson } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import {
  DangerButton,
  Dialog,
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  apiErrorMessage,
  inputClassName,
} from '../super/ui'

const PAGE_SIZE = 20

type ConfirmAction =
  | { kind: 'accept'; request: RegistrationRequest }
  | { kind: 'deny'; request: RegistrationRequest }
  | { kind: 'block'; user: UsuarioAdmin }
  | { kind: 'unblock'; user: UsuarioAdmin }
  | { kind: 'delete'; user: UsuarioAdmin }

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-EC', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function roleLabel(role: string) {
  switch (role.toLowerCase()) {
    case 'driver':
      return 'Conductor'
    case 'passenger':
      return 'Pasajero'
    case 'coordinador':
      return 'Coordinador'
    default:
      return role
  }
}

function userStatusLabel(status: string) {
  switch (status.toLowerCase()) {
    case 'active':
      return 'Activo'
    case 'blocked':
      return 'Bloqueado'
    case 'pending':
      return 'Pendiente'
    case 'denied':
      return 'Denegado'
    default:
      return status
  }
}

function userStatusTone(status: string) {
  switch (status.toLowerCase()) {
    case 'active':
      return 'bg-emerald-50 text-emerald-700 ring-emerald-600/20'
    case 'blocked':
      return 'bg-red-50 text-red-700 ring-red-600/20'
    case 'pending':
      return 'bg-amber-50 text-amber-800 ring-amber-600/20'
    default:
      return 'bg-slate-100 text-slate-700 ring-slate-500/20'
  }
}

function parseVehicle(json?: string | null): VehicleJson | null {
  if (!json) return null
  try {
    return JSON.parse(json) as VehicleJson
  } catch {
    return null
  }
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}

export function UsuariosPage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()

  const [status, setStatus] = useState('')
  const [role, setRole] = useState('')
  const [campus, setCampus] = useState('')
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [page, setPage] = useState(1)
  const [detail, setDetail] = useState<RegistrationRequest | null>(null)
  const [confirm, setConfirm] = useState<ConfirmAction | null>(null)

  const filters = useMemo(
    () => ({
      status: status || undefined,
      role: role || undefined,
      campus: campus || undefined,
      search: search || undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
    [status, role, campus, search, page],
  )

  const requests = useQuery({
    queryKey: ['admin', 'registration-requests'],
    queryFn: adminApi.listRegistrationRequests,
  })

  const users = useQuery({
    queryKey: ['admin', 'users', filters],
    queryFn: () => adminApi.listUsers(filters),
  })

  const totals = useQuery({
    queryKey: ['admin', 'users', 'totals'],
    queryFn: async () => {
      const [all, blocked] = await Promise.all([
        adminApi.listUsers({ page: 1, pageSize: 1 }),
        adminApi.listUsers({ status: 'blocked', page: 1, pageSize: 1 }),
      ])
      return { total: all.total, blocked: blocked.total }
    },
  })

  const universities = useQuery({
    queryKey: ['public', 'universities'],
    queryFn: adminApi.listPublicUniversities,
  })

  const campuses = useMemo(() => {
    const uni = universities.data?.find((u) => u.id === user?.universityId)
    return uni?.campuses ?? []
  }, [universities.data, user?.universityId])

  const campusNameById = useMemo(() => {
    const map = new Map<string, string>()
    for (const c of campuses) map.set(c.id, c.name)
    for (const req of requests.data ?? []) {
      if (req.campusId && req.campusName) map.set(req.campusId, req.campusName)
    }
    return map
  }, [campuses, requests.data])

  const acceptMutation = useMutation({
    mutationFn: (id: string) => adminApi.acceptRegistrationRequest(id),
    onSuccess: async () => {
      await invalidateUsers()
      setConfirm(null)
      setDetail(null)
    },
  })

  const denyMutation = useMutation({
    mutationFn: (id: string) => adminApi.denyRegistrationRequest(id),
    onSuccess: async () => {
      await invalidateUsers()
      setConfirm(null)
      setDetail(null)
    },
  })

  const blockMutation = useMutation({
    mutationFn: (id: string) => adminApi.blockUser(id),
    onSuccess: async () => {
      await invalidateUsers()
      setConfirm(null)
    },
  })

  const unblockMutation = useMutation({
    mutationFn: (id: string) => adminApi.unblockUser(id),
    onSuccess: async () => {
      await invalidateUsers()
      setConfirm(null)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => adminApi.deleteUser(id),
    onSuccess: async () => {
      await invalidateUsers()
      setConfirm(null)
    },
  })

  const exportMutation = useMutation({
    mutationFn: () =>
      adminApi.exportUsers({
        status: status || undefined,
        role: role || undefined,
        campus: campus || undefined,
        search: search || undefined,
      }),
    onSuccess: (blob) => {
      downloadBlob(blob, 'kubix_users.csv')
    },
  })

  async function invalidateUsers() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['admin', 'registration-requests'] }),
      queryClient.invalidateQueries({ queryKey: ['admin', 'users'] }),
    ])
  }

  function applySearch(event: FormEvent) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
  }

  function updateFilter(setter: (value: string) => void, value: string) {
    setter(value)
    setPage(1)
  }

  const pendingCount = requests.data?.length ?? 0
  const totalUsers = totals.data?.total ?? users.data?.total ?? 0
  const blockedUsers = totals.data?.blocked ?? 0
  const totalPages = Math.max(1, Math.ceil((users.data?.total ?? 0) / PAGE_SIZE))
  const confirming = Boolean(
    acceptMutation.isPending ||
      denyMutation.isPending ||
      blockMutation.isPending ||
      unblockMutation.isPending ||
      deleteMutation.isPending,
  )

  return (
    <div className="space-y-8">
      <PageHeader
        title="Gestión de Usuarios"
        description="Aprueba registros, filtra el directorio y bloquea cuentas cuando sea necesario."
        actions={
          <PrimaryButton
            disabled={exportMutation.isPending}
            onClick={() => exportMutation.mutate()}
          >
            {exportMutation.isPending ? 'Exportando…' : 'Exportar CSV'}
          </PrimaryButton>
        }
      />

      {exportMutation.isError && (
        <ErrorBanner
          message={apiErrorMessage(exportMutation.error, 'No se pudo exportar el CSV.')}
        />
      )}

      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard label="Solicitudes pendientes" value={String(pendingCount)} />
        <StatCard label="Total usuarios" value={String(totalUsers)} />
        <StatCard label="Usuarios bloqueados" value={String(blockedUsers)} />
      </div>

      <section>
        <div className="mb-3">
          <h2 className="text-lg font-semibold text-slate-900">Solicitudes pendientes</h2>
          <p className="mt-1 text-sm text-slate-600">
            Altas y cambios de perfil, rol o vehículo. Revisa la información antes de aceptar o denegar.
          </p>
        </div>

        {requests.isLoading && <LoadingState />}
        {requests.isError && (
          <ErrorBanner
            message={apiErrorMessage(requests.error, 'No se pudieron cargar las solicitudes.')}
          />
        )}
        {requests.data && requests.data.length === 0 && (
          <EmptyState label="No hay solicitudes pendientes." />
        )}
        {requests.data && requests.data.length > 0 && (
          <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
            <table className="min-w-full text-left text-sm">
              <thead className="border-b bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-3 font-medium">Tipo</th>
                  <th className="px-4 py-3 font-medium">Nombre</th>
                  <th className="px-4 py-3 font-medium">Rol</th>
                  <th className="px-4 py-3 font-medium">Campus</th>
                  <th className="px-4 py-3 font-medium">Carrera</th>
                  <th className="px-4 py-3 font-medium">Solicitado</th>
                  <th className="px-4 py-3 font-medium">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {requests.data.map((req) => (
                  <tr key={req.id} className="border-b last:border-0">
                    <td className="px-4 py-3">
                      <span
                        className={
                          req.kind === 'vehicle_change'
                            ? 'rounded-full bg-[var(--secondary)] px-2 py-0.5 text-xs font-medium text-[var(--accent)]'
                            : 'rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700'
                        }
                      >
                        {req.kind === 'vehicle_change'
                          ? 'Cambio vehículo'
                          : req.kind === 'role_change_driver'
                            ? 'Cambio a conductor'
                            : req.kind === 'profile_change'
                              ? 'Cambio de perfil'
                            : 'Registro'}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-slate-900">{req.name}</div>
                      <div className="text-xs text-slate-500">{req.email}</div>
                    </td>
                    <td className="px-4 py-3 text-slate-700">{roleLabel(req.role)}</td>
                    <td className="px-4 py-3 text-slate-700">
                      {req.campusName ?? campusNameById.get(req.campusId) ?? '—'}
                    </td>
                    <td className="px-4 py-3 text-slate-700">{req.career ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-700">{formatDateTime(req.createdAt)}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-2">
                        <SecondaryButton onClick={() => setDetail(req)}>Detalle</SecondaryButton>
                        <PrimaryButton onClick={() => setConfirm({ kind: 'accept', request: req })}>
                          Aceptar
                        </PrimaryButton>
                        <DangerButton onClick={() => setConfirm({ kind: 'deny', request: req })}>
                          Denegar
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
        <div className="mb-3">
          <h2 className="text-lg font-semibold text-slate-900">Directorio de usuarios</h2>
          <p className="mt-1 text-sm text-slate-600">
            Filtra por estado, rol, campus o texto libre. La paginación usa el total del servidor.
          </p>
        </div>

        <form
          onSubmit={applySearch}
          className="mb-4 grid gap-3 rounded-xl border bg-white p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-5"
        >
          <label className="block text-sm">
            <span className="font-medium text-slate-700">Estado</span>
            <select
              className={`${inputClassName} mt-1`}
              value={status}
              onChange={(e) => updateFilter(setStatus, e.target.value)}
              aria-label="Filtrar por estado"
            >
              <option value="">Todos</option>
              <option value="active">Activo</option>
              <option value="blocked">Bloqueado</option>
              <option value="pending">Pendiente</option>
            </select>
          </label>

          <label className="block text-sm">
            <span className="font-medium text-slate-700">Rol</span>
            <select
              className={`${inputClassName} mt-1`}
              value={role}
              onChange={(e) => updateFilter(setRole, e.target.value)}
              aria-label="Filtrar por rol"
            >
              <option value="">Todos</option>
              <option value="driver">Conductor</option>
              <option value="passenger">Pasajero</option>
            </select>
          </label>

          <label className="block text-sm">
            <span className="font-medium text-slate-700">Campus</span>
            <select
              className={`${inputClassName} mt-1`}
              value={campus}
              onChange={(e) => updateFilter(setCampus, e.target.value)}
              aria-label="Filtrar por campus"
            >
              <option value="">Todos</option>
              {campuses.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>

          <label className="block text-sm sm:col-span-2 lg:col-span-2">
            <span className="font-medium text-slate-700">Búsqueda</span>
            <div className="mt-1 flex gap-2">
              <input
                className={inputClassName}
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Nombre, correo o cédula"
                aria-label="Buscar usuarios"
              />
              <SecondaryButton type="submit">Buscar</SecondaryButton>
            </div>
          </label>
        </form>

        {users.isLoading && <LoadingState />}
        {users.isError && (
          <ErrorBanner
            message={apiErrorMessage(users.error, 'No se pudieron cargar los usuarios.')}
          />
        )}
        {users.data && users.data.items.length === 0 && (
          <EmptyState label="No hay usuarios con estos filtros." />
        )}
        {users.data && users.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
              <table className="min-w-full text-left text-sm">
                <thead className="border-b bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-3 font-medium">Nombre</th>
                    <th className="px-4 py-3 font-medium">Rol</th>
                    <th className="px-4 py-3 font-medium">Estado</th>
                    <th className="px-4 py-3 font-medium">Campus</th>
                    <th className="px-4 py-3 font-medium">Carrera</th>
                    <th className="px-4 py-3 font-medium">Rating</th>
                    <th className="px-4 py-3 font-medium">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {users.data.items.map((u) => (
                    <tr key={u.id} className="border-b last:border-0">
                      <td className="px-4 py-3">
                        <div className="font-medium text-slate-900">{u.name}</div>
                        <div className="text-xs text-slate-500">{u.email}</div>
                      </td>
                      <td className="px-4 py-3 text-slate-700">{roleLabel(u.role)}</td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${userStatusTone(u.status)}`}
                        >
                          {userStatusLabel(u.status)}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-slate-700">
                        {u.campusId ? (campusNameById.get(u.campusId) ?? '—') : '—'}
                      </td>
                      <td className="px-4 py-3 text-slate-700">{u.career ?? '—'}</td>
                      <td className="px-4 py-3 tabular-nums text-slate-700">
                        {Number(u.ratingAvg).toFixed(1)}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap gap-2">
                          {u.status.toLowerCase() === 'blocked' ? (
                            <PrimaryButton
                              onClick={() => setConfirm({ kind: 'unblock', user: u })}
                            >
                              Desbloquear
                            </PrimaryButton>
                          ) : u.status.toLowerCase() === 'active' ? (
                            <SecondaryButton
                              onClick={() => setConfirm({ kind: 'block', user: u })}
                            >
                              Bloquear
                            </SecondaryButton>
                          ) : null}
                          {['driver', 'passenger'].includes(u.role.toLowerCase()) && (
                            <DangerButton
                              onClick={() => setConfirm({ kind: 'delete', user: u })}
                            >
                              Eliminar
                            </DangerButton>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600">
              <span>
                Página {page} de {totalPages} · {users.data.total} resultado
                {users.data.total === 1 ? '' : 's'}
              </span>
              <div className="flex gap-2">
                <SecondaryButton
                  disabled={page <= 1 || users.isFetching}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Anterior
                </SecondaryButton>
                <SecondaryButton
                  disabled={page >= totalPages || users.isFetching}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Siguiente
                </SecondaryButton>
              </div>
            </div>
          </>
        )}
      </section>

      {detail && (
        <RequestDetailDialog
          request={detail}
          campusName={detail.campusName ?? campusNameById.get(detail.campusId) ?? '—'}
          onClose={() => setDetail(null)}
          onAccept={() => setConfirm({ kind: 'accept', request: detail })}
          onDeny={() => setConfirm({ kind: 'deny', request: detail })}
        />
      )}

      {confirm && (
        <ConfirmActionDialog
          action={confirm}
          pending={confirming}
          error={
            confirm.kind === 'accept' && acceptMutation.isError
              ? apiErrorMessage(acceptMutation.error, 'No se pudo aceptar la solicitud.')
              : confirm.kind === 'deny' && denyMutation.isError
                ? apiErrorMessage(denyMutation.error, 'No se pudo denegar la solicitud.')
                : confirm.kind === 'block' && blockMutation.isError
                  ? apiErrorMessage(blockMutation.error, 'No se pudo bloquear el usuario.')
                  : confirm.kind === 'unblock' && unblockMutation.isError
                    ? apiErrorMessage(unblockMutation.error, 'No se pudo desbloquear el usuario.')
                    : confirm.kind === 'delete' && deleteMutation.isError
                      ? apiErrorMessage(deleteMutation.error, 'No se pudo eliminar el usuario.')
                    : null
          }
          onClose={() => setConfirm(null)}
          onConfirm={() => {
            if (confirm.kind === 'accept') acceptMutation.mutate(confirm.request.id)
            if (confirm.kind === 'deny') denyMutation.mutate(confirm.request.id)
            if (confirm.kind === 'block') blockMutation.mutate(confirm.user.id)
            if (confirm.kind === 'unblock') unblockMutation.mutate(confirm.user.id)
            if (confirm.kind === 'delete') deleteMutation.mutate(confirm.user.id)
          }}
        />
      )}
    </div>
  )
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border bg-white p-5 shadow-sm">
      <div className="text-sm font-medium text-slate-500">{label}</div>
      <div className="mt-2 text-3xl font-semibold tracking-tight text-[var(--kubix-navy)]">
        {value}
      </div>
    </div>
  )
}

function RequestDetailDialog({
  request,
  campusName,
  onClose,
  onAccept,
  onDeny,
}: {
  request: RegistrationRequest
  campusName: string
  onClose: () => void
  onAccept: () => void
  onDeny: () => void
}) {
  const vehicle = parseVehicle(request.vehicleJson)

  return (
    <Dialog
      title={
        request.kind === 'vehicle_change'
          ? 'Detalle · cambio de vehículo'
          : request.kind === 'role_change_driver'
            ? 'Detalle · cambio a conductor'
            : request.kind === 'profile_change'
              ? 'Detalle · cambio de perfil'
              : 'Detalle de solicitud'
      }
      onClose={onClose}
      footer={
        <>
          <SecondaryButton onClick={onClose}>Cerrar</SecondaryButton>
          <DangerButton onClick={onDeny}>Denegar</DangerButton>
          <PrimaryButton onClick={onAccept}>Aceptar</PrimaryButton>
        </>
      }
    >
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <DetailItem label="Nombre" value={request.name} />
        <DetailItem label="Correo" value={request.email} />
        <DetailItem label="Rol" value={roleLabel(request.role)} />
        <DetailItem label="Campus" value={campusName} />
        <DetailItem label="Carrera" value={request.career ?? '—'} />
        <DetailItem label="Cédula / ID" value={request.idNumber ?? '—'} />
        <DetailItem
          label="Género"
          value={request.gender === 'female' ? 'Mujer' : request.gender === 'male' ? 'Hombre' : '—'}
        />
        <DetailItem label="Estado" value={userStatusLabel(request.status)} />
        <DetailItem label="Solicitado" value={formatDateTime(request.createdAt)} />
      </dl>

      {request.profileImage && (
        <div className="mt-4">
          <p className="mb-2 text-sm font-medium text-slate-800">Imagen de perfil</p>
          <img
            src={request.profileImage}
            alt={`Perfil de ${request.name}`}
            className="h-40 w-40 rounded-lg border object-cover"
          />
        </div>
      )}

      {(request.role.toLowerCase() === 'driver' || request.kind === 'role_change_driver') && (
        <div className="mt-4 rounded-md border border-slate-200 bg-slate-50 px-3 py-3">
          <p className="text-sm font-medium text-slate-800">Vehículo</p>
          {vehicle ? (
            <dl className="mt-2 grid gap-2 text-sm sm:grid-cols-2">
              <DetailItem label="Marca / modelo" value={vehicle.makeModel ?? '—'} />
              <DetailItem label="Placa" value={vehicle.plate ?? '—'} />
              <DetailItem label="Color" value={vehicle.color ?? '—'} />
              <DetailItem
                label="Asientos"
                value={vehicle.seatsTotal != null ? String(vehicle.seatsTotal) : '—'}
              />
            </dl>
          ) : (
            <p className="mt-1 text-sm text-slate-600">
              {request.vehicleJson ?? 'Sin información de vehículo.'}
            </p>
          )}
          {vehicle?.image && (
            <img
              src={vehicle.image}
              alt={`Vehículo de ${request.name}`}
              className="mt-3 h-44 w-full rounded-lg border object-cover"
            />
          )}
        </div>
      )}
    </Dialog>
  )
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-0.5 text-slate-900">{value}</dd>
    </div>
  )
}

function ConfirmActionDialog({
  action,
  pending,
  error,
  onClose,
  onConfirm,
}: {
  action: ConfirmAction
  pending: boolean
  error: string | null
  onClose: () => void
  onConfirm: () => void
}) {
  const isDanger = action.kind === 'deny' || action.kind === 'block' || action.kind === 'delete'
  const title =
    action.kind === 'accept'
      ? 'Aceptar solicitud'
      : action.kind === 'deny'
        ? 'Denegar solicitud'
        : action.kind === 'block'
          ? 'Bloquear usuario'
          : action.kind === 'unblock'
            ? 'Desbloquear usuario'
            : 'Eliminar usuario'

  const name =
    action.kind === 'accept' || action.kind === 'deny' ? action.request.name : action.user.name

  const message =
    action.kind === 'accept'
      ? action.request.kind === 'profile_change'
        ? `¿Aceptar los cambios de perfil solicitados por ${name}?`
        : action.request.kind === 'registration' || !action.request.kind
          ? `¿Aceptar el registro de ${name}? Podrá iniciar sesión de inmediato.`
          : `¿Aceptar la solicitud de ${name}?`
      : action.kind === 'deny'
        ? `¿Denegar la solicitud de ${name}? No se aplicarán cambios.`
        : action.kind === 'block'
          ? `¿Bloquear a ${name}? Perderá el acceso de inmediato.`
          : action.kind === 'unblock'
            ? `¿Desbloquear a ${name}? Recuperará el acceso a la plataforma.`
            : `¿Eliminar a ${name}? Se cancelarán sus actividades futuras y no podrá volver a iniciar sesión.`

  const confirmLabel =
    action.kind === 'accept'
      ? 'Confirmar aceptación'
      : action.kind === 'deny'
        ? 'Confirmar denegación'
        : action.kind === 'block'
          ? 'Confirmar bloqueo'
          : action.kind === 'unblock'
            ? 'Confirmar desbloqueo'
            : 'Eliminar usuario'

  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <SecondaryButton onClick={onClose} disabled={pending}>
            Cancelar
          </SecondaryButton>
          {isDanger ? (
            <DangerButton disabled={pending} onClick={onConfirm}>
              {pending ? 'Procesando…' : confirmLabel}
            </DangerButton>
          ) : (
            <PrimaryButton disabled={pending} onClick={onConfirm}>
              {pending ? 'Procesando…' : confirmLabel}
            </PrimaryButton>
          )}
        </>
      }
    >
      <p className="text-sm text-slate-600">{message}</p>
      {error && (
        <p className="mt-3 text-sm text-red-600" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  )
}
