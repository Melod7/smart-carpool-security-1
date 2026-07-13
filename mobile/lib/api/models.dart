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

  AuthUser copyWith({bool? mustChangePassword, String? status}) {
    return AuthUser(
      id: id,
      email: email,
      name: name,
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
