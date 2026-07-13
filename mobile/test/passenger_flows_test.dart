import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/features/passenger/pax_help_page.dart';
import 'package:kubix_mobile/features/passenger/trip_labels.dart';
import 'package:kubix_mobile/features/passenger/widgets/eco_widget.dart';

void main() {
  group('TripLabels', () {
    test('traduce estados de viaje y solicitud', () {
      expect(TripLabels.tripStatus('scheduled'), 'Programado');
      expect(TripLabels.tripStatus('in_progress'), 'En curso');
      expect(TripLabels.tripStatus('completed'), 'Completado');
      expect(TripLabels.requestStatus('pending'), 'Pendiente');
      expect(TripLabels.requestStatus('accepted'), 'Confirmado');
      expect(TripLabels.requestStatus(null), isEmpty);
    });

    test('elige próximo viaje accepted y separa historial', () {
      final now = DateTime.now();
      final trips = [
        MyTrip(
          id: '1',
          status: 'scheduled',
          role: 'passenger',
          requestStatus: 'pending',
          departureAt: now.add(const Duration(hours: 1)),
          originText: 'A',
          destinationCampusName: 'Campus',
          distanceKm: 5,
          co2SavedKg: 0,
        ),
        MyTrip(
          id: '2',
          status: 'scheduled',
          role: 'passenger',
          requestStatus: 'accepted',
          departureAt: now.add(const Duration(hours: 2)),
          originText: 'B',
          destinationCampusName: 'Campus',
          distanceKm: 8,
          co2SavedKg: 0,
        ),
        MyTrip(
          id: '3',
          status: 'completed',
          role: 'passenger',
          requestStatus: 'accepted',
          departureAt: now.subtract(const Duration(days: 1)),
          originText: 'C',
          destinationCampusName: 'Campus',
          distanceKm: 10,
          co2SavedKg: 1.2,
        ),
      ];

      expect(TripLabels.nextAcceptedTrip(trips)?.id, '2');
      expect(TripLabels.upcomingTrips(trips).map((t) => t.id), ['1', '2']);
      expect(TripLabels.historyTrips(trips).map((t) => t.id), ['3']);
      expect(TripLabels.hasActiveUpcoming(trips), isTrue);
    });

    test('etiquetas EcoTokens', () {
      expect(
        TripLabels.ecoType('trip_completed_passenger'),
        'Viaje completado',
      );
      expect(TripLabels.ecoType('rating_submitted'), 'Calificación enviada');
    });
  });

  group('EcoWidget', () {
    testWidgets('oculta contenido si gamificación está off', (tester) async {
      const eco = EcoSummary(
        balance: 40,
        lifetime: 40,
        level: 'Bronce',
        gamificationEnabled: false,
        transactions: [],
        totalCount: 0,
      );

      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: EcoWidget(eco: eco))),
      );

      expect(find.text('EcoTokens'), findsNothing);
      expect(find.textContaining('ECT'), findsNothing);
    });

    testWidgets('muestra balance y nivel si gamificación on', (tester) async {
      const eco = EcoSummary(
        balance: 120,
        lifetime: 150,
        level: 'Plata',
        progress: 0.4,
        gamificationEnabled: true,
        transactions: [],
        totalCount: 0,
      );

      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: EcoWidget(eco: eco))),
      );

      expect(find.text('EcoTokens'), findsOneWidget);
      expect(find.text('120 ECT'), findsOneWidget);
      expect(find.text('Plata'), findsOneWidget);
      expect(find.textContaining('40%'), findsOneWidget);
    });
  });

  group('PaxHelpPage', () {
    testWidgets('muestra FAQ y correo de soporte', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: PaxHelpPage(supportEmail: 'ayuda@test.edu')),
        ),
      );

      expect(find.text('Preguntas frecuentes'), findsOneWidget);
      expect(find.text('¿Cómo solicito un viaje?'), findsOneWidget);
      expect(find.text('ayuda@test.edu'), findsOneWidget);
      expect(find.textContaining('No hay chat'), findsOneWidget);
    });
  });
}
