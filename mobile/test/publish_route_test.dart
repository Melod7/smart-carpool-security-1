import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/features/driver/publish_route_validation.dart';
import 'package:kubix_mobile/features/map/map_route.dart';
import 'package:kubix_mobile/features/passenger/suggested_wait_labels.dart';

void main() {
  group('PublishRouteValidation', () {
    test('exige entre 2 y 8 waypoints', () {
      expect(
        PublishRouteValidation.validateWaypoints(const []),
        contains('2'),
      );
      expect(
        PublishRouteValidation.validateWaypoints(const [
          TripWaypoint(lat: -0.1, lng: -78.4),
        ]),
        contains('2'),
      );
      expect(
        PublishRouteValidation.validateWaypoints([
          for (var i = 0; i < 9; i++)
            TripWaypoint(lat: -0.1 + i * 0.001, lng: -78.4),
        ]),
        contains('8'),
      );
      expect(
        PublishRouteValidation.validateWaypoints(const [
          TripWaypoint(lat: -0.18, lng: -78.48, label: 'Inicio'),
          TripWaypoint(lat: -0.19, lng: -78.47, label: 'Punto 2'),
        ]),
        isNull,
      );
    });

    test('rechaza coordenadas inválidas', () {
      expect(
        PublishRouteValidation.validateWaypoints(const [
          TripWaypoint(lat: 91, lng: 0),
          TripWaypoint(lat: 0, lng: 0),
        ]),
        contains('inválidas'),
      );
    });

    test('originTextFromWaypoints usa etiqueta o Inicio', () {
      expect(
        PublishRouteValidation.originTextFromWaypoints(const [
          TripWaypoint(lat: 0, lng: 0, label: 'Av. América'),
        ]),
        'Av. América',
      );
      expect(
        PublishRouteValidation.originTextFromWaypoints(const [
          TripWaypoint(lat: 0, lng: 0),
        ]),
        'Inicio',
      );
    });

    test('validateMeta exige campus, salida futura y asientos', () {
      expect(
        PublishRouteValidation.validateMeta(
          campusId: null,
          departureAt: DateTime.now().add(const Duration(hours: 1)),
          seats: 2,
          maxSeats: 4,
        ),
        isNotNull,
      );
      expect(
        PublishRouteValidation.validateMeta(
          campusId: 'c1',
          departureAt: DateTime.now().add(const Duration(hours: 1)),
          seats: 2,
          maxSeats: 4,
        ),
        isNull,
      );
    });
  });

  group('SuggestedWaitLabels', () {
    test('formatea distancia y resumen', () {
      expect(SuggestedWaitLabels.formatDistance(120), '120 m');
      expect(SuggestedWaitLabels.formatDistance(1500), '1.5 km');

      const near = SuggestedWait(
        lat: -0.18,
        lng: -78.48,
        distanceM: 85,
        segmentIndex: 0,
      );
      expect(SuggestedWaitLabels.summary(near), contains('Espera aquí'));
      expect(SuggestedWaitLabels.summary(near), contains('85 m'));
      expect(SuggestedWaitLabels.tooFarWarning(near), isNull);

      const far = SuggestedWait(
        lat: -0.18,
        lng: -78.48,
        distanceM: 1200,
        segmentIndex: 1,
        tooFar: true,
      );
      expect(SuggestedWaitLabels.summary(far), contains('lejos'));
      expect(SuggestedWaitLabels.tooFarWarning(far), contains('lejos'));
    });

    test('pickupTextDefault', () {
      expect(SuggestedWaitLabels.pickupTextDefault(null), 'Punto de espera');
      expect(
        SuggestedWaitLabels.pickupTextDefault(
          const SuggestedWait(
            lat: 0,
            lng: 0,
            distanceM: 10,
            segmentIndex: 0,
          ),
        ),
        'Espera aquí',
      );
    });
  });

  group('buildMapRoute con waypoints', () {
    test('fallback discontinuo por waypoints cuando no hay polyline', () {
      final route = buildMapRoute(
        waypoints: const [
          TripWaypoint(lat: -0.18, lng: -78.48),
          TripWaypoint(lat: -0.19, lng: -78.47),
          TripWaypoint(lat: -0.20, lng: -78.46),
        ],
      );
      expect(route.dashed, isTrue);
      expect(route.points, hasLength(3));
      expect(route.isRenderable, isTrue);
    });
  });

  group('AvailableTrip.suggestedWait JSON', () {
    test('parsea waypoints y suggestedWait', () {
      final trip = AvailableTrip.fromJson({
        'id': 't1',
        'status': 'scheduled',
        'originText': 'Inicio',
        'originLat': -0.18,
        'originLng': -78.48,
        'destinationCampusId': 'c1',
        'departureAt': '2026-07-13T20:00:00Z',
        'seatsAvailable': 3,
        'distanceKm': 5.2,
        'co2SavedKg': 1.1,
        'driverId': 'd1',
        'waypoints': [
          {'lat': -0.18, 'lng': -78.48, 'label': 'Inicio', 'seq': 0},
          {'lat': -0.19, 'lng': -78.47, 'seq': 1},
        ],
        'suggestedWait': {
          'lat': -0.185,
          'lng': -78.475,
          'distanceM': 90,
          'segmentIndex': 0,
          'tooFar': false,
        },
      });

      expect(trip.waypoints, hasLength(2));
      expect(trip.waypoints.first.label, 'Inicio');
      expect(trip.suggestedWait?.distanceM, 90);
      expect(trip.suggestedWait?.tooFar, isFalse);
    });
  });
}
