import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

/// API mobile del pasajero (trips, eco, perfil, ratings).
class PassengerApi {
  PassengerApi(this._client);

  final ApiClient _client;

  Dio get _dio => _client.dio;

  Future<List<AvailableTrip>> getAvailableTrips() async {
    try {
      final res = await _dio.get<List<dynamic>>('/trips/available');
      final list = res.data ?? const [];
      return list
          .map((e) => AvailableTrip.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar los viajes.');
    }
  }

  Future<MyTripsResponse> getMyTrips({String period = 'total'}) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        '/trips/mine',
        queryParameters: {'period': period},
      );
      return MyTripsResponse.fromJson(res.data ?? const {});
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar tus viajes.');
    }
  }

  Future<RideRequest> requestRide({
    required String tripId,
    required String pickupText,
    required double pickupLat,
    required double pickupLng,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/trips/$tripId/requests',
        data: {
          'pickupText': pickupText,
          'pickupLat': pickupLat,
          'pickupLng': pickupLng,
        },
      );
      return RideRequest.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo solicitar el viaje.');
    }
  }

  Future<EcoSummary> getEco() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>('/me/eco');
      return EcoSummary.fromJson(res.data ?? const {});
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cargar EcoTokens.');
    }
  }

  Future<UserProfile> getProfile() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>('/me/profile');
      return UserProfile.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cargar el perfil.');
    }
  }

  Future<UserProfile> updateProfile({
    required String name,
    String? career,
  }) async {
    try {
      final res = await _dio.put<Map<String, dynamic>>(
        '/me/profile',
        data: {
          'name': name,
          if (career != null) 'career': career,
        },
      );
      return UserProfile.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo actualizar el perfil.');
    }
  }

  Future<List<EmergencyContact>> getEmergencyContacts() async {
    try {
      final res = await _dio.get<List<dynamic>>('/me/emergency-contacts');
      final list = res.data ?? const [];
      return list
          .map((e) => EmergencyContact.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar los contactos.');
    }
  }

  Future<EmergencyContact> createEmergencyContact({
    required String name,
    required String relationship,
    required String phone,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/me/emergency-contacts',
        data: {
          'name': name,
          'relationship': relationship,
          'phone': phone,
        },
      );
      return EmergencyContact.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo crear el contacto.');
    }
  }

  Future<void> deleteEmergencyContact(String id) async {
    try {
      await _dio.delete<void>('/me/emergency-contacts/$id');
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo eliminar el contacto.');
    }
  }

  Future<List<PendingRating>> getPendingRatings() async {
    try {
      final res = await _dio.get<List<dynamic>>('/ratings/pending');
      final list = res.data ?? const [];
      return list
          .map((e) => PendingRating.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar las calificaciones.');
    }
  }

  Future<RatingResult> submitRating({
    required String tripId,
    required String ratedUserId,
    required int stars,
    String? comment,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/trips/$tripId/ratings',
        data: {
          'ratedUserId': ratedUserId,
          'stars': stars,
          if (comment != null && comment.isNotEmpty) 'comment': comment,
        },
      );
      return RatingResult.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo enviar la calificación.');
    }
  }
}
