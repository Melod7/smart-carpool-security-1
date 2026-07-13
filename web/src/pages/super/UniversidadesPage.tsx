import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { superAdminApi } from '../../api/superAdmin'
import type { CreateUniversityPayload, UniversitySummary } from '../../api/types'
import { UniversityFormDialog } from './UniversityFormDialog'
import {
  DangerButton,
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  StatusBadge,
  apiErrorMessage,
  Dialog,
} from './ui'

export function UniversidadesPage() {
  const queryClient = useQueryClient()
  const [dialog, setDialog] = useState<'create' | 'edit' | null>(null)
  const [editing, setEditing] = useState<UniversitySummary | null>(null)
  const [suspending, setSuspending] = useState<UniversitySummary | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const universities = useQuery({
    queryKey: ['super', 'universities'],
    queryFn: superAdminApi.listUniversities,
  })

  const createMutation = useMutation({
    mutationFn: superAdminApi.createUniversity,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
      await queryClient.invalidateQueries({ queryKey: ['super', 'stats'] })
      setDialog(null)
      setFormError(null)
    },
    onError: (err) => {
      setFormError(apiErrorMessage(err, 'No se pudo crear la universidad.'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: CreateUniversityPayload }) =>
      superAdminApi.updateUniversity(id, { name: payload.name, slug: payload.slug }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
      setDialog(null)
      setEditing(null)
      setFormError(null)
    },
    onError: (err) => {
      setFormError(apiErrorMessage(err, 'No se pudo actualizar la universidad.'))
    },
  })

  const suspendMutation = useMutation({
    mutationFn: (id: string) => superAdminApi.suspendUniversity(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
      await queryClient.invalidateQueries({ queryKey: ['super', 'stats'] })
      setSuspending(null)
    },
  })

  function openCreate() {
    setEditing(null)
    setFormError(null)
    setDialog('create')
  }

  function openEdit(uni: UniversitySummary) {
    setEditing(uni)
    setFormError(null)
    setDialog('edit')
  }

  return (
    <div>
      <PageHeader
        title="Universidades"
        description="Provisiona tenants, campuses y coordinadores."
        actions={<PrimaryButton onClick={openCreate}>Nueva universidad</PrimaryButton>}
      />

      {universities.isLoading && <LoadingState />}
      {universities.isError && (
        <ErrorBanner message="No se pudieron cargar las universidades." />
      )}

      {universities.data && universities.data.length === 0 && (
        <EmptyState label="Aún no hay universidades registradas." />
      )}

      {universities.data && universities.data.length > 0 && (
        <div className="overflow-x-auto rounded-xl border bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b bg-slate-50 text-slate-600">
              <tr>
                <th className="px-4 py-3 font-medium">Nombre</th>
                <th className="px-4 py-3 font-medium">Slug</th>
                <th className="px-4 py-3 font-medium">Campuses</th>
                <th className="px-4 py-3 font-medium">Usuarios</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {universities.data.map((uni) => (
                <tr key={uni.id} className="border-b last:border-0">
                  <td className="px-4 py-3">
                    <Link
                      to={`/super/universidades/${uni.id}`}
                      className="font-medium text-[var(--kubix-blue)] hover:underline"
                    >
                      {uni.name}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{uni.slug}</td>
                  <td className="px-4 py-3">{uni.campusesCount}</td>
                  <td className="px-4 py-3">{uni.usersCount}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={uni.status} />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-2">
                      <SecondaryButton onClick={() => openEdit(uni)}>Editar</SecondaryButton>
                      {uni.status.toLowerCase() !== 'suspended' && (
                        <DangerButton onClick={() => setSuspending(uni)}>
                          Suspender
                        </DangerButton>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {dialog === 'create' && (
        <UniversityFormDialog
          mode="create"
          submitting={createMutation.isPending}
          error={formError}
          onClose={() => setDialog(null)}
          onSubmit={(payload) => createMutation.mutateAsync(payload)}
        />
      )}

      {dialog === 'edit' && editing && (
        <UniversityFormDialog
          mode="edit"
          initial={editing}
          submitting={updateMutation.isPending}
          error={formError}
          onClose={() => {
            setDialog(null)
            setEditing(null)
          }}
          onSubmit={(payload) =>
            updateMutation.mutateAsync({ id: editing.id, payload })
          }
        />
      )}

      {suspending && (
        <Dialog
          title="Suspender universidad"
          onClose={() => setSuspending(null)}
          footer={
            <>
              <SecondaryButton
                onClick={() => setSuspending(null)}
                disabled={suspendMutation.isPending}
              >
                Cancelar
              </SecondaryButton>
              <DangerButton
                disabled={suspendMutation.isPending}
                onClick={() => suspendMutation.mutate(suspending.id)}
              >
                {suspendMutation.isPending ? 'Suspendiendo…' : 'Confirmar suspensión'}
              </DangerButton>
            </>
          }
        >
          <p className="text-sm text-slate-600">
            ¿Suspender <span className="font-medium text-slate-900">{suspending.name}</span>?
            Los usuarios de este tenant no podrán iniciar sesión.
          </p>
          {suspendMutation.isError && (
            <p className="mt-3 text-sm text-red-600" role="alert">
              {apiErrorMessage(suspendMutation.error, 'No se pudo suspender la universidad.')}
            </p>
          )}
        </Dialog>
      )}
    </div>
  )
}
