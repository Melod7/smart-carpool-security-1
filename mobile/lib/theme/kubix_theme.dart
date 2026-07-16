import 'package:flutter/material.dart';

/// Paleta Kubix UTN 2.0
abstract final class KubixColors {
  static const Color text = Color(0xFF050315);
  static const Color background = Color(0xFFFBFBFE);
  static const Color primary = Color(0xFFCE2727);
  static const Color secondary = Color(0xFFE9B3B3);
  static const Color accent = Color(0xFF181212);

  /// Alias legacy → nueva paleta (evita romper imports existentes).
  static const Color utnBlue = primary;
  static const Color emergency = primary;
  static const Color eco = Color(0xFF2E7D32);
  static const Color gold = Color(0xFFF9A825);
  static const Color muted = Color(0xFF6B6570);
  static const Color surface = Colors.white;
}

ThemeData buildKubixTheme() {
  const seed = KubixColors.primary;
  final scheme = ColorScheme.fromSeed(
    seedColor: seed,
    primary: seed,
    secondary: KubixColors.secondary,
    surface: KubixColors.surface,
    error: KubixColors.primary,
    onPrimary: Colors.white,
    onSurface: KubixColors.text,
  );

  final textTheme = Typography.material2021(platform: TargetPlatform.android)
      .black
      .apply(
        bodyColor: KubixColors.text,
        displayColor: KubixColors.accent,
        fontFamily: 'Roboto',
      );

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: KubixColors.background,
    textTheme: textTheme,
    appBarTheme: const AppBarTheme(
      backgroundColor: KubixColors.primary,
      foregroundColor: Colors.white,
      elevation: 0,
      centerTitle: false,
    ),
    floatingActionButtonTheme: const FloatingActionButtonThemeData(
      backgroundColor: KubixColors.primary,
      foregroundColor: Colors.white,
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: KubixColors.secondary),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: KubixColors.primary, width: 1.5),
      ),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: KubixColors.primary,
        foregroundColor: Colors.white,
        minimumSize: const Size.fromHeight(48),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: KubixColors.primary,
        minimumSize: const Size.fromHeight(48),
        side: const BorderSide(color: KubixColors.primary),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: Colors.white,
      indicatorColor: KubixColors.primary.withValues(alpha: 0.12),
      labelTextStyle: WidgetStateProperty.resolveWith((states) {
        final selected = states.contains(WidgetState.selected);
        return TextStyle(
          fontSize: 12,
          fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
          color: selected ? KubixColors.primary : KubixColors.muted,
        );
      }),
    ),
  );
}
