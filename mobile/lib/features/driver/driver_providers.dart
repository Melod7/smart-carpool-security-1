import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/driver_api.dart';
import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../passenger/trip_labels.dart';

const driverPollInterval = Duration(seconds: 15);

void _schedulePoll(Ref ref, Duration interval) {
  final timer = Timer.periodic(interval, (_) {
    ref.invalidateSelf();
  });
  ref.onDispose(timer.cancel);
}

final driverMyTripsProvider =
    FutureProvider.autoDispose.family<MyTripsResponse, String>((ref, period) async {
  final data = await ref.watch(driverApiProvider).getMyTrips(period: period);
  if (TripLabels.hasDriverActiveTrip(data.trips)) {
    _schedulePoll(ref, driverPollInterval);
  } else {
    _schedulePoll(ref, const Duration(seconds: 30));
  }
  return data;
});

final driverEcoProvider = FutureProvider.autoDispose<EcoSummary>((ref) async {
  return ref.watch(driverApiProvider).getEco();
});

final driverProfileProvider =
    FutureProvider.autoDispose<UserProfile>((ref) async {
  return ref.watch(driverApiProvider).getProfile();
});

final driverVehicleProvider = FutureProvider.autoDispose<Vehicle>((ref) async {
  return ref.watch(driverApiProvider).getVehicle();
});

/// Solicitudes del viaje activo — poll ~15s (PLAN §3 / KBX-24).
final tripRequestsProvider =
    FutureProvider.autoDispose.family<List<RideRequest>, String>((ref, tripId) async {
  _schedulePoll(ref, driverPollInterval);
  return ref.watch(driverApiProvider).getTripRequests(tripId);
});

final campusesForDriverProvider =
    FutureProvider.autoDispose<List<CampusPublic>>((ref) async {
  final user = ref.watch(authProvider).user;
  final universities = await ref.watch(publicApiProvider).getUniversities();
  if (user?.universityId == null) return const [];
  final uni = universities.where((u) => u.id == user!.universityId);
  if (uni.isEmpty) return const [];
  return uni.first.campuses;
});

void invalidateDriverTrips(WidgetRef ref) {
  ref.invalidate(driverMyTripsProvider);
  ref.invalidate(tripRequestsProvider);
  ref.invalidate(driverEcoProvider);
}

DriverApi driverApiOf(WidgetRef ref) => ref.read(driverApiProvider);
