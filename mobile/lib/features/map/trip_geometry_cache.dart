import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';

/// Caché en memoria de geometría de viajes (polyline/origen/pickup).
///
/// Se alimenta al publicar, al listar disponibles, al solicitar asiento y al
/// iniciar/completar — cubre el mapa `scheduled` sin endpoint GET trip.
class TripGeometryCache extends StateNotifier<Map<String, TripMapSeed>> {
  TripGeometryCache() : super(const {});

  void put(TripMapSeed seed) {
    final previous = state[seed.tripId];
    if (previous == null) {
      state = {...state, seed.tripId: seed};
      return;
    }
    state = {
      ...state,
      seed.tripId: TripMapSeed(
        tripId: seed.tripId,
        status: seed.status,
        polyline: seed.polyline ?? previous.polyline,
        originLat: seed.originLat ?? previous.originLat,
        originLng: seed.originLng ?? previous.originLng,
        pickupLat: seed.pickupLat ?? previous.pickupLat,
        pickupLng: seed.pickupLng ?? previous.pickupLng,
        waypoints: seed.waypoints.isNotEmpty
            ? seed.waypoints
            : previous.waypoints,
      ),
    };
  }

  void putAvailable(AvailableTrip trip) {
    put(TripMapSeed.fromAvailableTrip(trip));
  }

  void putAvailableList(Iterable<AvailableTrip> trips) {
    for (final trip in trips) {
      putAvailable(trip);
    }
  }

  void putPickup({
    required String tripId,
    required double pickupLat,
    required double pickupLng,
    String status = 'scheduled',
  }) {
    put(
      TripMapSeed(
        tripId: tripId,
        status: status,
        pickupLat: pickupLat,
        pickupLng: pickupLng,
      ),
    );
  }

  TripMapSeed? get(String tripId) => state[tripId];
}

final tripGeometryCacheProvider =
    StateNotifierProvider<TripGeometryCache, Map<String, TripMapSeed>>((ref) {
  return TripGeometryCache();
});
