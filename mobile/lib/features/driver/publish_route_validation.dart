import '../../api/models.dart';

/// Validación pura del flujo publicar ruta en mapa (KBX-33).
class PublishRouteValidation {
  static const minWaypoints = 2;
  static const maxWaypoints = 8;

  static String? validateWaypoints(List<TripWaypoint> waypoints) {
    if (waypoints.length < minWaypoints) {
      return 'Añade al menos $minWaypoints puntos en el mapa (inicio y ruta).';
    }
    if (waypoints.length > maxWaypoints) {
      return 'Máximo $maxWaypoints waypoints.';
    }
    for (var i = 0; i < waypoints.length; i++) {
      final w = waypoints[i];
      if (w.lat.abs() > 90 || w.lng.abs() > 180) {
        return 'Coordenadas inválidas en el punto ${i + 1}.';
      }
    }
    return null;
  }

  static String? validateMeta({
    required String? campusId,
    required DateTime? departureAt,
    required int? seats,
    required int? maxSeats,
  }) {
    if (campusId == null || campusId.isEmpty) {
      return 'Selecciona el campus de destino.';
    }
    if (departureAt == null) {
      return 'Elige fecha y hora de salida.';
    }
    if (departureAt.isBefore(DateTime.now())) {
      return 'La salida debe ser en el futuro.';
    }
    if (seats == null || seats < 1) {
      return 'Indica al menos 1 asiento.';
    }
    if (maxSeats != null && seats > maxSeats) {
      return 'No puedes ofrecer más de $maxSeats asientos.';
    }
    return null;
  }

  /// Origen textual: etiqueta del primer waypoint o "Inicio".
  static String originTextFromWaypoints(List<TripWaypoint> waypoints) {
    if (waypoints.isEmpty) return 'Inicio';
    final label = waypoints.first.label?.trim();
    if (label != null && label.isNotEmpty) return label;
    return 'Inicio';
  }

  static bool canAddWaypoint(int currentCount) =>
      currentCount < maxWaypoints;
}
