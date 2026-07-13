import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

/// API mobile de tracking (`POST /trips/{id}/pings`, `GET /trips/{id}/tracking`).
class TrackingApi {
  TrackingApi(this._client);

  final ApiClient _client;

  Dio get _dio => _client.dio;

  /// Envía un ping GPS. Solo válido en trips `in_progress`.
  Future<LocationPing> postPing({
    required String tripId,
    required double lat,
    required double lng,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/trips/$tripId/pings',
        data: {'lat': lat, 'lng': lng},
      );
      return LocationPing.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo enviar la ubicación.');
    }
  }

  /// Estado de tracking con visibilidad scoped al rol.
  /// Backend responde 409 si el trip no está `in_progress`.
  Future<TripTracking> getTracking(String tripId) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        '/trips/$tripId/tracking',
      );
      return TripTracking.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cargar el tracking.');
    }
  }
}
