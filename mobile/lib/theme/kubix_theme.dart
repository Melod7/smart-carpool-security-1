import 'package:flutter/material.dart';

/// Paleta Kubix UTN (design tokens mobile).
abstract final class KubixColors {
  static const Color utnBlue = Color(0xFF003087);
  static const Color background = Color(0xFFF0F4FA);
  static const Color emergency = Color(0xFFC8102E);
  static const Color eco = Color(0xFF2E7D32);
  static const Color gold = Color(0xFFF9A825);
  static const Color muted = Color(0xFF5A6A8A);
  static const Color surface = Colors.white;
}

ThemeData buildKubixTheme() {
  const seed = KubixColors.utnBlue;
  final scheme = ColorScheme.fromSeed(
    seedColor: seed,
    primary: seed,
    surface: KubixColors.surface,
    error: KubixColors.emergency,
  );

  // Tipografía limpia estilo Inter (Material 3 sans, sin dependencia extra).
  final textTheme = Typography.material2021(platform: TargetPlatform.android)
      .black
      .apply(
        bodyColor: const Color(0xFF1A2438),
        displayColor: KubixColors.utnBlue,
        fontFamily: 'Roboto',
      );

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: KubixColors.background,
    textTheme: textTheme,
    appBarTheme: const AppBarTheme(
      backgroundColor: KubixColors.utnBlue,
      foregroundColor: Colors.white,
      elevation: 0,
      centerTitle: false,
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: Color(0xFFD0D7E6)),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: KubixColors.utnBlue, width: 1.5),
      ),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: KubixColors.utnBlue,
        foregroundColor: Colors.white,
        minimumSize: const Size.fromHeight(48),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: KubixColors.utnBlue,
        minimumSize: const Size.fromHeight(48),
        side: const BorderSide(color: KubixColors.utnBlue),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: Colors.white,
      indicatorColor: KubixColors.utnBlue.withValues(alpha: 0.12),
      labelTextStyle: WidgetStateProperty.resolveWith((states) {
        final selected = states.contains(WidgetState.selected);
        return TextStyle(
          fontSize: 12,
          fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
          color: selected ? KubixColors.utnBlue : KubixColors.muted,
        );
      }),
    ),
  );
}
