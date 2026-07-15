import 'package:google_maps_flutter/google_maps_flutter.dart';

/// Ajusta la cámara para enmarcar todos los [points] (mín. padding).
Future<void> fitMapToPoints(
  GoogleMapController? controller,
  Iterable<LatLng> points, {
  double padding = 56,
}) async {
  if (controller == null) return;
  final list = points.toList();
  if (list.isEmpty) return;
  if (list.length == 1) {
    await controller.animateCamera(
      CameraUpdate.newLatLngZoom(list.first, 15),
    );
    return;
  }
  var minLat = list.first.latitude;
  var maxLat = list.first.latitude;
  var minLng = list.first.longitude;
  var maxLng = list.first.longitude;
  for (final p in list.skip(1)) {
    if (p.latitude < minLat) minLat = p.latitude;
    if (p.latitude > maxLat) maxLat = p.latitude;
    if (p.longitude < minLng) minLng = p.longitude;
    if (p.longitude > maxLng) maxLng = p.longitude;
  }
  // Evita bounds degenerados.
  if ((maxLat - minLat).abs() < 0.0002) {
    minLat -= 0.002;
    maxLat += 0.002;
  }
  if ((maxLng - minLng).abs() < 0.0002) {
    minLng -= 0.002;
    maxLng += 0.002;
  }
  await controller.animateCamera(
    CameraUpdate.newLatLngBounds(
      LatLngBounds(
        southwest: LatLng(minLat, minLng),
        northeast: LatLng(maxLat, maxLng),
      ),
      padding,
    ),
  );
}
