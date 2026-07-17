import { api } from './cliente'
import type {
  Campus,
  CampusPayload,
  Coordinador,
  CoordinadorCreado,
  CreateCoordinadorPayload,
  CreateUniversityPayload,
  ResetPasswordResponse,
  SuperStats,
  UniversityDetail,
  UniversitySummary,
  UpdateUniversityPayload,
} from './types'

export const superAdminApi = {
  getStats: async () => {
    const { data } = await api.get<SuperStats>('/super/stats')
    return data
  },

  listUniversities: async () => {
    const { data } = await api.get<UniversitySummary[]>('/super/universities')
    return data
  },

  createUniversity: async (payload: CreateUniversityPayload) => {
    const { data } = await api.post<UniversityDetail>('/super/universities', payload)
    return data
  },

  updateUniversity: async (id: string, payload: UpdateUniversityPayload) => {
    const { data } = await api.put<UniversityDetail>(`/super/universities/${id}`, payload)
    return data
  },

  suspendUniversity: async (id: string) => {
    await api.post(`/super/universities/${id}/suspend`)
  },

  listCampuses: async (universityId: string) => {
    const { data } = await api.get<Campus[]>(`/super/universities/${universityId}/campuses`)
    return data
  },

  createCampus: async (universityId: string, payload: CampusPayload) => {
    const { data } = await api.post<Campus>(
      `/super/universities/${universityId}/campuses`,
      payload,
    )
    return data
  },

  updateCampus: async (universityId: string, campusId: string, payload: CampusPayload) => {
    const { data } = await api.put<Campus>(
      `/super/universities/${universityId}/campuses/${campusId}`,
      payload,
    )
    return data
  },

  deleteCampus: async (universityId: string, campusId: string) => {
    await api.delete(`/super/universities/${universityId}/campuses/${campusId}`)
  },

  listCoordinadores: async (universityId: string) => {
    const { data } = await api.get<Coordinador[]>(
      `/super/universities/${universityId}/coordinadores`,
    )
    return data
  },

  createCoordinador: async (universityId: string, payload: CreateCoordinadorPayload) => {
    const { data } = await api.post<CoordinadorCreado>(
      `/super/universities/${universityId}/coordinadores`,
      payload,
    )
    return data
  },

  deleteCoordinador: async (coordinadorId: string) => {
    await api.delete(`/super/coordinadores/${coordinadorId}`)
  },

  resetCoordinadorPassword: async (coordinadorId: string) => {
    const { data } = await api.post<ResetPasswordResponse>(
      `/super/coordinadores/${coordinadorId}/reset-password`,
    )
    return data
  },
}
