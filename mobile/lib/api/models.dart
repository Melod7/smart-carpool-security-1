/// Modelos JSON (claves en inglés, alineadas con la API).

class AuthUser {
  const AuthUser({
    required this.id,
    required this.email,
    required this.name,
    required this.role,
    this.universityId,
    this.campusId,
    this.mustChangePassword,
    this.status,
  });

  final String id;
  final String email;
  final String name;
  final String role;
  final String? universityId;
  final String? campusId;
  final bool? mustChangePassword;
  final String? status;

  bool get isDriver => role == 'driver';
  bool get isPassenger => role == 'passenger';
  bool get isAdminRole =>
      role == 'coordinador' || role == 'super_admin' || role == 'university_admin';

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'] as String,
      email: json['email'] as String,
      name: json['name'] as String,
      role: json['role'] as String,
      universityId: json['universityId'] as String?,
      campusId: json['campusId'] as String?,
      mustChangePassword: json['mustChangePassword'] as bool?,
      status: json['status'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'email': email,
        'name': name,
        'role': role,
        'universityId': universityId,
        'campusId': campusId,
        'mustChangePassword': mustChangePassword,
        'status': status,
      };

  AuthUser copyWith({
    String? name,
    bool? mustChangePassword,
    String? status,
  }) {
    return AuthUser(
      id: id,
      email: email,
      name: name ?? this.name,
      role: role,
      universityId: universityId,
      campusId: campusId,
      mustChangePassword: mustChangePassword ?? this.mustChangePassword,
      status: status ?? this.status,
    );
  }
}

class AuthResponse {
  const AuthResponse({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresIn,
    required this.mustChangePassword,
    required this.user,
  });

  final String accessToken;
  final String refreshToken;
  final int expiresIn;
  final bool mustChangePassword;
  final AuthUser user;

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      expiresIn: json['expiresIn'] as int,
      mustChangePassword: json['mustChangePassword'] as bool? ?? false,
      user: AuthUser.fromJson(json['user'] as Map<String, dynamic>),
    );
  }
}

class CampusPublic {
  const CampusPublic({required this.id, required this.name});

  final String id;
  final String name;

  factory CampusPublic.fromJson(Map<String, dynamic> json) {
    return CampusPublic(
      id: json['id'] as String,
      name: json['name'] as String,
    );
  }
}

class UniversityPublic {
  const UniversityPublic({
    required this.id,
    required this.name,
    required this.campuses,
  });

  final String id;
  final String name;
  final List<CampusPublic> campuses;

  factory UniversityPublic.fromJson(Map<String, dynamic> json) {
    final raw = json['campuses'] as List<dynamic>? ?? const [];
    return UniversityPublic(
      id: json['id'] as String,
      name: json['name'] as String,
      campuses: raw
          .map((e) => CampusPublic.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class VehicleRegister {
  const VehicleRegister({
    required this.makeModel,
    required this.plate,
    required this.color,
    required this.seatsTotal,
  });

  final String makeModel;
  final String plate;
  final String color;
  final int seatsTotal;

  Map<String, dynamic> toJson() => {
        'makeModel': makeModel,
        'plate': plate,
        'color': color,
        'seatsTotal': seatsTotal,
      };
}

class RegisterRequest {
  const RegisterRequest({
    required this.universityId,
    required this.campusId,
    required this.role,
    required this.name,
    required this.email,
    required this.password,
    this.career,
    this.idNumber,
    this.vehicle,
  });

  final String universityId;
  final String campusId;
  final String role;
  final String name;
  final String email;
  final String password;
  final String? career;
  final String? idNumber;
  final VehicleRegister? vehicle;

  Map<String, dynamic> toJson() => {
        'universityId': universityId,
        'campusId': campusId,
        'role': role,
        'name': name,
        'email': email,
        'password': password,
        if (career != null && career!.isNotEmpty) 'career': career,
        if (idNumber != null && idNumber!.isNotEmpty) 'idNumber': idNumber,
        if (vehicle != null) 'vehicle': vehicle!.toJson(),
      };
}

class RegisterResponse {
  const RegisterResponse({
    required this.id,
    required this.status,
    required this.email,
  });

  final String id;
  final String status;
  final String email;

  factory RegisterResponse.fromJson(Map<String, dynamic> json) {
    return RegisterResponse(
      id: json['id'] as String,
      status: json['status'] as String,
      email: json['email'] as String,
    );
  }
}

class ApiException implements Exception {
  ApiException(this.message, {this.statusCode, this.code});

  final String message;
  final int? statusCode;
  final String? code;

  @override
  String toString() => message;
}

double _asDouble(dynamic value) {
  if (value == null) return 0;
  if (value is num) return value.toDouble();
  return double.tryParse(value.toString()) ?? 0;
}

DateTime _asDateTime(dynamic value) {
  if (value is DateTime) return value;
  return DateTime.parse(value as String);
}

/// Waypoint de ruta del conductor (POST /trips, respuesta de viaje).
class TripWaypoint {
  const TripWaypoint({
    required this.lat,
    required this.lng,
    this.label,
    this.seq,
  });

  final double lat;
  final double lng;
  final String? label;
  final int? seq;

  Map<String, dynamic> toJson() => {
        'lat': lat,
        'lng': lng,
        if (label != null && label!.isNotEmpty) 'label': label,
        if (seq != null) 'seq': seq,
      };

  factory TripWaypoint.fromJson(Map<String, dynamic> json) {
    return TripWaypoint(
      lat: _asDouble(json['lat']),
      lng: _asDouble(json['lng']),
      label: json['label'] as String?,
      seq: json['seq'] as int?,
    );
  }

  TripWaypoint copyWith({
    double? lat,
    double? lng,
    String? label,
    int? seq,
  }) {
    return TripWaypoint(
      lat: lat ?? this.lat,
      lng: lng ?? this.lng,
      label: label ?? this.label,
      seq: seq ?? this.seq,
    );
  }
}

/// Punto de espera sugerido (GET /trips/available?lat=&lng=).
class SuggestedWait {
  const SuggestedWait({
    required this.lat,
    required this.lng,
    required this.distanceM,
    required this.segmentIndex,
    this.tooFar = false,
  });

  final double lat;
  final double lng;
  final double distanceM;
  final int segmentIndex;
  final bool tooFar;

  factory SuggestedWait.fromJson(Map<String, dynamic> json) {
    return SuggestedWait(
      lat: _asDouble(json['lat']),
      lng: _asDouble(json['lng']),
      distanceM: _asDouble(json['distanceM']),
      segmentIndex: json['segmentIndex'] as int? ?? 0,
      tooFar: json['tooFar'] as bool? ?? false,
    );
  }
}

/// Viaje disponible (GET /trips/available).
class AvailableTrip {
  const AvailableTrip({
    required this.id,
    required this.status,
    required this.originText,
    required this.originLat,
    required this.originLng,
    required this.destinationCampusId,
    required this.departureAt,
    required this.seatsAvailable,
    required this.distanceKm,
    required this.co2SavedKg,
    required this.driverId,
    this.polyline,
    this.waypoints = const [],
    this.suggestedWait,
  });

  final String id;
  final String status;
  final String originText;
  final double originLat;
  final double originLng;
  final String destinationCampusId;
  final DateTime departureAt;
  final int seatsAvailable;
  final double distanceKm;
  final double co2SavedKg;
  final String driverId;
  final String? polyline;
  final List<TripWaypoint> waypoints;
  final SuggestedWait? suggestedWait;

  factory AvailableTrip.fromJson(Map<String, dynamic> json) {
    final rawWp = json['waypoints'] as List<dynamic>? ?? const [];
    return AvailableTrip(
      id: json['id'] as String,
      status: json['status'] as String,
      originText: json['originText'] as String? ?? '',
      originLat: _asDouble(json['originLat']),
      originLng: _asDouble(json['originLng']),
      destinationCampusId: json['destinationCampusId'] as String,
      departureAt: _asDateTime(json['departureAt']),
      seatsAvailable: json['seatsAvailable'] as int? ?? 0,
      distanceKm: _asDouble(json['distanceKm']),
      co2SavedKg: _asDouble(json['co2SavedKg']),
      driverId: json['driverId'] as String,
      polyline: json['polyline'] as String?,
      waypoints: rawWp
          .map((e) => TripWaypoint.fromJson(e as Map<String, dynamic>))
          .toList(),
      suggestedWait: json['suggestedWait'] == null
          ? null
          : SuggestedWait.fromJson(
              json['suggestedWait'] as Map<String, dynamic>,
            ),
    );
  }
}

class TripStats {
  const TripStats({
    required this.period,
    required this.trips,
    required this.km,
    required this.co2Kg,
  });

  final String period;
  final int trips;
  final double km;
  final double co2Kg;

  factory TripStats.fromJson(Map<String, dynamic> json) {
    return TripStats(
      period: json['period'] as String? ?? 'total',
      trips: json['trips'] as int? ?? 0,
      km: _asDouble(json['km']),
      co2Kg: _asDouble(json['co2Kg']),
    );
  }
}

/// Viaje propio (elemento de GET /trips/mine).
class MyTrip {
  const MyTrip({
    required this.id,
    required this.status,
    required this.role,
    required this.departureAt,
    required this.originText,
    required this.destinationCampusName,
    required this.distanceKm,
    required this.co2SavedKg,
    this.requestStatus,
  });

  final String id;
  final String status;
  final String role;
  final String? requestStatus;
  final DateTime departureAt;
  final String originText;
  final String destinationCampusName;
  final double distanceKm;
  final double co2SavedKg;

  bool get isUpcoming =>
      status == 'scheduled' || status == 'in_progress';

  bool get isHistory =>
      status == 'completed' || status == 'cancelled';

  factory MyTrip.fromJson(Map<String, dynamic> json) {
    return MyTrip(
      id: json['id'] as String,
      status: json['status'] as String,
      role: json['role'] as String? ?? 'passenger',
      requestStatus: json['requestStatus'] as String?,
      departureAt: _asDateTime(json['departureAt']),
      originText: json['originText'] as String? ?? '',
      destinationCampusName: json['destinationCampusName'] as String? ?? '',
      distanceKm: _asDouble(json['distanceKm']),
      co2SavedKg: _asDouble(json['co2SavedKg']),
    );
  }
}

class MyTripsResponse {
  const MyTripsResponse({required this.trips, required this.stats});

  final List<MyTrip> trips;
  final TripStats stats;

  factory MyTripsResponse.fromJson(Map<String, dynamic> json) {
    final raw = json['trips'] as List<dynamic>? ?? const [];
    return MyTripsResponse(
      trips: raw
          .map((e) => MyTrip.fromJson(e as Map<String, dynamic>))
          .toList(),
      stats: TripStats.fromJson(
        json['stats'] as Map<String, dynamic>? ?? const {},
      ),
    );
  }
}

class RideRequest {
  const RideRequest({
    required this.id,
    required this.tripId,
    required this.passengerId,
    required this.pickupText,
    required this.pickupLat,
    required this.pickupLng,
    required this.status,
  });

  final String id;
  final String tripId;
  final String passengerId;
  final String pickupText;
  final double pickupLat;
  final double pickupLng;
  final String status;

  factory RideRequest.fromJson(Map<String, dynamic> json) {
    return RideRequest(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      passengerId: json['passengerId'] as String,
      pickupText: json['pickupText'] as String? ?? '',
      pickupLat: _asDouble(json['pickupLat']),
      pickupLng: _asDouble(json['pickupLng']),
      status: json['status'] as String,
    );
  }
}

class EcoSummary {
  const EcoSummary({
    required this.balance,
    required this.lifetime,
    required this.level,
    required this.gamificationEnabled,
    required this.transactions,
    required this.totalCount,
    this.progress,
  });

  final int balance;
  final int lifetime;
  final String level;
  final double? progress;
  final bool gamificationEnabled;
  final List<EcoTransaction> transactions;
  final int totalCount;

  factory EcoSummary.fromJson(Map<String, dynamic> json) {
    final raw = json['transactions'] as List<dynamic>? ?? const [];
    return EcoSummary(
      balance: json['balance'] as int? ?? 0,
      lifetime: json['lifetime'] as int? ?? 0,
      level: json['level'] as String? ?? 'Bronce',
      progress: json['progress'] == null ? null : _asDouble(json['progress']),
      gamificationEnabled: json['gamificationEnabled'] as bool? ?? true,
      transactions: raw
          .map((e) => EcoTransaction.fromJson(e as Map<String, dynamic>))
          .toList(),
      totalCount: json['totalCount'] as int? ?? 0,
    );
  }
}

class EcoTransaction {
  const EcoTransaction({
    required this.id,
    required this.type,
    required this.amount,
    required this.sourceId,
    required this.createdAt,
  });

  final String id;
  final String type;
  final int amount;
  final String sourceId;
  final DateTime createdAt;

  factory EcoTransaction.fromJson(Map<String, dynamic> json) {
    return EcoTransaction(
      id: json['id'] as String,
      type: json['type'] as String,
      amount: json['amount'] as int? ?? 0,
      sourceId: json['sourceId'] as String? ?? '',
      createdAt: _asDateTime(json['createdAt']),
    );
  }
}

class UserProfile {
  const UserProfile({
    required this.id,
    required this.name,
    required this.email,
    required this.role,
    required this.status,
    this.career,
    this.campusId,
    this.universityId,
  });

  final String id;
  final String name;
  final String email;
  final String role;
  final String? career;
  final String? campusId;
  final String? universityId;
  final String status;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      role: json['role'] as String,
      career: json['career'] as String?,
      campusId: json['campusId'] as String?,
      universityId: json['universityId'] as String?,
      status: json['status'] as String? ?? 'active',
    );
  }
}

class EmergencyContact {
  const EmergencyContact({
    required this.id,
    required this.name,
    required this.relationship,
    required this.phone,
  });

  final String id;
  final String name;
  final String relationship;
  final String phone;

  factory EmergencyContact.fromJson(Map<String, dynamic> json) {
    return EmergencyContact(
      id: json['id'] as String,
      name: json['name'] as String,
      relationship: json['relationship'] as String,
      phone: json['phone'] as String,
    );
  }
}

class PendingRating {
  const PendingRating({
    required this.tripId,
    required this.ratedUserId,
    required this.ratedUserName,
    required this.roleToRate,
    this.expiresAt,
  });

  final String tripId;
  final String ratedUserId;
  final String ratedUserName;
  final String roleToRate;
  final DateTime? expiresAt;

  factory PendingRating.fromJson(Map<String, dynamic> json) {
    return PendingRating(
      tripId: json['tripId'] as String,
      ratedUserId: json['ratedUserId'] as String,
      ratedUserName: json['ratedUserName'] as String? ?? '',
      roleToRate: json['roleToRate'] as String? ?? 'driver',
      expiresAt: json['expiresAt'] == null
          ? null
          : _asDateTime(json['expiresAt']),
    );
  }
}

class RatingResult {
  const RatingResult({
    required this.id,
    required this.tripId,
    required this.raterId,
    required this.ratedUserId,
    required this.stars,
    this.comment,
  });

  final String id;
  final String tripId;
  final String raterId;
  final String ratedUserId;
  final int stars;
  final String? comment;

  factory RatingResult.fromJson(Map<String, dynamic> json) {
    return RatingResult(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      raterId: json['raterId'] as String,
      ratedUserId: json['ratedUserId'] as String,
      stars: json['stars'] as int? ?? 0,
      comment: json['comment'] as String?,
    );
  }
}

/// Alerta SOS (POST /sos, POST /sos/{id}/close).
class SosAlert {
  const SosAlert({
    required this.id,
    required this.status,
    required this.lat,
    required this.lng,
    required this.firedAt,
    required this.universityId,
    required this.userId,
    this.tripId,
    this.resolvedBy,
    this.resolvedAt,
  });

  final String id;
  final String status;
  final double lat;
  final double lng;
  final String? tripId;
  final DateTime firedAt;
  final String? resolvedBy;
  final DateTime? resolvedAt;
  final String universityId;
  final String userId;

  bool get isActive => status == 'active';

  factory SosAlert.fromJson(Map<String, dynamic> json) {
    return SosAlert(
      id: json['id'] as String,
      status: json['status'] as String? ?? 'active',
      lat: _asDouble(json['lat']),
      lng: _asDouble(json['lng']),
      tripId: json['tripId'] as String?,
      firedAt: _asDateTime(json['firedAt']),
      resolvedBy: json['resolvedBy'] as String?,
      resolvedAt: json['resolvedAt'] == null
          ? null
          : _asDateTime(json['resolvedAt']),
      universityId: json['universityId'] as String? ?? '',
      userId: json['userId'] as String? ?? '',
    );
  }
}

/// Ping de ubicación (POST /trips/{id}/pings).
class LocationPing {
  const LocationPing({
    required this.id,
    required this.tripId,
    required this.userId,
    required this.lat,
    required this.lng,
    required this.recordedAt,
  });

  final String id;
  final String tripId;
  final String userId;
  final double lat;
  final double lng;
  final DateTime recordedAt;

  factory LocationPing.fromJson(Map<String, dynamic> json) {
    return LocationPing(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      userId: json['userId'] as String,
      lat: _asDouble(json['lat']),
      lng: _asDouble(json['lng']),
      recordedAt: _asDateTime(json['recordedAt']),
    );
  }
}

/// Participante visible en GET /trips/{id}/tracking.
class TrackingParticipant {
  const TrackingParticipant({
    required this.userId,
    required this.role,
    required this.lat,
    required this.lng,
    required this.source,
    this.name,
    this.recordedAt,
  });

  final String userId;
  final String role;
  final String? name;
  final double lat;
  final double lng;
  final DateTime? recordedAt;

  /// `ping` | `pickup`
  final String source;

  bool get isDriver => role == 'driver';
  bool get isPickup => source == 'pickup' || source == 'boarding';
  bool get isBoardingPoint =>
      role == 'boarding_point' || source == 'boarding';
  bool get isLivePassenger =>
      role == 'passenger' || role == 'boarding';

  factory TrackingParticipant.fromJson(Map<String, dynamic> json) {
    return TrackingParticipant(
      userId: json['userId'] as String,
      role: json['role'] as String? ?? 'passenger',
      name: json['name'] as String?,
      lat: _asDouble(json['lat']),
      lng: _asDouble(json['lng']),
      recordedAt: json['recordedAt'] == null
          ? null
          : _asDateTime(json['recordedAt']),
      source: json['source'] as String? ?? 'ping',
    );
  }
}

/// Estado de tracking (GET /trips/{id}/tracking) — scheduled o in_progress.
class TripTracking {
  const TripTracking({
    required this.tripId,
    required this.status,
    required this.participants,
    this.polyline,
    this.waypoints = const [],
  });

  final String tripId;
  final String status;
  final String? polyline;
  final List<TripWaypoint> waypoints;
  final List<TrackingParticipant> participants;

  bool get isActive => status == 'in_progress' || status == 'scheduled';
  bool get isTerminal => status == 'completed' || status == 'cancelled';

  factory TripTracking.fromJson(Map<String, dynamic> json) {
    final raw = json['participants'] as List<dynamic>? ?? const [];
    final wp = json['waypoints'] as List<dynamic>? ?? const [];
    return TripTracking(
      tripId: json['tripId'] as String,
      status: json['status'] as String? ?? 'in_progress',
      polyline: json['polyline'] as String?,
      waypoints: wp
          .map((e) => TripWaypoint.fromJson(e as Map<String, dynamic>))
          .toList(),
      participants: raw
          .map((e) => TrackingParticipant.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

/// Semilla de geometría para mapa scheduled (sin pings aún).
class TripMapSeed {
  const TripMapSeed({
    required this.tripId,
    required this.status,
    this.polyline,
    this.originLat,
    this.originLng,
    this.pickupLat,
    this.pickupLng,
    this.waypoints = const [],
  });

  final String tripId;
  final String status;
  final String? polyline;
  final double? originLat;
  final double? originLng;
  final double? pickupLat;
  final double? pickupLng;
  final List<TripWaypoint> waypoints;

  bool get hasOrigin => originLat != null && originLng != null;
  bool get hasPickup => pickupLat != null && pickupLng != null;
  bool get hasWaypoints => waypoints.length >= 2;

  TripMapSeed copyWith({
    String? status,
    String? polyline,
    double? originLat,
    double? originLng,
    double? pickupLat,
    double? pickupLng,
    List<TripWaypoint>? waypoints,
  }) {
    return TripMapSeed(
      tripId: tripId,
      status: status ?? this.status,
      polyline: polyline ?? this.polyline,
      originLat: originLat ?? this.originLat,
      originLng: originLng ?? this.originLng,
      pickupLat: pickupLat ?? this.pickupLat,
      pickupLng: pickupLng ?? this.pickupLng,
      waypoints: waypoints ?? this.waypoints,
    );
  }

  factory TripMapSeed.fromAvailableTrip(AvailableTrip trip) {
    return TripMapSeed(
      tripId: trip.id,
      status: trip.status,
      polyline: trip.polyline,
      originLat: trip.originLat,
      originLng: trip.originLng,
      waypoints: trip.waypoints,
      pickupLat: trip.suggestedWait?.lat,
      pickupLng: trip.suggestedWait?.lng,
    );
  }
}

/// Vehículo del conductor (GET/PUT /me/vehicle).
class Vehicle {
  const Vehicle({
    required this.id,
    required this.makeModel,
    required this.plate,
    required this.color,
    required this.seatsTotal,
  });

  final String id;
  final String makeModel;
  final String plate;
  final String color;
  final int seatsTotal;

  factory Vehicle.fromJson(Map<String, dynamic> json) {
    return Vehicle(
      id: json['id'] as String,
      makeModel: json['makeModel'] as String? ?? '',
      plate: json['plate'] as String? ?? '',
      color: json['color'] as String? ?? '',
      seatsTotal: json['seatsTotal'] as int? ?? 4,
    );
  }
}

/// Respuesta 202 al solicitar cambio de vehículo.
class VehicleChangeRequest {
  const VehicleChangeRequest({
    required this.id,
    required this.status,
    required this.kind,
    this.message,
  });

  final String id;
  final String status;
  final String kind;
  final String? message;

  factory VehicleChangeRequest.fromJson(Map<String, dynamic> json) {
    return VehicleChangeRequest(
      id: json['id'] as String? ?? '',
      status: json['status'] as String? ?? 'pending',
      kind: json['kind'] as String? ?? 'vehicle_change',
      message: json['message'] as String?,
    );
  }
}
