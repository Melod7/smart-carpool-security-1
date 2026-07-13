import 'package:flutter_test/flutter_test.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/features/map/decode_polyline.dart';
import 'package:kubix_mobile/features/map/map_route.dart';

void main() {
  group('decodePolyline', () {
    test('decodifica una polyline conocida de Google', () {
      final points = decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@');
      expect(points, hasLength(3));
      expect(points[0].latitude, closeTo(38.5, 0.0001));
      expect(points[0].longitude, closeTo(-120.2, 0.0001));
      expect(points[1].latitude, closeTo(40.7, 0.0001));
      expect(points[1].longitude, closeTo(-120.95, 0.0001));
      expect(points[2].latitude, closeTo(43.252, 0.0001));
      expect(points[2].longitude, closeTo(-126.453, 0.0001));
    });

    test('devuelve vacío para cadena vacía', () {
      expect(decodePolyline(''), isEmpty);
    });
  });

  group('buildMapRoute', () {
    test('usa polyline encoded cuando hay ≥2 puntos', () {
      final route = buildMapRoute(
        polyline: '_p~iF~ps|U_ulLnnqC_mqNvxq`@',
      );
      expect(route.dashed, isFalse);
      expect(route.points, hasLength(3));
      expect(route.isRenderable, isTrue);
    });

    test('fallback discontinuo origen → pickup cuando polyline es null', () {
      final route = buildMapRoute(
        originLat: -0.18,
        originLng: -78.48,
        pickupLat: -0.20,
        pickupLng: -78.50,
      );
      expect(route.dashed, isTrue);
      expect(route.points, hasLength(2));
      expect(route.points.first, const LatLng(-0.18, -78.48));
      expect(route.points.last, const LatLng(-0.20, -78.50));
    });

    test('fallback con participantes si no hay origen/pickup', () {
      final route = buildMapRoute(
        participants: const [
          TrackingParticipant(
            userId: 'd1',
            role: 'driver',
            lat: -0.1,
            lng: -78.4,
            source: 'ping',
          ),
          TrackingParticipant(
            userId: 'p1',
            role: 'passenger',
            lat: -0.2,
            lng: -78.5,
            source: 'pickup',
          ),
        ],
      );
      expect(route.dashed, isTrue);
      expect(route.isRenderable, isTrue);
      expect(route.points.first.latitude, -0.1);
      expect(route.points.last.latitude, -0.2);
    });
  });

  group('shouldSendPings / shouldPollTracking', () {
    test('solo in_progress (battery-safe)', () {
      expect(shouldSendPings('in_progress'), isTrue);
      expect(shouldPollTracking('in_progress'), isTrue);
      expect(shouldSendPings('scheduled'), isFalse);
      expect(shouldPollTracking('scheduled'), isFalse);
      expect(shouldSendPings('completed'), isFalse);
      expect(shouldSendPings('cancelled'), isFalse);
    });
  });

  group('TripTracking / LocationPing models', () {
    test('parsea GET /trips/{id}/tracking', () {
      final tracking = TripTracking.fromJson({
        'tripId': 't1',
        'status': 'in_progress',
        'polyline': null,
        'participants': [
          {
            'userId': 'd1',
            'role': 'driver',
            'name': 'Ana',
            'lat': -0.18,
            'lng': -78.48,
            'recordedAt': '2026-07-13T12:00:00Z',
            'source': 'ping',
          },
        ],
      });
      expect(tracking.tripId, 't1');
      expect(tracking.isActive, isTrue);
      expect(tracking.participants.single.isDriver, isTrue);
      expect(tracking.participants.single.name, 'Ana');
    });

    test('parsea POST /trips/{id}/pings', () {
      final ping = LocationPing.fromJson({
        'id': 'ping1',
        'tripId': 't1',
        'userId': 'u1',
        'lat': -0.19,
        'lng': -78.49,
        'recordedAt': '2026-07-13T12:00:10Z',
      });
      expect(ping.id, 'ping1');
      expect(ping.lat, -0.19);
    });
  });
}
