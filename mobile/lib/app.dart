import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'router/app_router.dart';
import 'theme/kubix_theme.dart';

class KubixApp extends ConsumerWidget {
  const KubixApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(appRouterProvider);

    return MaterialApp.router(
      title: 'kubix 2.0',
      debugShowCheckedModeBanner: false,
      theme: buildKubixTheme(),
      routerConfig: router,
    );
  }
}
