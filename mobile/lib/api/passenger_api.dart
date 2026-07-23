import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

/// API mobile del pasajero (trips, eco, perfil, ratings).
class PassengerApi {
  PassengerApi(this._client);

  final ApiClient _client;

  Dio get _dio => _client.dio;

  /// Lista viajes disponibles. Con [lat]/[lng] el server incluye `suggestedWait`.
  Future<List<AvailableTrip>> getAvailableTrips({
    double? lat,
    double? lng,
  }) async {
    try {
      final res = await _dio.get<List<dynamic>>(
        '/trips/available',
        queryParameters: {
          if (lat != null) 'lat': lat,
          if (lng != null) 'lng': lng,
        },
      );
      final list = res.data ?? const [];
      return list
          .map((e) => AvailableTrip.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar los viajes.');
    }
  }

  /// Recalcula el punto de espera sugerido para un viaje.
  Future<SuggestedWait> getSuggestedPickup({
    required String tripId,
    required double lat,
    required double lng,
  }) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        '/trips/$tripId/suggested-pickup',
        queryParameters: {'lat': lat, 'lng': lng},
      );
      return SuggestedWait.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(
        e,
        fallback: 'No se pudo calcular el punto de espera.',
      );
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
      final error = mapDioError(e, fallback: 'No se pudo solicitar el viaje.');
      final message = switch (error.code) {
        'gender_required' =>
          'Completa tu género en Perfil antes de solicitar un viaje.',
        'gender_mismatch' =>
          'Por seguridad, solo puedes viajar con un conductor de tu mismo género.',
        'duplicate_request' => 'Ya solicitaste un lugar en este viaje.',
        'no_seats_available' => 'Este viaje ya no tiene asientos disponibles.',
        _ => error.message,
      };
      throw ApiException(
        message,
        statusCode: error.statusCode,
        code: error.code,
      );
    }
  }

  Future<EcoSummary> getEco() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>('/me/eco');
      return EcoSummary.fromJson(res.data ?? const {});
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar los EcoTokensUTN.');
    }
  }

  Future<PrizeRedemptionResult> redeemPrize(String prizeCode) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/me/eco/redeem',
        data: {'prizeCode': prizeCode},
      );
      return PrizeRedemptionResult.fromJson(res.data ?? const {});
    } on DioException catch (e) {
      final error = mapDioError(e, fallback: 'No se pudo canjear el premio.');
      throw ApiException(
        switch (error.code) {
          'insufficient_balance' =>
            'No tienes suficientes EcoTokensUTN para este premio.',
          'unknown_prize' => 'Premio no válido.',
          'gamification_disabled' =>
            'La gamificación está desactivada en tu universidad.',
          _ => error.message,
        },
        statusCode: error.statusCode,
        code: error.code,
      );
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

  Future<ProfileChangeResponse> requestProfileChange({
    required String name,
    required String career,
    required String gender,
    String? profileImage,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/me/profile-change',
        data: {
          'name': name,
          'career': career,
          'gender': gender,
          if (profileImage != null) 'profileImage': profileImage,
        },
      );
      return ProfileChangeResponse.fromJson(res.data!);
    } on DioException catch (e) {
      final error = mapDioError(
        e,
        fallback: 'No se pudo enviar la solicitud de cambio.',
      );
      if (error.code == 'profile_change_pending') {
        throw ApiException(
          'Ya tienes una solicitud de cambio de perfil pendiente.',
          statusCode: error.statusCode,
          code: error.code,
        );
      }
      throw error;
    }
  }

  Future<ModeChangeResponse> requestDriverMode(VehicleRegister vehicle) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/me/mode',
        data: {
          'mode': 'driver',
          'vehicle': vehicle.toJson(),
        },
      );
      return ModeChangeResponse.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(
        e,
        fallback: 'No se pudo solicitar el cambio a conductor.',
      );
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
      throw mapDioError(e,
          fallback: 'No se pudieron cargar las calificaciones.');
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
