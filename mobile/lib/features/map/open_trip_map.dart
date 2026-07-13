import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../api/models.dart';
import 'trip_geometry_cache.dart';

/// Abre el mapa de viaje con semilla de geometría resuelta desde caché / trip.
void openTripMap(
  BuildContext context,
  WidgetRef ref, {
  required String tripId,
  required String status,
  AvailableTrip? available,
  double? pickupLat,
  double? pickupLng,
}) {
  final cache = ref.read(tripGeometryCacheProvider.notifier);
  if (available != null) {
    cache.putAvailable(available);
  }
  if (pickupLat != null && pickupLng != null) {
    cache.putPickup(
      tripId: tripId,
      pickupLat: pickupLat,
      pickupLng: pickupLng,
      status: status,
    );
  } else {
    cache.put(TripMapSeed(tripId: tripId, status: status));
  }

  final seed = cache.get(tripId) ??
      TripMapSeed(tripId: tripId, status: status);

  context.push('/trips/$tripId/map', extra: seed);
}

void openTripMapFromMyTrip(
  BuildContext context,
  WidgetRef ref,
  MyTrip trip, {
  AvailableTrip? available,
}) {
  openTripMap(
    context,
    ref,
    tripId: trip.id,
    status: trip.status,
    available: available,
  );
}
