import { api } from './cliente'
import type {
  AdminDashboard,
  AdminReport,
  AdminUsersFilter,
  AdminUsersPage,
  AlertaSosAdmin,
  AuditLogFilter,
  AuditLogPage,
  PublicUniversity,
  RegistrationRequest,
  ReportExportFormat,
  ReportExportResult,
  ReportPeriod,
  UsuarioAdmin,
} from './types'

function filenameFromContentDisposition(header?: string | null): string | null {
  if (!header) return null
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(header)
  if (utf8?.[1]) {
    try {
      return decodeURIComponent(utf8[1].trim())
    } catch {
      return utf8[1].trim()
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(header)
  return plain?.[1]?.trim() || null
}

function usersQueryParams(filters: AdminUsersFilter = {}) {
  const params: Record<string, string | number> = {}
  if (filters.status) params.status = filters.status
  if (filters.role) params.role = filters.role
  if (filters.campus) params.campus = filters.campus
  if (filters.search) params.search = filters.search
  if (filters.page != null) params.page = filters.page
  if (filters.pageSize != null) params.pageSize = filters.pageSize
  return params
}

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

  listRegistrationRequests: async () => {
    const { data } = await api.get<RegistrationRequest[]>('/admin/registration-requests')
    return data
  },

  acceptRegistrationRequest: async (id: string) => {
    await api.post(`/admin/registration-requests/${id}/accept`)
  },

  denyRegistrationRequest: async (id: string) => {
    await api.post(`/admin/registration-requests/${id}/deny`)
  },

  listUsers: async (filters: AdminUsersFilter = {}): Promise<AdminUsersPage> => {
    const { data, headers } = await api.get<UsuarioAdmin[]>('/admin/users', {
      params: usersQueryParams(filters),
    })
    const totalHeader = headers['x-total-count'] ?? headers['X-Total-Count']
    const total = Number(totalHeader ?? data.length)
    return { items: data, total: Number.isFinite(total) ? total : data.length }
  },

  exportUsers: async (filters: Omit<AdminUsersFilter, 'page' | 'pageSize'> = {}) => {
    const { data } = await api.get<Blob>('/admin/users/export', {
      params: usersQueryParams(filters),
      responseType: 'blob',
    })
    return data
  },

  blockUser: async (id: string) => {
    await api.post(`/admin/users/${id}/block`)
  },

  unblockUser: async (id: string) => {
    await api.post(`/admin/users/${id}/unblock`)
  },

  listPublicUniversities: async () => {
    const { data } = await api.get<PublicUniversity[]>('/public/universities')
    return data
  },

  getReports: async (period: ReportPeriod = 'semanal') => {
    const { data } = await api.get<AdminReport>('/admin/reports', {
      params: { period },
    })
    return data
  },

  exportReports: async (
    period: ReportPeriod,
    format: ReportExportFormat,
  ): Promise<ReportExportResult> => {
    const { data, headers } = await api.get<Blob>('/admin/reports/export', {
      params: { period, format },
      responseType: 'blob',
    })
    const disposition =
      (headers['content-disposition'] as string | undefined) ??
      (headers['Content-Disposition'] as string | undefined)
    return {
      blob: data,
      filename: filenameFromContentDisposition(disposition),
    }
  },

  getAuditLog: async (filters: AuditLogFilter = {}): Promise<AuditLogPage> => {
    const params: Record<string, string | number> = {}
    if (filters.type) params.type = filters.type
    if (filters.severity) params.severity = filters.severity
    if (filters.page != null) params.page = filters.page
    if (filters.pageSize != null) params.pageSize = filters.pageSize
    const { data } = await api.get<AuditLogPage>('/admin/audit-log', { params })
    return data
  },
}
