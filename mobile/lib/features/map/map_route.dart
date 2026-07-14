import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../api/models.dart';
import 'decode_polyline.dart';

/// Resultado de geometría de ruta para el mapa.
class MapRoutePath {
  const MapRoutePath({required this.points, required this.dashed});

  final List<LatLng> points;
  final bool dashed;

  bool get isRenderable => points.length >= 2;
}

/// Construye la ruta: polyline encoded, waypoints ordenados, o línea recta discontinua.
MapRoutePath buildMapRoute({
  String? polyline,
  double? originLat,
  double? originLng,
  double? pickupLat,
  double? pickupLng,
  List<TripWaypoint> waypoints = const [],
  List<TrackingParticipant> participants = const [],
}) {
  if (polyline != null && polyline.isNotEmpty) {
    final decoded = decodePolyline(polyline);
    if (decoded.length >= 2) {
      return MapRoutePath(points: decoded, dashed: false);
    }
  }

  if (waypoints.length >= 2) {
    return MapRoutePath(
      points: [
        for (final w in waypoints) LatLng(w.lat, w.lng),
      ],
      dashed: true,
    );
  }

  final points = <LatLng>[];

  if (originLat != null && originLng != null) {
    points.add(LatLng(originLat, originLng));
  }
  if (pickupLat != null && pickupLng != null) {
    final pickup = LatLng(pickupLat, pickupLng);
    if (points.isEmpty ||
        points.last.latitude != pickup.latitude ||
        points.last.longitude != pickup.longitude) {
      points.add(pickup);
    }
  }

  if (points.length < 2) {
    for (final p in participants) {
      points.add(LatLng(p.lat, p.lng));
      if (points.length >= 2) break;
    }
  }

  if (points.length >= 2) {
    return MapRoutePath(points: [points.first, points[1]], dashed: true);
  }

  return MapRoutePath(points: points, dashed: true);
}

/// ¿Se deben enviar pings? Solo en `in_progress` (battery-safe).
bool shouldSendPings(String status) => status == 'in_progress';

/// ¿Se debe hacer poll de tracking?
bool shouldPollTracking(String status) => status == 'in_progress';
