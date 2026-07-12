import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/main.dart';

void main() {
  testWidgets('muestra el esqueleto Kubix', (WidgetTester tester) async {
    await tester.pumpWidget(const KubixApp());

    expect(find.text('Kubix UTN'), findsOneWidget);
    expect(find.textContaining('Carpooling seguro'), findsOneWidget);
  });
}
