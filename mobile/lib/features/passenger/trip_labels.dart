import '../../api/models.dart';

/// Etiquetas y helpers de UI para viajes del pasajero (testeable sin Flutter).
abstract final class TripLabels {
  static String tripStatus(String status) {
    return switch (status) {
      'scheduled' => 'Programado',
      'in_progress' => 'En curso',
      'completed' => 'Completado',
      'cancelled' => 'Cancelado',
      _ => status,
    };
  }

  static String requestStatus(String? status) {
    return switch (status) {
      'pending' => 'Pendiente',
      'accepted' => 'Confirmado',
      'rejected' => 'Rechazado',
      'cancelled_by_passenger' => 'Cancelado por ti',
      'cancelled_by_driver' => 'Cancelado por conductor',
      null => '',
      _ => status,
    };
  }

  static String ecoType(String type) {
    return switch (type) {
      'trip_completed_passenger' => 'Viaje completado',
      'trip_completed_driver' => 'Viaje como conductor',
      'rating_submitted' => 'Calificación enviada',
      'weekly_streak' => 'Racha semanal',
      'late_cancel_penalty' => 'Penalización cancelación',
      _ => type,
    };
  }

  static String formatDateTime(DateTime dt) {
    final local = dt.toLocal();
    final d =
        '${local.day.toString().padLeft(2, '0')}/${local.month.toString().padLeft(2, '0')}/${local.year}';
    final h =
        '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
    return '$d · $h';
  }

  static String formatKm(double km) => '${km.toStringAsFixed(1)} km';

  static String formatCo2(double kg) => '${kg.toStringAsFixed(1)} kg CO₂';

  /// Próximo viaje confirmado: primer upcoming con request accepted.
  static MyTrip? nextAcceptedTrip(List<MyTrip> trips) {
    final upcoming = trips.where((t) {
      return t.isUpcoming && t.requestStatus == 'accepted';
    }).toList()
      ..sort((a, b) => a.departureAt.compareTo(b.departureAt));
    return upcoming.isEmpty ? null : upcoming.first;
  }

  static List<MyTrip> upcomingTrips(List<MyTrip> trips) {
    final list = trips.where((t) => t.isUpcoming).toList()
      ..sort((a, b) => a.departureAt.compareTo(b.departureAt));
    return list;
  }

  static List<MyTrip> historyTrips(List<MyTrip> trips) {
    final list = trips.where((t) => t.isHistory).toList()
      ..sort((a, b) => b.departureAt.compareTo(a.departureAt));
    return list;
  }

  static bool hasActiveUpcoming(List<MyTrip> trips) {
    return trips.any(
      (t) =>
          t.isUpcoming &&
          (t.requestStatus == 'pending' || t.requestStatus == 'accepted'),
    );
  }
}
