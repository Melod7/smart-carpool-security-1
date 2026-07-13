import 'package:flutter_test/flutter_test.dart';
import 'package:kubix_mobile/api/models.dart';
import 'package:kubix_mobile/auth/auth_repository.dart';
import 'package:kubix_mobile/features/auth/register_wizard_page.dart';

void main() {
  group('RegisterWizardValidation', () {
    test('rechaza campos personales vacíos', () {
      final errors = RegisterWizardValidation.validatePersonal(
        name: '',
        email: '',
        password: '',
      );
      expect(errors['name'], isNotNull);
      expect(errors['email'], isNotNull);
      expect(errors['password'], isNotNull);
    });

    test('acepta datos personales válidos', () {
      final errors = RegisterWizardValidation.validatePersonal(
        name: 'Ana Pasajera',
        email: 'ana@utn.edu.ec',
        password: 'Secreta123!',
      );
      expect(errors, isEmpty);
    });

    test('exige vehículo completo para conductor', () {
      final errors = RegisterWizardValidation.validateVehicle(
        makeModel: '',
        plate: '',
        color: '',
        seatsText: '',
      );
      expect(errors.length, greaterThanOrEqualTo(4));
    });

    test('valida universidad y rol', () {
      expect(RegisterWizardValidation.validateUniversity(null), isNotNull);
      expect(RegisterWizardValidation.validateRole('admin'), isNotNull);
      expect(RegisterWizardValidation.validateRole('passenger'), isNull);
    });
  });

  group('AuthRepository.friendlyLoginError', () {
    test('mapea códigos de estado de usuario', () {
      expect(
        AuthRepository.friendlyLoginError(
          ApiException('x', code: 'user_pending'),
        ),
        contains('pendiente'),
      );
      expect(
        AuthRepository.friendlyLoginError(
          ApiException('x', code: 'user_blocked'),
        ),
        contains('bloqueada'),
      );
      expect(
        AuthRepository.friendlyLoginError(
          ApiException('x', code: 'admin_role'),
        ),
        AuthRepository.adminConsoleMessage,
      );
    });

    test('mensaje de sesión expirada', () {
      expect(
        AuthRepository.friendlySessionExpiredMessage(),
        contains('sesión expiró'),
      );
    });
  });
}
