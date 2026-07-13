import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

/// API mobile de alertas SOS (`POST /sos`, `POST /sos/{id}/close`).
class SosApi {
  SosApi(this._client);

  final ApiClient _client;

  Dio get _dio => _client.dio;

  Future<SosAlert> createSos({
    required double lat,
    required double lng,
    String? tripId,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/sos',
        data: {
          'lat': lat,
          'lng': lng,
          if (tripId != null && tripId.isNotEmpty) 'tripId': tripId,
        },
      );
      return SosAlert.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo enviar la alerta SOS.');
    }
  }

  Future<SosAlert> closeSos(String alertId) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/sos/$alertId/close',
      );
      return SosAlert.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cerrar la alerta SOS.');
    }
  }
}
