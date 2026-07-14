import '../../api/models.dart';

/// Textos de UI para el punto de espera sugerido (testeable).
class SuggestedWaitLabels {
  /// Distancia legible: metros o kilómetros.
  static String formatDistance(double distanceM) {
    if (distanceM < 1000) {
      return '${distanceM.round()} m';
    }
    final km = distanceM / 1000;
    return '${km.toStringAsFixed(km < 10 ? 1 : 0)} km';
  }

  /// Línea corta para la card de viaje disponible.
  static String summary(SuggestedWait wait) {
    final dist = formatDistance(wait.distanceM);
    if (wait.tooFar) {
      return 'Espera aquí · $dist (lejos de la ruta)';
    }
    return 'Espera aquí · a $dist de ti';
  }

  /// Aviso cuando el pasajero está demasiado lejos (> umbral server).
  static String? tooFarWarning(SuggestedWait? wait) {
    if (wait == null || !wait.tooFar) return null;
    return 'Estás lejos de la ruta del conductor '
        '(${formatDistance(wait.distanceM)}). '
        'Puedes solicitar igual; el punto de espera está sobre la ruta.';
  }

  static String pickupTextDefault(SuggestedWait? wait) {
    if (wait == null) return 'Punto de espera';
    return 'Espera aquí';
  }
}
