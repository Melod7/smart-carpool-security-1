import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/auth_api.dart';
import '../api/driver_api.dart';
import '../api/models.dart';
import '../api/passenger_api.dart';
import '../api/public_api.dart';
import '../api/sos_api.dart';
import '../api/tracking_api.dart';
import 'auth_repository.dart';
import 'token_storage.dart';

/// URL base inyectada por `--dart-define=API_URL=...`.
const apiUrl = String.fromEnvironment(
  'API_URL',
  defaultValue: 'http://127.0.0.1:8080',
);

enum AuthStatus { unknown, authenticated, unauthenticated }

class AuthState {
  const AuthState({
    required this.status,
    this.user,
    this.errorMessage,
    this.sessionExpiredMessage,
    this.mustChangePassword = false,
  });

  final AuthStatus status;
  final AuthUser? user;
  final String? errorMessage;
  final String? sessionExpiredMessage;
  final bool mustChangePassword;

  bool get isAuthenticated =>
      status == AuthStatus.authenticated && user != null;

  AuthState copyWith({
    AuthStatus? status,
    AuthUser? user,
    String? errorMessage,
    String? sessionExpiredMessage,
    bool? mustChangePassword,
    bool clearUser = false,
    bool clearError = false,
    bool clearSessionExpired = false,
  }) {
    return AuthState(
      status: status ?? this.status,
      user: clearUser ? null : (user ?? this.user),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      sessionExpiredMessage: clearSessionExpired
          ? null
          : (sessionExpiredMessage ?? this.sessionExpiredMessage),
      mustChangePassword: mustChangePassword ?? this.mustChangePassword,
    );
  }
}

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

final apiClientProvider = Provider<ApiClient>((ref) {
  final storage = ref.watch(tokenStorageProvider);
  final client = ApiClient(baseUrl: apiUrl, tokenStorage: storage);
  return client;
});

final authApiProvider = Provider<AuthApi>((ref) {
  return AuthApi(ref.watch(apiClientProvider));
});

final publicApiProvider = Provider<PublicApi>((ref) {
  return PublicApi(ref.watch(apiClientProvider));
});

final passengerApiProvider = Provider<PassengerApi>((ref) {
  return PassengerApi(ref.watch(apiClientProvider));
});

final driverApiProvider = Provider<DriverApi>((ref) {
  return DriverApi(ref.watch(apiClientProvider));
});

final sosApiProvider = Provider<SosApi>((ref) {
  return SosApi(ref.watch(apiClientProvider));
});

final trackingApiProvider = Provider<TrackingApi>((ref) {
  return TrackingApi(ref.watch(apiClientProvider));
});

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepository(
    authApi: ref.watch(authApiProvider),
    tokenStorage: ref.watch(tokenStorageProvider),
  );
});

class AuthNotifier extends StateNotifier<AuthState> {
  AuthNotifier(this._repo, this._apiClient)
      : super(const AuthState(status: AuthStatus.unknown)) {
    _apiClient.onSessionExpired = _handleSessionExpired;
    restoreSession();
  }

  final AuthRepository _repo;
  final ApiClient _apiClient;

  Future<void> restoreSession() async {
    state = state.copyWith(status: AuthStatus.unknown, clearError: true);
    try {
      final user = await _repo.restoreSession();
      if (user == null) {
        state = const AuthState(status: AuthStatus.unauthenticated);
        return;
      }
      state = AuthState(
        status: AuthStatus.authenticated,
        user: user,
        mustChangePassword: user.mustChangePassword ?? false,
      );
    } on ApiException catch (e) {
      if (e.code == 'admin_role') {
        state = AuthState(
          status: AuthStatus.unauthenticated,
          errorMessage: e.message,
        );
        return;
      }
      state = const AuthState(status: AuthStatus.unauthenticated);
    } catch (_) {
      state = const AuthState(status: AuthStatus.unauthenticated);
    }
  }

  Future<void> login({
    required String email,
    required String password,
  }) async {
    state = state.copyWith(clearError: true, clearSessionExpired: true);
    try {
      final user = await _repo.login(email: email, password: password);
      state = AuthState(
        status: AuthStatus.authenticated,
        user: user,
        mustChangePassword: user.mustChangePassword ?? false,
      );
    } catch (e) {
      state = AuthState(
        status: AuthStatus.unauthenticated,
        errorMessage: AuthRepository.friendlyLoginError(e),
      );
      rethrow;
    }
  }

  Future<RegisterResponse> register(RegisterRequest request) {
    return _repo.register(request);
  }

  Future<void> logout() async {
    await _repo.logout();
    state = const AuthState(status: AuthStatus.unauthenticated);
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await _repo.changePassword(
      currentPassword: currentPassword,
      newPassword: newPassword,
    );
    final user = state.user?.copyWith(mustChangePassword: false);
    state = state.copyWith(
      user: user,
      mustChangePassword: false,
      clearError: true,
    );
  }

  void updateDisplayName(String name) {
    final user = state.user;
    if (user == null) return;
    state = state.copyWith(user: user.copyWith(name: name));
  }

  Future<void> _handleSessionExpired() async {
    state = AuthState(
      status: AuthStatus.unauthenticated,
      sessionExpiredMessage: AuthRepository.friendlySessionExpiredMessage(),
    );
  }

  void clearMessages() {
    state = state.copyWith(clearError: true, clearSessionExpired: true);
  }
}

final authProvider = StateNotifierProvider<AuthNotifier, AuthState>((ref) {
  return AuthNotifier(
    ref.watch(authRepositoryProvider),
    ref.watch(apiClientProvider),
  );
});
