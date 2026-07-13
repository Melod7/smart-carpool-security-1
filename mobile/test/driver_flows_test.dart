import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/features/driver/drv_help_page.dart';
import 'package:kubix_mobile/features/driver/widgets/publish_modal.dart';
import 'package:kubix_mobile/features/passenger/trip_labels.dart';

void main() {
  group('TripLabels (conductor)', () {
    test('elige ruta activa priorizando in_progress', () {
      final now = DateTime.now();
      final trips = [
        MyTrip(
          id: '1',
          status: 'scheduled',
          role: 'driver',
          departureAt: now.add(const Duration(hours: 1)),
          originText: 'A',
          destinationCampusName: 'Campus',
          distanceKm: 5,
          co2SavedKg: 0,
        ),
        MyTrip(
          id: '2',
          status: 'in_progress',
          role: 'driver',
          departureAt: now.subtract(const Duration(minutes: 10)),
          originText: 'B',
          destinationCampusName: 'Campus',
          distanceKm: 8,
          co2SavedKg: 0,
        ),
        MyTrip(
          id: '3',
          status: 'completed',
          role: 'driver',
          departureAt: now.subtract(const Duration(days: 1)),
          originText: 'C',
          destinationCampusName: 'Campus',
          distanceKm: 10,
          co2SavedKg: 1.2,
        ),
      ];

      expect(TripLabels.activeDriverTrip(trips)?.id, '2');
      expect(TripLabels.hasDriverActiveTrip(trips), isTrue);
      expect(TripLabels.upcomingTrips(trips).map((t) => t.id), ['2', '1']);
    });

    test('suma ECT de transacciones del día', () {
      final today = DateTime(2026, 7, 13, 10);
      final txs = [
        EcoTransaction(
          id: 'a',
          type: 'trip_completed_driver',
          amount: 8,
          sourceId: 't1',
          createdAt: DateTime(2026, 7, 13, 9),
        ),
        EcoTransaction(
          id: 'b',
          type: 'rating_submitted',
          amount: 2,
          sourceId: 'r1',
          createdAt: DateTime(2026, 7, 13, 18),
        ),
        EcoTransaction(
          id: 'c',
          type: 'trip_completed_driver',
          amount: 8,
          sourceId: 't0',
          createdAt: DateTime(2026, 7, 12, 22),
        ),
      ];

      expect(TripLabels.ectTodayAmount(txs, day: today), 10);
    });
  });

  group('PublishValidation', () {
    test('exige origen, campus, salida futura y asientos', () {
      expect(
        PublishValidation.validate(
          originText: '',
          campusId: 'c1',
          departureAt: DateTime.now().add(const Duration(hours: 1)),
          seats: 2,
          maxSeats: 4,
        ),
        isNotNull,
      );
      expect(
        PublishValidation.validate(
          originText: 'Av. América',
          campusId: null,
          departureAt: DateTime.now().add(const Duration(hours: 1)),
          seats: 2,
          maxSeats: 4,
        ),
        isNotNull,
      );
      expect(
        PublishValidation.validate(
          originText: 'Av. América',
          campusId: 'c1',
          departureAt: DateTime.now().subtract(const Duration(minutes: 5)),
          seats: 2,
          maxSeats: 4,
        ),
        isNotNull,
      );
      expect(
        PublishValidation.validate(
          originText: 'Av. América',
          campusId: 'c1',
          departureAt: DateTime.now().add(const Duration(hours: 1)),
          seats: 5,
          maxSeats: 4,
        ),
        contains('4'),
      );
      expect(
        PublishValidation.validate(
          originText: 'Av. América',
          campusId: 'c1',
          departureAt: DateTime.now().add(const Duration(hours: 1)),
          seats: 2,
          maxSeats: 4,
        ),
        isNull,
      );
    });
  });

  group('DrvHelpPage', () {
    testWidgets('muestra FAQ de conductor y correo de soporte', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: DrvHelpPage(supportEmail: 'drv@test.edu')),
        ),
      );

      expect(find.text('Preguntas frecuentes'), findsOneWidget);
      expect(find.text('¿Cómo publico una ruta?'), findsOneWidget);
      expect(find.text('drv@test.edu'), findsOneWidget);
      expect(find.textContaining('No hay chat'), findsOneWidget);
    });
  });
}
