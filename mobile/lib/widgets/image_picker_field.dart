import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

class ImagePickerField extends StatefulWidget {
  const ImagePickerField({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
    this.errorText,
  });

  final String label;
  final String? value;
  final ValueChanged<String> onChanged;
  final String? errorText;

  @override
  State<ImagePickerField> createState() => _ImagePickerFieldState();
}

class _ImagePickerFieldState extends State<ImagePickerField> {
  static const _maxBytes = 2 * 1024 * 1024;
  bool _loading = false;
  String? _localError;

  Future<void> _pickImage() async {
    setState(() {
      _loading = true;
      _localError = null;
    });

    try {
      final file = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        maxWidth: 1280,
        maxHeight: 1280,
        imageQuality: 72,
      );
      if (file == null) return;

      final bytes = await file.readAsBytes();
      if (bytes.length > _maxBytes) {
        setState(() => _localError = 'La imagen debe pesar máximo 2 MB.');
        return;
      }

      final extension = file.name.split('.').last.toLowerCase();
      final mime = switch (extension) {
        'png' => 'image/png',
        'webp' => 'image/webp',
        _ => 'image/jpeg',
      };
      widget.onChanged('data:$mime;base64,${base64Encode(bytes)}');
    } catch (_) {
      setState(() => _localError = 'No se pudo leer la imagen.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final value = widget.value;
    final comma = value?.indexOf(',') ?? -1;
    final bytes = comma >= 0 ? base64Decode(value!.substring(comma + 1)) : null;
    final error = _localError ?? widget.errorText;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: _loading ? null : _pickImage,
          child: Ink(
            height: 132,
            width: double.infinity,
            decoration: BoxDecoration(
              border: Border.all(
                color: error == null
                    ? Theme.of(context).colorScheme.outline
                    : Theme.of(context).colorScheme.error,
              ),
              borderRadius: BorderRadius.circular(12),
            ),
            child: bytes == null
                ? Center(
                    child: _loading
                        ? const CircularProgressIndicator()
                        : Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(Icons.add_photo_alternate_outlined),
                              const SizedBox(height: 8),
                              Text(widget.label),
                              const Text('JPG, PNG o WebP · máximo 2 MB'),
                            ],
                          ),
                  )
                : ClipRRect(
                    borderRadius: BorderRadius.circular(11),
                    child: Image.memory(bytes, fit: BoxFit.cover),
                  ),
          ),
        ),
        if (bytes != null)
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: _loading ? null : _pickImage,
              icon: const Icon(Icons.edit_outlined),
              label: const Text('Cambiar imagen'),
            ),
          ),
        if (error != null)
          Padding(
            padding: const EdgeInsets.only(left: 12, top: 6),
            child: Text(
              error,
              style: TextStyle(
                color: Theme.of(context).colorScheme.error,
                fontSize: 12,
              ),
            ),
          ),
      ],
    );
  }
}
