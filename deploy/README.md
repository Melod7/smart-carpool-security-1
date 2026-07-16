# Deploy — Kubix UTN 2.0 (KBX-30)

Infra de **prueba** en AWS: API en **App Runner** (imagen ECR), **RDS Postgres `db.t4g.micro`**, admin web en **S3 + CloudFront** (routing SPA). Migraciones EF Core al arranque (`Database__MigrateOnStartup=true`).

> Cuenta de prueba: **siempre** ejecuta el teardown al terminar. RDS + App Runner generan cargo aunque no haya tráfico.

## Prerrequisitos

- AWS CLI v2 autenticado (`aws sts get-caller-identity`)
- Docker
- `jq`, Node 20+, npm
- Permisos: CloudFormation, ECR, S3, CloudFront, RDS, EC2 (SG), IAM, App Runner

Variables mínimas:

```bash
export AWS_REGION=us-east-1
export DB_PASSWORD='TuPasswordSeguro1'
# opcionales
export SUPER_ADMIN_EMAIL=superadmin@kubix.local
export SUPER_ADMIN_PASSWORD='ChangeMe123!'
export GOOGLE_MAPS_API_KEY=''
export STACK_NAME=kubix-test
```

## Deploy

```bash
chmod +x deploy/scripts/*.sh
./deploy/scripts/deploy.sh
```

El script:

1. Despliega `deploy/aws/cloudformation.yml` (ECR, S3, CloudFront SPA, RDS, roles App Runner)
2. Build/push de la imagen API a ECR
3. Crea o actualiza el servicio App Runner (env: connection string, CORS = URL CloudFront, seed/migrate)
4. Build del web con `VITE_API_BASE_URL` apuntando a App Runner
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

Workflow: `.github/workflows/deploy.yml`

- Disparo: **manual** (`workflow_dispatch`) para no gastar AWS en cada push
- Opcional: push a `main` si existen secrets AWS
- Secrets recomendados (OIDC o access keys):

| Secret | Uso |
|--------|-----|
| `AWS_ROLE_ARN` | Rol IAM con trust a GitHub OIDC (preferido) |
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | Alternativa a OIDC |
| `DB_PASSWORD` | Master RDS |
| `SUPER_ADMIN_PASSWORD` | Seed super-admin |
| `GOOGLE_MAPS_API_KEY` | Opcional |

Variables de repo opcionales: `AWS_REGION`, `STACK_NAME`.

Tras un deploy exitoso, el job publica el artefacto `deploy-urls` con `urls.env`. Job opcional de APK Flutter como artefacto (no Play Store).

## Arquitectura

```
GitHub Actions / deploy.sh
        │
        ├─► ECR ──► App Runner (API :8080) ──► RDS Postgres t4g.micro
        │
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
