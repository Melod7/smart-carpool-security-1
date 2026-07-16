import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/api/sos_api.dart';
import 'package:kubix_mobile/auth/auth_state.dart';
import 'package:kubix_mobile/features/passenger/trip_labels.dart';
import 'package:kubix_mobile/features/sos/sos_button.dart';
import 'package:kubix_mobile/features/sos/sos_location.dart';
import 'package:kubix_mobile/features/sos/sos_overlay.dart';
import 'package:mocktail/mocktail.dart';

class _MockSosApi extends Mock implements SosApi {}

class _FakeLocationOk implements SosLocationSource {
  @override
  Future<SosCoords> getCurrentCoords() async => (lat: -0.18, lng: -78.48);
}

class _FakeLocationDenied implements SosLocationSource {
  @override
  Future<SosCoords> getCurrentCoords() async {
    throw const SosLocationException(
      'Se necesita permiso de ubicación para enviar la alerta SOS.',
    );
  }
}

void main() {
  group('SosAlert', () {
    test('parsea respuesta de POST /sos', () {
      final alert = SosAlert.fromJson({
        'id': 'a1',
        'status': 'active',
        'lat': -0.18,
        'lng': -78.48,
        'tripId': 't1',
        'firedAt': '2026-07-13T12:00:00Z',
        'universityId': 'u1',
        'userId': 'p1',
      });

      expect(alert.id, 'a1');
      expect(alert.isActive, isTrue);
      expect(alert.tripId, 't1');
      expect(alert.lat, -0.18);
    });
  });

  group('TripLabels.activeTripForSos', () {
    test('prioriza in_progress sobre scheduled', () {
      final now = DateTime.now();
      final trips = [
        MyTrip(
          id: 'sched',
          status: 'scheduled',
          role: 'passenger',
          requestStatus: 'accepted',
          departureAt: now.add(const Duration(hours: 1)),
          originText: 'A',
          destinationCampusName: 'Campus',
          distanceKm: 5,
          co2SavedKg: 0,
        ),
        MyTrip(
          id: 'active',
          status: 'in_progress',
          role: 'passenger',
          requestStatus: 'accepted',
          departureAt: now.subtract(const Duration(minutes: 5)),
          originText: 'B',
          destinationCampusName: 'Campus',
          distanceKm: 8,
          co2SavedKg: 0,
        ),
      ];

      expect(
        TripLabels.activeTripForSos(trips, asDriver: false)?.id,
        'active',
      );
    });
  });

  group('SosButton', () {
    testWidgets('muestra etiqueta de emergencia', (tester) async {
      var tapped = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SosButton(onPressed: () => tapped = true),
          ),
        ),
      );

      expect(find.text('SOS · EMERGENCIA'), findsOneWidget);
      await tester.tap(find.text('SOS · EMERGENCIA'));
      expect(tapped, isTrue);
    });
  });

  group('SosOverlay', () {
    testWidgets('confirma y muestra copy de seguridad del campus',
        (tester) async {
      final api = _MockSosApi();
      when(
        () => api.createSos(
          lat: any(named: 'lat'),
          lng: any(named: 'lng'),
          tripId: any(named: 'tripId'),
        ),
      ).thenAnswer(
        (_) async => SosAlert(
          id: 'sos-1',
          status: 'active',
          lat: -0.18,
          lng: -78.48,
          firedAt: DateTime.utc(2026, 7, 13, 12),
          universityId: 'u1',
          userId: 'p1',
          tripId: 't1',
        ),
      );
      when(() => api.closeSos(any())).thenAnswer(
        (_) async => SosAlert(
          id: 'sos-1',
          status: 'resolved',
          lat: -0.18,
          lng: -78.48,
          firedAt: DateTime.utc(2026, 7, 13, 12),
          universityId: 'u1',
          userId: 'p1',
          resolvedAt: DateTime.utc(2026, 7, 13, 12, 5),
          resolvedBy: 'p1',
        ),
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sosApiProvider.overrideWithValue(api),
            sosLocationSourceProvider.overrideWithValue(_FakeLocationOk()),
          ],
          child: const MaterialApp(home: SosOverlay(tripId: 't1')),
        ),
      );

      expect(find.text('¿Activar alerta SOS?'), findsOneWidget);
      expect(find.textContaining('seguridad del campus'), findsOneWidget);
      expect(find.textContaining('contactos de emergencia'), findsNothing);

      await tester.tap(find.text('Enviar alerta'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      expect(find.text('ALERTA ENVIADA'), findsOneWidget);
      expect(find.text(sosCampusNotifiedCopy), findsOneWidget);
      expect(find.textContaining('contactos de emergencia'), findsNothing);
      expect(find.text('Estoy a salvo · Cerrar alerta'), findsOneWidget);

      verify(
        () => api.createSos(lat: -0.18, lng: -78.48, tripId: 't1'),
      ).called(1);

      await tester.tap(find.text('Estoy a salvo · Cerrar alerta'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      verify(() => api.closeSos('sos-1')).called(1);
    });

    testWidgets('bloquea envío si GPS denegado', (tester) async {
      final api = _MockSosApi();

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sosApiProvider.overrideWithValue(api),
            sosLocationSourceProvider.overrideWithValue(_FakeLocationDenied()),
          ],
          child: const MaterialApp(home: SosOverlay()),
        ),
      );

      await tester.tap(find.text('Enviar alerta'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      expect(
        find.textContaining('permiso de ubicación'),
        findsOneWidget,
      );
      expect(find.text('¿Activar alerta SOS?'), findsOneWidget);
      verifyNever(
        () => api.createSos(
          lat: any(named: 'lat'),
          lng: any(named: 'lng'),
          tripId: any(named: 'tripId'),
        ),
      );
    });

    testWidgets('sale si el coordinador ya resolvió la alerta', (tester) async {
      final api = _MockSosApi();
      var closed = false;
      when(
        () => api.createSos(
          lat: any(named: 'lat'),
          lng: any(named: 'lng'),
          tripId: any(named: 'tripId'),
        ),
      ).thenAnswer(
        (_) async => SosAlert(
          id: 'sos-2',
          status: 'active',
          lat: -0.18,
          lng: -78.48,
          firedAt: DateTime.utc(2026, 7, 16, 18),
          universityId: 'u1',
          userId: 'p1',
        ),
      );
      when(() => api.closeSos('sos-2')).thenThrow(
        ApiException(
          'SOS alert is already resolved.',
          statusCode: 409,
          code: 'sos_already_resolved',
        ),
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sosApiProvider.overrideWithValue(api),
            sosLocationSourceProvider.overrideWithValue(_FakeLocationOk()),
          ],
          child: MaterialApp(
            home: SosOverlay(onClosed: () => closed = true),
          ),
        ),
      );

      await tester.tap(find.text('Enviar alerta'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));
      await tester.tap(find.text('Estoy a salvo · Cerrar alerta'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      expect(closed, isTrue);
      expect(find.textContaining('already resolved'), findsNothing);
    });
  });
}
