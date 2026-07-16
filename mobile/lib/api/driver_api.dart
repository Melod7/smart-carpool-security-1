import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models.dart';

/// API mobile del conductor (viajes, solicitudes, vehículo, eco, perfil).
class DriverApi {
  DriverApi(this._client);

  final ApiClient _client;

  Dio get _dio => _client.dio;

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

  /// Publica un viaje. Preferir [waypoints] (≥2, ≤8); legacy originLat/Lng
  /// se mantiene para el modal textual de fallback.
  Future<AvailableTrip> publishTrip({
    required String destinationCampusId,
    required DateTime departureAt,
    required int seatsAvailable,
    String? originText,
    double? originLat,
    double? originLng,
    List<TripWaypoint>? waypoints,
  }) async {
    try {
      final wp = waypoints;
      final body = <String, dynamic>{
        'destinationCampusId': destinationCampusId,
        'departureAt': departureAt.toUtc().toIso8601String(),
        'seatsAvailable': seatsAvailable,
        if (originText != null && originText.isNotEmpty)
          'originText': originText,
        if (wp != null && wp.isNotEmpty)
          'waypoints': wp.map((w) => w.toJson()).toList()
        else ...{
          if (originLat != null) 'originLat': originLat,
          if (originLng != null) 'originLng': originLng,
        },
      };
      final res = await _dio.post<Map<String, dynamic>>('/trips', data: body);
      return AvailableTrip.fromJson(res.data!);
    } on DioException catch (e) {
      final error = mapDioError(e, fallback: 'No se pudo publicar el viaje.');
      final message = switch (error.code) {
        'vehicle_required' =>
          'Registra un vehículo antes de publicar una ruta.',
        'invalid_seats' => 'Los asientos superan la capacidad del vehículo.',
        'max_daily_trips' =>
          'Alcanzaste el máximo de rutas permitidas para ese día.',
        'campus_not_found' => 'El campus seleccionado ya no está disponible.',
        'invalid_waypoints' => 'Marca entre 2 y 8 puntos válidos para la ruta.',
        'driver_only' => 'Tu cuenta no está activa como conductor.',
        _ => error.message,
      };
      throw ApiException(
        message,
        statusCode: error.statusCode,
        code: error.code,
      );
    }
  }

  /// Vista previa Directions (sin persistir) para el mapa de publicación.
  Future<({String? polyline, double distanceKm, bool directionsOk})>
      previewRoute({
    required String destinationCampusId,
    required List<TripWaypoint> waypoints,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/trips/preview-route',
        data: {
          'destinationCampusId': destinationCampusId,
          'waypoints': waypoints.map((w) => w.toJson()).toList(),
        },
      );
      final data = res.data ?? const {};
      return (
        polyline: data['polyline'] as String?,
        distanceKm: (data['distanceKm'] as num?)?.toDouble() ?? 0,
        directionsOk: data['directionsOk'] as bool? ?? false,
      );
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo calcular la ruta.');
    }
  }

  Future<AvailableTrip> startTrip(String tripId) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>('/trips/$tripId/start');
      return AvailableTrip.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo iniciar el viaje.');
    }
  }

  Future<AvailableTrip> completeTrip(String tripId) async {
    try {
      final res =
          await _dio.post<Map<String, dynamic>>('/trips/$tripId/complete');
      return AvailableTrip.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo completar el viaje.');
    }
  }

  Future<List<RideRequest>> getTripRequests(String tripId) async {
    try {
      final res = await _dio.get<List<dynamic>>('/trips/$tripId/requests');
      final list = res.data ?? const [];
      return list
          .map((e) => RideRequest.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudieron cargar las solicitudes.');
    }
  }

  Future<RideRequest> acceptRequest(String requestId) async {
    try {
      final res =
          await _dio.post<Map<String, dynamic>>('/requests/$requestId/accept');
      return RideRequest.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo aceptar la solicitud.');
    }
  }

  Future<RideRequest> rejectRequest(String requestId) async {
    try {
      final res =
          await _dio.post<Map<String, dynamic>>('/requests/$requestId/reject');
      return RideRequest.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo rechazar la solicitud.');
    }
  }

  Future<Vehicle> getVehicle() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>('/me/vehicle');
      return Vehicle.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo cargar el vehículo.');
    }
  }

  Future<VehicleChangeRequest?> requestVehicleChange({
    required String makeModel,
    required String plate,
    required String color,
    required int seatsTotal,
    required String image,
  }) async {
    try {
      final res = await _dio.put<Map<String, dynamic>>(
        '/me/vehicle',
        data: {
          'makeModel': makeModel,
          'plate': plate,
          'color': color,
          'seatsTotal': seatsTotal,
          'image': image,
        },
      );
      // 200 = alta directa (sin vehículo previo); 202 = pendiente de aprobación
      if (res.statusCode == 202 || res.data?['kind'] == 'vehicle_change') {
        return VehicleChangeRequest.fromJson(res.data!);
      }
      return null;
    } on DioException catch (e) {
      throw mapDioError(
        e,
        fallback: 'No se pudo solicitar el cambio de vehículo.',
      );
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
    required String career,
    required String gender,
    String? profileImage,
  }) async {
    try {
      final res = await _dio.put<Map<String, dynamic>>(
        '/me/profile',
        data: {
          'name': name,
          'career': career,
          'gender': gender,
          if (profileImage != null) 'profileImage': profileImage,
        },
      );
      return UserProfile.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(e, fallback: 'No se pudo actualizar el perfil.');
    }
  }

  Future<ModeChangeResponse> switchToPassenger() async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        '/me/mode',
        data: const {'mode': 'passenger'},
      );
      return ModeChangeResponse.fromJson(res.data!);
    } on DioException catch (e) {
      throw mapDioError(
        e,
        fallback: 'No se pudo cambiar al modo pasajero.',
      );
    }
  }
}
