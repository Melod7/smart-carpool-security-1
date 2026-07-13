import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

class PublicApi {
  PublicApi(this._client);

  final ApiClient _client;

  Future<List<UniversityPublic>> getUniversities() async {
    try {
      final res = await _client.dio.get<List<dynamic>>('/public/universities');
      final list = res.data ?? const [];
      return list
          .map((e) => UniversityPublic.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar las universidades.');
    }
  }
}
