import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../constants/utn_careers.dart';
import '../../theme/kubix_theme.dart';
import '../../widgets/image_picker_field.dart';

/// Validación pura del wizard (testeable sin widgets).
class RegisterWizardValidation {
  static String? validateUniversity(String? universityId) {
    if (universityId == null || universityId.isEmpty) {
      return 'Selecciona una universidad';
    }
    return null;
  }

  static String? validateCampus(String? campusId) {
    if (campusId == null || campusId.isEmpty) {
      return 'Selecciona un campus';
    }
    return null;
  }

  static String? validateRole(String? role) {
    if (role != 'driver' && role != 'passenger') {
      return 'Selecciona un rol';
    }
    return null;
  }

  static Map<String, String> validatePersonal({
    required String name,
    required String email,
    required String password,
    String career = '',
    String idNumber = '',
    String? gender,
    String? profileImage,
  }) {
    final errors = <String, String>{};
    if (name.trim().isEmpty) errors['name'] = 'Ingresa tu nombre';
    if (email.trim().isEmpty) {
      errors['email'] = 'Ingresa tu correo';
    } else if (!email.contains('@')) {
      errors['email'] = 'Correo inválido';
    }
    if (password.isEmpty) {
      errors['password'] = 'Ingresa una contraseña';
    } else if (password.length < 8) {
      errors['password'] = 'Mínimo 8 caracteres';
    }
    if (career.trim().isEmpty) errors['career'] = 'Selecciona tu carrera';
    if (idNumber.trim().isEmpty) errors['idNumber'] = 'Ingresa tu cédula / ID';
    if (gender != 'male' && gender != 'female') {
      errors['gender'] = 'Selecciona tu género';
    }
    if (profileImage == null || profileImage.isEmpty) {
      errors['profileImage'] = 'Sube una imagen de perfil';
    }
    return errors;
  }

  static Map<String, String> validateVehicle({
    required String makeModel,
    required String plate,
    required String color,
    required String seatsText,
    String? image,
  }) {
    final errors = <String, String>{};
    if (makeModel.trim().isEmpty) {
      errors['makeModel'] = 'Ingresa marca y modelo';
    }
    if (plate.trim().isEmpty) errors['plate'] = 'Ingresa la placa';
    if (color.trim().isEmpty) errors['color'] = 'Ingresa el color';
    final seats = int.tryParse(seatsText.trim());
    if (seats == null || seats < 1 || seats > 8) {
      errors['seats'] = 'Asientos entre 1 y 8';
    }
    if (image == null || image.isEmpty) {
      errors['image'] = 'Sube una imagen del vehículo';
    }
    return errors;
  }
}

class RegisterWizardPage extends ConsumerStatefulWidget {
  const RegisterWizardPage({super.key});

  @override
  ConsumerState<RegisterWizardPage> createState() => _RegisterWizardPageState();
}

class _RegisterWizardPageState extends ConsumerState<RegisterWizardPage> {
  int _step = 0;
  bool _loadingUnis = true;
  bool _submitting = false;
  String? _error;
  List<UniversityPublic> _universities = const [];

  String? _universityId;
  String? _campusId;
  String? _role;

  final _nameCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  final _careerCtrl = TextEditingController();
  final _idCtrl = TextEditingController();
  String? _gender;
  String? _profileImage;

  final _makeCtrl = TextEditingController();
  final _plateCtrl = TextEditingController();
  final _colorCtrl = TextEditingController();
  final _seatsCtrl = TextEditingController(text: '3');
  String? _vehicleImage;

  Map<String, String> _fieldErrors = {};

  @override
  void initState() {
    super.initState();
    _loadUniversities();
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _emailCtrl.dispose();
    _passwordCtrl.dispose();
    _careerCtrl.dispose();
    _idCtrl.dispose();
    _makeCtrl.dispose();
    _plateCtrl.dispose();
    _colorCtrl.dispose();
    _seatsCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadUniversities() async {
    setState(() {
      _loadingUnis = true;
      _error = null;
    });
    try {
      final list = await ref.read(publicApiProvider).getUniversities();
      if (!mounted) return;
      setState(() {
        _universities = list;
        _loadingUnis = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingUnis = false;
        _error = e is ApiException
            ? e.message
            : 'No se pudieron cargar las universidades.';
      });
    }
  }

  UniversityPublic? get _selectedUniversity {
    for (final u in _universities) {
      if (u.id == _universityId) return u;
    }
    return null;
  }

  List<CampusPublic> get _campuses => _selectedUniversity?.campuses ?? const [];

  int get _lastStep => _role == 'driver' ? 5 : 4;

  String get _stepTitle {
    switch (_step) {
      case 0:
        return 'Universidad';
      case 1:
        return 'Campus';
      case 2:
        return 'Rol';
      case 3:
        return 'Datos personales';
      case 4:
        return _role == 'driver' ? 'Vehículo' : 'Confirmar';
      case 5:
        return 'Confirmar';
      default:
        return 'Registro';
    }
  }

  bool _validateCurrentStep() {
    setState(() {
      _fieldErrors = {};
      _error = null;
    });

    switch (_step) {
      case 0:
        final err = RegisterWizardValidation.validateUniversity(_universityId);
        if (err != null) {
          setState(() => _error = err);
          return false;
        }
        return true;
      case 1:
        final err = RegisterWizardValidation.validateCampus(_campusId);
        if (err != null) {
          setState(() => _error = err);
          return false;
        }
        return true;
      case 2:
        final err = RegisterWizardValidation.validateRole(_role);
        if (err != null) {
          setState(() => _error = err);
          return false;
        }
        return true;
      case 3:
        final errors = RegisterWizardValidation.validatePersonal(
          name: _nameCtrl.text,
          email: _emailCtrl.text,
          password: _passwordCtrl.text,
          career: _careerCtrl.text,
          idNumber: _idCtrl.text,
          gender: _gender,
          profileImage: _profileImage,
        );
        if (errors.isNotEmpty) {
          setState(() => _fieldErrors = errors);
          return false;
        }
        return true;
      case 4:
        if (_role == 'driver') {
          final errors = RegisterWizardValidation.validateVehicle(
            makeModel: _makeCtrl.text,
            plate: _plateCtrl.text,
            color: _colorCtrl.text,
            seatsText: _seatsCtrl.text,
            image: _vehicleImage,
          );
          if (errors.isNotEmpty) {
            setState(() => _fieldErrors = errors);
            return false;
          }
        }
        return true;
      default:
        return true;
    }
  }

  void _next() {
    if (!_validateCurrentStep()) return;
    if (_step < _lastStep) {
      setState(() => _step++);
    } else {
      _submit();
    }
  }

  void _back() {
    if (_step == 0) {
      context.pop();
      return;
    }
    setState(() {
      _step--;
      _error = null;
      _fieldErrors = {};
    });
  }

  Future<void> _submit() async {
    if (!_validateCurrentStep()) return;
    setState(() {
      _submitting = true;
      _error = null;
    });

    final request = RegisterRequest(
      universityId: _universityId!,
      campusId: _campusId!,
      role: _role!,
      name: _nameCtrl.text.trim(),
      email: _emailCtrl.text.trim(),
      password: _passwordCtrl.text,
      career: _careerCtrl.text.trim(),
      idNumber: _idCtrl.text.trim(),
      gender: _gender!,
      profileImage: _profileImage!,
      vehicle: _role == 'driver'
          ? VehicleRegister(
              makeModel: _makeCtrl.text.trim(),
              plate: _plateCtrl.text.trim(),
              color: _colorCtrl.text.trim(),
              seatsTotal: int.parse(_seatsCtrl.text.trim()),
              image: _vehicleImage!,
            )
          : null,
    );

    try {
      await ref.read(authProvider.notifier).register(request);
      if (!mounted) return;
      context.go('/pending');
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error =
            e is ApiException ? e.message : 'No se pudo completar el registro.';
        _submitting = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Registro · $_stepTitle'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: _submitting ? null : _back,
        ),
      ),
      body: _loadingUnis
          ? const Center(child: CircularProgressIndicator())
          : SafeArea(
              child: Column(
                children: [
                  LinearProgressIndicator(
                    value: (_step + 1) / (_lastStep + 1),
                    backgroundColor: const Color(0xFFD0D7E6),
                    color: KubixColors.utnBlue,
                  ),
                  Expanded(
                    child: SingleChildScrollView(
                      padding: const EdgeInsets.all(24),
                      child: _buildStepBody(),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        if (_error != null) ...[
                          Text(
                            _error!,
                            style:
                                const TextStyle(color: KubixColors.emergency),
                          ),
                          const SizedBox(height: 12),
                        ],
                        FilledButton(
                          onPressed: _submitting ? null : _next,
                          child: _submitting
                              ? const SizedBox(
                                  height: 22,
                                  width: 22,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : Text(
                                  _step == _lastStep
                                      ? 'Enviar solicitud'
                                      : 'Continuar',
                                ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _buildStepBody() {
    switch (_step) {
      case 0:
        return _universityStep();
      case 1:
        return _campusStep();
      case 2:
        return _roleStep();
      case 3:
        return _personalStep();
      case 4:
        return _role == 'driver' ? _vehicleStep() : _confirmStep();
      case 5:
        return _confirmStep();
      default:
        return const SizedBox.shrink();
    }
  }

  Widget _universityStep() {
    if (_universities.isEmpty) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('No hay universidades disponibles.'),
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: _loadUniversities,
            child: const Text('Reintentar'),
          ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Elige tu universidad',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 16),
        ..._universities.map(
          (u) => RadioListTile<String>(
            value: u.id,
            groupValue: _universityId,
            title: Text(u.name),
            onChanged: (v) => setState(() {
              _universityId = v;
              _campusId = null;
            }),
          ),
        ),
      ],
    );
  }

  Widget _campusStep() {
    final campuses = _campuses;
    if (campuses.isEmpty) {
      return const Text('Esta universidad no tiene campuses configurados.');
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Elige tu campus',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 16),
        ...campuses.map(
          (c) => RadioListTile<String>(
            value: c.id,
            groupValue: _campusId,
            title: Text(c.name),
            onChanged: (v) => setState(() => _campusId = v),
          ),
        ),
      ],
    );
  }

  Widget _roleStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          '¿Cómo vas a usar Kubix?',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 16),
        RadioListTile<String>(
          value: 'passenger',
          groupValue: _role,
          title: const Text('Pasajero'),
          subtitle: const Text('Busco viajes hacia el campus'),
          onChanged: (v) => setState(() => _role = v),
        ),
        RadioListTile<String>(
          value: 'driver',
          groupValue: _role,
          title: const Text('Conductor'),
          subtitle: const Text('Publico rutas con mi vehículo'),
          onChanged: (v) => setState(() => _role = v),
        ),
      ],
    );
  }

  Widget _personalStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Tus datos',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _nameCtrl,
          decoration: InputDecoration(
            labelText: 'Nombre completo',
            errorText: _fieldErrors['name'],
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _emailCtrl,
          keyboardType: TextInputType.emailAddress,
          decoration: InputDecoration(
            labelText: 'Correo institucional',
            errorText: _fieldErrors['email'],
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _passwordCtrl,
          obscureText: true,
          decoration: InputDecoration(
            labelText: 'Contraseña',
            errorText: _fieldErrors['password'],
          ),
        ),
        const SizedBox(height: 12),
        LayoutBuilder(
          builder: (context, constraints) => DropdownMenu<String>(
            controller: _careerCtrl,
            width: constraints.maxWidth,
            enableFilter: true,
            enableSearch: true,
            requestFocusOnTap: true,
            label: const Text('Carrera'),
            helperText: 'Escribe para buscar',
            errorText: _fieldErrors['career'],
            dropdownMenuEntries: [
              for (final career in utnCareers)
                DropdownMenuEntry(value: career, label: career),
            ],
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _idCtrl,
          keyboardType: TextInputType.number,
          decoration: InputDecoration(
            labelText: 'Cédula / ID',
            errorText: _fieldErrors['idNumber'],
          ),
        ),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          key: ValueKey(_gender),
          initialValue: _gender,
          decoration: InputDecoration(
            labelText: 'Género',
            errorText: _fieldErrors['gender'],
          ),
          items: const [
            DropdownMenuItem(value: 'female', child: Text('Mujer')),
            DropdownMenuItem(value: 'male', child: Text('Hombre')),
          ],
          onChanged: (value) => setState(() => _gender = value),
        ),
        const SizedBox(height: 16),
        ImagePickerField(
          label: 'Subir imagen de perfil',
          value: _profileImage,
          errorText: _fieldErrors['profileImage'],
          onChanged: (value) => setState(() {
            _profileImage = value;
            _fieldErrors.remove('profileImage');
          }),
        ),
      ],
    );
  }

  Widget _vehicleStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Datos del vehículo',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _makeCtrl,
          decoration: InputDecoration(
            labelText: 'Marca y modelo',
            errorText: _fieldErrors['makeModel'],
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _plateCtrl,
          decoration: InputDecoration(
            labelText: 'Placa',
            errorText: _fieldErrors['plate'],
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _colorCtrl,
          decoration: InputDecoration(
            labelText: 'Color',
            errorText: _fieldErrors['color'],
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _seatsCtrl,
          keyboardType: TextInputType.number,
          decoration: InputDecoration(
            labelText: 'Asientos disponibles',
            errorText: _fieldErrors['seats'],
          ),
        ),
        const SizedBox(height: 16),
        ImagePickerField(
          label: 'Subir imagen del vehículo',
          value: _vehicleImage,
          errorText: _fieldErrors['image'],
          onChanged: (value) => setState(() {
            _vehicleImage = value;
            _fieldErrors.remove('image');
          }),
        ),
      ],
    );
  }

  Widget _confirmStep() {
    final uni = _selectedUniversity;
    CampusPublic? campus;
    for (final c in _campuses) {
      if (c.id == _campusId) {
        campus = c;
        break;
      }
    }
    final roleLabel = _role == 'driver' ? 'Conductor' : 'Pasajero';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Revisa tu solicitud',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 16),
        _kv('Universidad', uni?.name ?? '—'),
        _kv('Campus', campus?.name ?? '—'),
        _kv('Rol', roleLabel),
        _kv('Nombre', _nameCtrl.text.trim()),
        _kv('Correo', _emailCtrl.text.trim()),
        _kv('Carrera', _careerCtrl.text.trim()),
        _kv('Género', _gender == 'female' ? 'Mujer' : 'Hombre'),
        if (_role == 'driver') ...[
          _kv('Vehículo', _makeCtrl.text.trim()),
          _kv('Placa', _plateCtrl.text.trim()),
        ],
        const SizedBox(height: 12),
        const Text(
          'Un coordinador revisará tu solicitud. Te avisaremos cuando esté aprobada.',
          style: TextStyle(color: KubixColors.muted),
        ),
      ],
    );
  }

  Widget _kv(String k, String v) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              k,
              style: const TextStyle(
                color: KubixColors.muted,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Expanded(
            child: Text(v, style: const TextStyle(fontWeight: FontWeight.w600)),
          ),
        ],
      ),
    );
  }
}
