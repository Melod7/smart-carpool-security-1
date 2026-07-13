import '../api/auth_api.dart';
import '../api/models.dart';
import 'token_storage.dart';

/// Orquesta auth API + almacenamiento seguro de tokens.
class AuthRepository {
  AuthRepository({
    required AuthApi authApi,
    required TokenStorage tokenStorage,
  })  : _authApi = authApi,
        _tokenStorage = tokenStorage;

  final AuthApi _authApi;
  final TokenStorage _tokenStorage;

  static const adminConsoleMessage =
      'Esta cuenta es de administración; usa la consola web.';

  Future<AuthUser?> restoreSession() async {
    final access = await _tokenStorage.getAccessToken();
    if (access == null || access.isEmpty) return null;

    try {
      final user = await _authApi.me();
      if (user.isAdminRole) {
        await _tokenStorage.clear();
        throw ApiException(adminConsoleMessage, code: 'admin_role');
      }
      return user;
    } catch (_) {
      await _tokenStorage.clear();
      rethrow;
    }
  }

  Future<AuthUser> login({
    required String email,
    required String password,
  }) async {
    final response = await _authApi.login(email: email, password: password);
    final user = response.user.copyWith(
      mustChangePassword:
          response.mustChangePassword || (response.user.mustChangePassword ?? false),
    );

    if (user.isAdminRole) {
      throw ApiException(adminConsoleMessage, code: 'admin_role');
    }

    await _tokenStorage.saveTokens(
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
    );
    return user;
  }

  Future<RegisterResponse> register(RegisterRequest request) {
    return _authApi.register(request);
  }

  Future<void> logout() async {
    final refresh = await _tokenStorage.getRefreshToken();
    try {
      if (refresh != null) {
        await _authApi.logout(refresh);
      }
    } catch (_) {
      // Limpiamos local aunque el servidor falle.
    } finally {
      await _tokenStorage.clear();
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) {
    return _authApi.changePassword(
      currentPassword: currentPassword,
      newPassword: newPassword,
    );
  }

  Future<void> clearLocal() => _tokenStorage.clear();

  /// Traduce códigos de error de login a mensajes en español.
  static String friendlyLoginError(Object error) {
    if (error is ApiException) {
      switch (error.code) {
        case 'user_pending':
          return 'Tu cuenta aún está pendiente de aprobación.';
        case 'user_blocked':
          return 'Tu cuenta está bloqueada. Contacta a tu coordinador.';
        case 'invalid_credentials':
          return 'Correo o contraseña incorrectos.';
        case 'admin_role':
          return adminConsoleMessage;
        default:
          return error.message;
      }
    }
    return error.toString();
  }

  static String friendlySessionExpiredMessage() =>
      'Tu sesión expiró. Inicia sesión de nuevo.';
}
