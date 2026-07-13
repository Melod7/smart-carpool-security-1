import { api } from './cliente'
import type { AdminDashboard, AlertaSosAdmin } from './types'

export const adminApi = {
  getDashboard: async () => {
    const { data } = await api.get<AdminDashboard>('/admin/dashboard')
    return data
  },

  listSos: async () => {
    const { data } = await api.get<AlertaSosAdmin[]>('/admin/sos')
    return data
  },

  resolveSos: async (id: string) => {
    await api.post(`/admin/sos/${id}/resolve`)
  },
}
