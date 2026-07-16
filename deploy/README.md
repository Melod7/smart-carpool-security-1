# Deploy — Kubix UTN 2.0 (KBX-30)

Infra de **prueba** en AWS: API en **ECS Fargate + ALB** (fallback automático si App Runner no está habilitado en la cuenta), imagen en **ECR**, **RDS Postgres `db.t4g.micro`**, admin web en **S3 + CloudFront** (routing SPA). Migraciones EF Core al arranque (`Database__MigrateOnStartup=true`).

> En Mac Apple Silicon el build usa `--platform linux/amd64` (ECS/Fargate no acepta arm64).
> Si el deploy “se cuelga” en `services-stable`, revisa eventos ECS: el error típico es imagen arm64.

> Cuenta de prueba: **siempre** ejecuta el teardown al terminar. RDS + App Runner generan cargo aunque no haya tráfico.

## Prerrequisitos

- AWS CLI v2 con profile **`kx-dev`** (cuenta Kubix). Los scripts **ignoran** `AWS_PROFILE` del shell (p. ej. perfiles WP) y fuerzan `kx-dev`.
- Docker, `jq`, Node 20+, npm
- Permisos: CloudFormation, ECR, S3, CloudFront, RDS, EC2 (SG), IAM, App Runner

Login (console credentials, no SSO WP):

```bash
aws login --profile kx-dev
aws sts get-caller-identity --profile kx-dev
# debe mostrar Account 900103507605
```

Variables mínimas:

```bash
export AWS_REGION=us-east-1
export DB_PASSWORD='TuPasswordSeguro1'
# Gmail SMTP opcional (usa una contraseña de aplicación, no tu contraseña normal):
# export SMTP_USERNAME='notificaciones@utn.edu.ec'
# export SMTP_PASSWORD='xxxx xxxx xxxx xxxx'
# export SMTP_FROM_EMAIL="$SMTP_USERNAME"
# override opcional del profile Kubix (default kx-dev):
# export KUBIX_AWS_PROFILE=kx-dev
```

## Deploy

```bash
chmod +x deploy/scripts/*.sh
aws login --profile kx-dev   # si la sesión expiró
export DB_PASSWORD='TuPasswordSeguro1'
# Reanudar sin rebuild de imagen (ya está en ECR):
# export SKIP_IMAGE_BUILD=1
./deploy/scripts/deploy.sh
```

El script:

1. Despliega `deploy/aws/cloudformation.yml` (ECR, S3, CloudFront SPA, RDS, ALB/ECS roles)
2. Build/push de la imagen API a ECR
3. Crea/actualiza la API: **App Runner** si la cuenta lo permite; si no, **ECS Fargate + ALB**
4. Build del web con `VITE_API_URL` apuntando a CloudFront (proxy `/api/*` → ALB)
5. Sync a S3 + invalidación CloudFront
6. Escribe URLs en `deploy/.out/urls.env`

Smoke:

```bash
source deploy/.out/urls.env
./deploy/scripts/smoke.sh
```

## Teardown (obligatorio)

```bash
./deploy/scripts/teardown.sh
```

Elimina App Runner, vacía S3/ECR y borra el stack CloudFormation (RDS, distribución, roles). Verifica en la consola que no queden recursos `kubix-test-*`.

## CI/CD (GitHub Actions)

Workflows:

- `.github/workflows/deploy.yml`: infraestructura + web/API; al finalizar llama al build móvil.
- `.github/workflows/mobile-builds.yml`: Android/iOS independiente, ejecutable manualmente sin credenciales AWS.

- Disparo: **manual** (`workflow_dispatch`) para no gastar AWS en cada push
- Opcional: push a `main` si existen secrets AWS
- Secrets recomendados (OIDC o access keys):

| Secret | Uso |
|--------|-----|
| `AWS_ROLE_ARN` | Rol IAM con trust a GitHub OIDC (preferido) |
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | Alternativa a OIDC |
| `DB_PASSWORD` | Master RDS |
| `SUPER_ADMIN_PASSWORD` | Seed super-admin |
| `GOOGLE_MAPS_API_KEY` | Maps en Android/iOS |
| `SMTP_USERNAME` / `SMTP_PASSWORD` | Gmail SMTP; requiere contraseña de aplicación |
| `ANDROID_KEYSTORE_BASE64` | Keystore estable para firmar el APK |
| `ANDROID_KEYSTORE_PASSWORD` | Password del keystore |
| `ANDROID_KEY_ALIAS` / `ANDROID_KEY_PASSWORD` | Alias y password de la clave |

Variables de repo opcionales: `AWS_REGION`, `STACK_NAME`, `SMTP_FROM_EMAIL`, `SMTP_FROM_NAME`.

El correo se habilita automáticamente cuando existen ambos secrets SMTP. La aprobación o
denegación nunca se revierte si Gmail no está disponible; el fallo queda registrado en logs.

Tras un deploy manual exitoso, Actions usa la URL CloudFront resultante y publica:

- `deploy-urls`: URLs del entorno.
- `kubix-android-apk`: APK Android con firma estable, conectado a la API desplegada.
- `kubix-ios-simulator-app`: aplicación ejecutable en iOS Simulator.

Si ya existía una compilación Android firmada con otra clave, hay que
desinstalarla una vez antes de instalar el nuevo APK. Los próximos artefactos
de Actions sí podrán actualizarse entre ellos.

El ZIP iOS se instala en un Simulator arrancado con:

```bash
xcrun simctl install booted Runner.app
```

Un IPA instalable en un iPhone físico requiere certificado Apple Distribution,
provisioning profile y el UDID/distribución TestFlight; no puede generarse a
partir de una aplicación sin firma.

Para regenerar solamente las apps: **Actions → Mobile artifacts → Run
workflow**. El input `api_url` debe ser la URL CloudFront del entorno.

## Arquitectura

```
GitHub Actions / deploy.sh
        │
        ├─► ECR ──► ECS Fargate (+ ALB) ──► RDS Postgres t4g.micro
        │         (o App Runner si la cuenta lo permite)
        └─► web/dist ──► S3 ──► CloudFront (403/404 → index.html)
```

CORS de la API se configura con el dominio CloudFront en el deploy.

## Costes / seguridad (cuenta de prueba)

- RDS `PubliclyAccessible=true` y SG con CIDR configurable (`ALLOWED_CIDR`, default `0.0.0.0/0`) — **solo para demo**. En producción: VPC privada + connector App Runner.
- `BackupRetentionPeriod=0` y `DeletionProtection=false` para facilitar teardown.
- No subas `deploy/.out/` ni passwords al repo.

## Checklist QA (ticket)

- [ ] Deploy fresco desde cuenta limpia siguiendo este README
- [ ] Smoke / login web vía CloudFront
- [ ] Teardown sin recursos facturables residuales
