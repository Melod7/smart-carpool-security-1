import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

class AuthApi {
  AuthApi(this._client);

  final ApiClient _client;

  Dio get _dio => _client.dio;

  Future<AuthResponse> login({
    required String email,
    required String password,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/auth/login',
        data: {'email': email, 'password': password},
      );
      return AuthResponse.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo iniciar sesión.');
    }
  }

  Future<AuthResponse> refresh(String refreshToken) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/auth/refresh',
        data: {'refreshToken': refreshToken},
      );
      return AuthResponse.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo renovar la sesión.');
    }
  }

  Future<void> logout(String? refreshToken) async {
    try {
      await _dio.post<void>(
        '/auth/logout',
        data: {'refreshToken': refreshToken},
      );
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cerrar sesión.');
    }
  }

  Future<RegisterResponse> register(RegisterRequest request) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/auth/register',
        data: request.toJson(),
      );
      return RegisterResponse.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo completar el registro.');
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _dio.post<void>(
        '/auth/change-password',
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        },
      );
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cambiar la contraseña.');
    }
  }

  Future<AuthUser> me() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>('/me');
      return AuthUser.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cargar el perfil.');
    }
  }
}
