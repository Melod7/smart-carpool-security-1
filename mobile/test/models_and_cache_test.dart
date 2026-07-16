import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/auth/auth_state.dart';
import 'package:kubix_mobile/features/map/trip_geometry_cache.dart';
import 'package:kubix_mobile/theme/kubix_theme.dart';

void main() {
  group('AuthUser / AuthState', () {
    test('fromJson y roles', () {
      final user = AuthUser.fromJson({
        'id': 'u1',
        'email': 'a@b.com',
        'name': 'Ana',
        'role': 'driver',
        'universityId': 'uni',
        'campusId': 'cam',
        'mustChangePassword': false,
        'status': 'active',
      });
      expect(user.isDriver, isTrue);
      expect(user.isPassenger, isFalse);
      expect(user.isAdminRole, isFalse);
      expect(user.copyWith(name: 'Bob').name, 'Bob');
      expect(user.toJson()['email'], 'a@b.com');
    });

    test('AuthState.copyWith limpia errores', () {
      const s = AuthState(
        status: AuthStatus.unauthenticated,
        errorMessage: 'x',
        sessionExpiredMessage: 'y',
      );
      final cleared = s.copyWith(clearError: true, clearSessionExpired: true);
      expect(cleared.errorMessage, isNull);
      expect(cleared.sessionExpiredMessage, isNull);
      expect(cleared.isAuthenticated, isFalse);
    });
  });

  group('Trip / Vehicle / Eco models', () {
    test('AvailableTrip.fromJson con suggestedWait', () {
      final trip = AvailableTrip.fromJson({
        'id': 't1',
        'driverId': 'd1',
        'originText': 'Norte',
        'originLat': -0.1,
        'originLng': -78.4,
        'destinationCampusId': 'c1',
        'departureAt': '2026-07-16T12:00:00Z',
        'seatsAvailable': 3,
        'status': 'scheduled',
        'polyline': null,
        'distanceKm': 12.5,
        'co2SavedKg': 2.1,
        'waypoints': [
          {'lat': -0.1, 'lng': -78.4, 'label': 'Inicio'},
          {'lat': -0.15, 'lng': -78.45, 'label': 'Paso'},
        ],
        'suggestedWait': {
          'lat': -0.12,
          'lng': -78.42,
          'distanceM': 40,
          'segmentIndex': 0,
          'tooFar': false,
        },
      });
      expect(trip.seatsAvailable, 3);
      expect(trip.waypoints, hasLength(2));
      expect(trip.suggestedWait?.tooFar, isFalse);
      expect(trip.suggestedWait?.distanceM, 40);
    });

    test('Vehicle y VehicleChangeRequest', () {
      final v = Vehicle.fromJson({
        'id': 'v1',
        'makeModel': 'Kia Rio',
        'plate': 'ABC-123',
        'color': 'Gris',
        'seatsTotal': 4,
      });
      expect(v.plate, 'ABC-123');
      final change = VehicleChangeRequest.fromJson({
        'id': 'r1',
        'status': 'pending',
        'kind': 'vehicle_change',
        'message': 'ok',
      });
      expect(change.kind, 'vehicle_change');
      expect(change.message, 'ok');
    });

    test('EcoSummary.fromJson', () {
      final eco = EcoSummary.fromJson({
        'balance': 20,
        'lifetime': 120,
        'level': 'Plata',
        'progress': 0.2,
        'gamificationEnabled': true,
        'totalCount': 1,
        'transactions': [
          {
            'id': 'tx1',
            'type': 'trip_completed_driver',
            'amount': 8,
            'sourceId': 't1',
            'createdAt': '2026-07-16T10:00:00Z',
          },
        ],
      });
      expect(eco.level, 'Plata');
      expect(eco.transactions, hasLength(1));
      expect(eco.gamificationEnabled, isTrue);
    });

    test('UserProfile.fromJson', () {
      final p = UserProfile.fromJson({
        'id': 'u1',
        'name': 'Ana',
        'email': 'a@b.com',
        'role': 'passenger',
        'career': 'Sistemas',
        'campusId': 'c1',
        'universityId': 'uni',
        'status': 'active',
      });
      expect(p.career, 'Sistemas');
      expect(p.role, 'passenger');
    });

    test('AuthResponse.fromJson', () {
      final r = AuthResponse.fromJson({
        'accessToken': 'a',
        'refreshToken': 'r',
        'expiresIn': 3600,
        'mustChangePassword': false,
        'user': {
          'id': 'u1',
          'email': 'a@b.com',
          'name': 'Ana',
          'role': 'passenger',
        },
      });
      expect(r.accessToken, 'a');
      expect(r.user.isPassenger, isTrue);
    });
  });

  group('TripGeometryCache', () {
    test('merge conserva polyline previa', () {
      final cache = TripGeometryCache();
      cache.put(
        const TripMapSeed(
          tripId: 't1',
          status: 'scheduled',
          polyline: 'abc',
          originLat: 1,
          originLng: 2,
        ),
      );
      cache.put(
        const TripMapSeed(
          tripId: 't1',
          status: 'in_progress',
          pickupLat: 3,
          pickupLng: 4,
        ),
      );
      final seed = cache.get('t1')!;
      expect(seed.polyline, 'abc');
      expect(seed.status, 'in_progress');
      expect(seed.pickupLat, 3);
    });

    test('putPickup y putAvailableList', () {
      final cache = TripGeometryCache();
      cache.putPickup(tripId: 't2', pickupLat: 1.5, pickupLng: 2.5);
      expect(cache.get('t2')?.pickupLat, 1.5);

      final trip = AvailableTrip.fromJson({
        'id': 't3',
        'driverId': 'd1',
        'originText': 'Norte',
        'originLat': -0.1,
        'originLng': -78.4,
        'destinationCampusId': 'c1',
        'departureAt': '2026-07-16T12:00:00Z',
        'seatsAvailable': 2,
        'status': 'scheduled',
        'distanceKm': 5,
        'co2SavedKg': 1,
        'waypoints': [],
      });
      cache.putAvailableList([trip]);
      expect(cache.get('t3')?.originLat, -0.1);
    });
  });

  group('Theme', () {
    test('buildKubixTheme usa primary de paleta 2.0', () {
      final theme = buildKubixTheme();
      expect(theme.colorScheme.primary, KubixColors.primary);
      expect(theme.scaffoldBackgroundColor, KubixColors.background);
    });
  });
}
