import 'package:dio/dio.dart';

import '../auth/token_storage.dart';
import 'models.dart';

/// Cliente HTTP Dio con Bearer JWT y refresh único ante 401.
class ApiClient {
  ApiClient({
    required this.baseUrl,
    required this.tokenStorage,
    Dio? dio,
  }) : dio = dio ??
            Dio(
              BaseOptions(
                baseUrl: baseUrl,
                headers: {'Content-Type': 'application/json'},
                connectTimeout: const Duration(seconds: 15),
                receiveTimeout: const Duration(seconds: 20),
              ),
            ) {
    this.dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: _onRequest,
        onError: _onError,
      ),
    );
  }

  final String baseUrl;
  final TokenStorage tokenStorage;
  final Dio dio;

  /// Se invoca cuando el refresh falla (token expirado/revocado).
  Future<void> Function()? onSessionExpired;

  Future<void> _onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await tokenStorage.getAccessToken();
    if (token != null && token.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  Future<void> _onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final response = err.response;
    final request = err.requestOptions;
    final alreadyRetried = request.extra['auth_retry'] == true;
    final path = request.path;

    final isAuthEndpoint = path.contains('/auth/login') ||
        path.contains('/auth/refresh') ||
        path.contains('/auth/register');

    if (response?.statusCode != 401 || alreadyRetried || isAuthEndpoint) {
      handler.next(err);
      return;
    }

    try {
      final newAccess = await _refreshAccessToken();
      if (newAccess == null) {
        await _expireSession();
        handler.next(err);
        return;
      }

      request.extra['auth_retry'] = true;
      request.headers['Authorization'] = 'Bearer $newAccess';
      final clone = await dio.fetch(request);
      handler.resolve(clone);
    } catch (_) {
      await _expireSession();
      handler.next(err);
    }
  }

  Future<String?> _refreshAccessToken() async {
    final refresh = await tokenStorage.getRefreshToken();
    if (refresh == null || refresh.isEmpty) return null;

    // Dio limpio para evitar re-entrada del interceptor.
    final bare = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        headers: {'Content-Type': 'application/json'},
      ),
    );

    try {
      final res = await bare.post<Map<String, dynamic>>(
        '/auth/refresh',
        data: {'refreshToken': refresh},
      );
      final data = AuthResponse.fromJson(res.data!);
      await tokenStorage.saveTokens(
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
      );
      return data.accessToken;
    } on DioException {
      return null;
    }
  }

  Future<void> _expireSession() async {
    await tokenStorage.clear();
    final cb = onSessionExpired;
    if (cb != null) {
      await cb();
    }
  }
}

ApiException mapDioError(DioException e, {String fallback = 'Error de red'}) {
  final data = e.response?.data;
  String? detail;
  String? code;
  if (data is Map<String, dynamic>) {
    detail = data['detail'] as String? ?? data['title'] as String?;
    code = data['code'] as String?;
  }
  return ApiException(
    detail ?? fallback,
    statusCode: e.response?.statusCode,
    code: code,
  );
}
