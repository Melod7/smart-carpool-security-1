import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { superAdminApi } from '../../api/superAdmin'
import type { UniversitySummary } from '../../api/types'
import { ErrorBanner, LoadingState, PageHeader, StatusBadge } from './ui'
import { UniversityFormDialog } from './UniversityFormDialog'

export function UniversidadesPage() {
  const queryClient = useQueryClient()
  const [editingUni, setEditingUni] = useState<UniversitySummary | null>(null)
  const [isFormOpen, setIsFormOpen] = useState(false)

  const unisQuery = useQuery({
    queryKey: ['super', 'universities'],
    queryFn: superAdminApi.listUniversities,
  })

  const suspendMutation = useMutation({
    mutationFn: (id: string) => superAdminApi.suspendUniversity(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
    },
  })

  const handleCreate = () => {
    setEditingUni(null)
    setIsFormOpen(true)
  }

  const handleEdit = (uni: UniversitySummary) => {
    setEditingUni(uni)
    setIsFormOpen(true)
  }

  const handleToggleStatus = (uni: UniversitySummary) => {
    const isActiva = uni.status.toLowerCase() === 'activa'
    const action = isActiva ? 'suspender' : 'activar'
    if (confirm(`¿Deseas ${action} la universidad "${uni.name}"?`)) {
      suspendMutation.mutate(uni.id)
    }
  }

  const handleSaved = () => {
    setIsFormOpen(false)
    queryClient.invalidateQueries({ queryKey: ['super', 'universities'] })
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <PageHeader
          title="Universidades"
          description="Provisiona universidades, campus y coordinadores."
        />
        <button
          onClick={handleCreate}
          className="rounded-lg bg-red-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm hover:bg-red-700 transition-colors"
        >
          Nueva universidad
        </button>
      </div>

      {unisQuery.isLoading && <LoadingState />}
      {unisQuery.isError && (
        <ErrorBanner message="No se pudieron cargar las universidades." />
      )}

      {unisQuery.data && (
        <div className="mt-6 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-left text-sm text-slate-600">
            <thead className="bg-slate-50 text-xs font-semibold uppercase text-slate-500 border-b border-slate-200">
              <tr>
                <th className="px-6 py-3">Nombre</th>
                <th className="px-6 py-3">Identificador</th>
                <th className="px-6 py-3">Campus</th>
                <th className="px-6 py-3">Usuarios</th>
                <th className="px-6 py-3">Estado</th>
                <th className="px-6 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {unisQuery.data.map((u) => {
                const isActiva = u.status.toLowerCase() === 'activa'
                return (
                  <tr key={u.id} className="hover:bg-slate-50/80">
                    <td className="px-6 py-4 font-medium text-red-700">{u.name}</td>
                    <td className="px-6 py-4 text-slate-500">{u.slug}</td>
                    <td className="px-6 py-4">{u.campusesCount ?? 0}</td>
                    <td className="px-6 py-4">{u.usersCount ?? 0}</td>
                    <td className="px-6 py-4">
                      <StatusBadge status={u.status} />
                    </td>
                    <td className="px-6 py-4 text-right space-x-2">
                      <button
                        onClick={() => handleEdit(u)}
                        className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 transition-colors"
                      >
                        Editar
                      </button>
                      <button
                        onClick={() => handleToggleStatus(u)}
                        disabled={suspendMutation.isPending}
                        className={`rounded-md px-3 py-1.5 text-xs font-medium text-white transition-colors disabled:opacity-50 ${
                          isActiva
                            ? 'bg-red-600 hover:bg-red-700'
                            : 'bg-emerald-600 hover:bg-emerald-700'
                        }`}
                      >
                        {isActiva ? 'Suspender' : 'Activar'}
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {isFormOpen && (
        <UniversityFormDialog
          mode={editingUni ? 'edit' : 'create'}
          initial={
            editingUni
              ? { name: editingUni.name, slug: editingUni.slug }
              : undefined
          }
          onClose={() => setIsFormOpen(false)}
          onSubmit={async (payload) => {
            if (editingUni) {
              await superAdminApi.updateUniversity(editingUni.id, payload)
            } else {
              await superAdminApi.createUniversity(payload)
            }
            handleSaved()
          }}
        />
      )}
    </div>
  )
}
