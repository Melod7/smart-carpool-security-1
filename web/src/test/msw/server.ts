import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'

/** Handlers MSW base (KBX-27). Los tests pueden sobrescribir con server.use(...). */
export const handlers = [
  http.get('*/health', () => HttpResponse.json({ status: 'ok' })),
  http.get('*/admin/dashboard', () =>
    HttpResponse.json({
      tripsToday: 0,
      blockedUsers: 0,
      co2SavedKg: 0,
      adoptionRate: 0,
      adoptionRateBasis: 'active_users_over_total',
      activeSos: [],
      xpByCareer: [],
      gamificationEnabled: true,
    }),
  ),
]

export const server = setupServer(...handlers)
