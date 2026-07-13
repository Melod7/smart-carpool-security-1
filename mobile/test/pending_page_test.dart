import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/features/auth/pending_page.dart';

void main() {
  testWidgets('PendingPage muestra estado de aprobación', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(home: PendingPage()),
    );

    expect(find.text('Solicitud enviada'), findsOneWidget);
    expect(find.textContaining('pendiente de aprobación'), findsOneWidget);
    expect(find.text('Volver al inicio de sesión'), findsOneWidget);
  });
}
