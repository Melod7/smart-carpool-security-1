import 'package:google_maps_flutter/google_maps_flutter.dart';

/// Decodifica una polyline encoded de Google en puntos [LatLng].
List<LatLng> decodePolyline(String encoded) {
  if (encoded.isEmpty) return const [];

  final points = <LatLng>[];
  var index = 0;
  var lat = 0;
  var lng = 0;

  while (index < encoded.length) {
    var result = 0;
    var shift = 0;
    int byte;

    do {
      byte = encoded.codeUnitAt(index++) - 63;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);

    final deltaLat = (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
    lat += deltaLat;

    result = 0;
    shift = 0;

    do {
      byte = encoded.codeUnitAt(index++) - 63;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);

    final deltaLng = (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
    lng += deltaLng;

    points.add(LatLng(lat / 1e5, lng / 1e5));
  }

  return points;
}
