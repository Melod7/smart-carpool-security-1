/** Etiquetas en español para acciones de auditoría (código → texto legible). */
const ACTION_LABELS: Record<string, string> = {
  'sos.fired': 'Alerta SOS activada',
  'sos.closed': 'Alerta SOS cerrada',
  'sos.resolved': 'Alerta SOS resuelta',
  'platform.seeded': 'Plataforma inicializada',
  'registration.submitted': 'Registro enviado',
  'registration.accepted': 'Registro aceptado',
  'registration.denied': 'Registro denegado',
  'profile.change_requested': 'Cambio de perfil solicitado',
  'profile.change_accepted': 'Cambio de perfil aceptado',
  'profile.change_denied': 'Cambio de perfil denegado',
  'role.change_driver_requested': 'Cambio a conductor solicitado',
  'role.change_driver_accepted': 'Cambio a conductor aceptado',
  'role.change_driver_denied': 'Cambio a conductor denegado',
  'role.changed_to_passenger': 'Cambio a modo pasajero',
  'vehicle.change_requested': 'Cambio de vehículo solicitado',
  'vehicle.change_accepted': 'Cambio de vehículo aceptado',
  'user.blocked': 'Usuario bloqueado',
  'user.unblocked': 'Usuario desbloqueado',
  'user.deleted': 'Usuario eliminado',
  'user.auto_blocked_min_rating': 'Usuario bloqueado por calificación mínima',
  'university.created': 'Universidad creada',
  'university.suspended': 'Universidad suspendida',
  'coordinador.created': 'Coordinador creado',
  'coordinador.deleted': 'Coordinador eliminado',
  'coordinador.password_reset': 'Contraseña de coordinador restablecida',
  'trip.published': 'Viaje publicado',
  'trip.late_cancel': 'Cancelación tardía de viaje',
  'ride_request.created': 'Solicitud de viaje creada',
  'ride_request.recreated': 'Solicitud de viaje recreada',
  'ride_request.accepted': 'Solicitud de viaje aceptada',
  'ride_request.rejected': 'Solicitud de viaje rechazada',
  'ride_request.cancelled': 'Solicitud de viaje cancelada',
}

export function auditActionLabel(action: string): string {
  const raw = action.trim()
  if (!raw) return '—'

  const sep = raw.indexOf(':')
  const key = sep >= 0 ? raw.slice(0, sep) : raw
  const id = sep >= 0 ? raw.slice(sep + 1) : ''
  const label = ACTION_LABELS[key] ?? key

  if (!id) return label
  const shortId = id.length > 8 ? `${id.slice(0, 8)}…` : id
  return `${label} (${shortId})`
}
