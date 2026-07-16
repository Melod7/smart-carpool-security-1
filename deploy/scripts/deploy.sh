#!/usr/bin/env bash
# Deploy Kubix UTN 2.0 to AWS (KBX-30).
# Prereqs: aws CLI v2, docker, jq, node/npm, .NET SDK (optional if image already built).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
REGION="${AWS_REGION:-us-east-1}"
STACK="${STACK_NAME:-kubix-test}"
PROJECT="${PROJECT_NAME:-kubix}"
ENV_NAME="${ENVIRONMENT_NAME:-test}"
IMAGE_TAG="${IMAGE_TAG:-$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo latest)}"

DB_USERNAME="${DB_USERNAME:-kubix}"
DB_PASSWORD="${DB_PASSWORD:?Set DB_PASSWORD (min 8 chars)}"
SUPER_ADMIN_EMAIL="${SUPER_ADMIN_EMAIL:-superadmin@kubix.local}"
SUPER_ADMIN_PASSWORD="${SUPER_ADMIN_PASSWORD:-ChangeMe123!}"
GOOGLE_MAPS_API_KEY="${GOOGLE_MAPS_API_KEY:-}"
ALLOWED_CIDR="${ALLOWED_CIDR:-0.0.0.0/0}"

echo "==> Region=$REGION Stack=$STACK Tag=$IMAGE_TAG"

echo "==> CloudFormation stack"
aws cloudformation deploy \
  --region "$REGION" \
  --stack-name "$STACK" \
  --template-file "$ROOT/deploy/aws/cloudformation.yml" \
  --capabilities CAPABILITY_NAMED_IAM \
  --parameter-overrides \
    ProjectName="$PROJECT" \
    EnvironmentName="$ENV_NAME" \
    DbUsername="$DB_USERNAME" \
    DbPassword="$DB_PASSWORD" \
    AllowedCidr="$ALLOWED_CIDR"

cfn_out() {
  aws cloudformation describe-stacks \
    --region "$REGION" \
    --stack-name "$STACK" \
    --query "Stacks[0].Outputs[?OutputKey=='$1'].OutputValue" \
    --output text
}

ECR_URI="$(cfn_out EcrRepositoryUri)"
BUCKET="$(cfn_out WebBucketName)"
CF_ID="$(cfn_out CloudFrontDistributionId)"
CF_URL="$(cfn_out CloudFrontUrl)"
RDS_HOST="$(cfn_out RdsEndpoint)"
RDS_PORT="$(cfn_out RdsPort)"
APPRUNNER_ACCESS_ROLE="$(cfn_out AppRunnerAccessRoleArn)"
APPRUNNER_INSTANCE_ROLE="$(cfn_out AppRunnerInstanceRoleArn)"

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
ECR_REGISTRY="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com"

echo "==> ECR login + build/push API image"
aws ecr get-login-password --region "$REGION" \
  | docker login --username AWS --password-stdin "$ECR_REGISTRY"

docker build \
  -f "$ROOT/backend/src/Kubix.Api/Dockerfile" \
  -t "$ECR_URI:$IMAGE_TAG" \
  -t "$ECR_URI:latest" \
  "$ROOT"

docker push "$ECR_URI:$IMAGE_TAG"
docker push "$ECR_URI:latest"

CONN="Host=${RDS_HOST};Port=${RDS_PORT};Database=kubix;Username=${DB_USERNAME};Password=${DB_PASSWORD}"
SERVICE_NAME="${PROJECT}-${ENV_NAME}-api"

echo "==> App Runner service ($SERVICE_NAME)"
EXISTING="$(aws apprunner list-services --region "$REGION" \
  --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
  --output text)"

ENV_JSON=$(jq -n \
  --arg conn "$CONN" \
  --arg cors "$CF_URL" \
  --arg email "$SUPER_ADMIN_EMAIL" \
  --arg pass "$SUPER_ADMIN_PASSWORD" \
  --arg maps "$GOOGLE_MAPS_API_KEY" \
  '{
    ConnectionStrings__Default: $conn,
    Cors__WebOrigin: $cors,
    Cors__AllowedOrigins__0: $cors,
    Database__MigrateOnStartup: "true",
    Database__SeedOnStartup: "true",
    SUPER_ADMIN_EMAIL: $email,
    SUPER_ADMIN_PASSWORD: $pass,
    GoogleMaps__ApiKey: $maps,
    ASPNETCORE_ENVIRONMENT: "Production",
    Swagger__Enabled: "false"
  } | to_entries | map({Name: .key, Value: .value})')

if [[ -z "$EXISTING" || "$EXISTING" == "None" ]]; then
  aws apprunner create-service --region "$REGION" --cli-input-json "$(jq -n \
    --arg name "$SERVICE_NAME" \
    --arg image "$ECR_URI:$IMAGE_TAG" \
    --arg accessRole "$APPRUNNER_ACCESS_ROLE" \
    --arg instanceRole "$APPRUNNER_INSTANCE_ROLE" \
    --argjson env "$ENV_JSON" \
    '{
      ServiceName: $name,
      SourceConfiguration: {
        AuthenticationConfiguration: { AccessRoleArn: $accessRole },
        AutoDeploymentsEnabled: false,
        ImageRepository: {
          ImageIdentifier: $image,
          ImageRepositoryType: "ECR",
          ImageConfiguration: {
            Port: "8080",
            RuntimeEnvironmentVariables: ($env | map({(.Name): .Value}) | add)
          }
        }
      },
      InstanceConfiguration: {
        Cpu: "0.25 vCPU",
        Memory: "0.5 GB",
        InstanceRoleArn: $instanceRole
      },
      HealthCheckConfiguration: {
        Protocol: "TCP",
        Interval: 10,
        Timeout: 5,
        HealthyThreshold: 1,
        UnhealthyThreshold: 5
      }
    }')" >/dev/null
  echo "Created App Runner service $SERVICE_NAME"
else
  aws apprunner update-service --region "$REGION" --cli-input-json "$(jq -n \
    --arg arn "$EXISTING" \
    --arg image "$ECR_URI:$IMAGE_TAG" \
    --arg accessRole "$APPRUNNER_ACCESS_ROLE" \
    --argjson env "$ENV_JSON" \
    '{
      ServiceArn: $arn,
      SourceConfiguration: {
        AuthenticationConfiguration: { AccessRoleArn: $accessRole },
        AutoDeploymentsEnabled: false,
        ImageRepository: {
          ImageIdentifier: $image,
          ImageRepositoryType: "ECR",
          ImageConfiguration: {
            Port: "8080",
            RuntimeEnvironmentVariables: ($env | map({(.Name): .Value}) | add)
          }
        }
      }
    }')" >/dev/null
  echo "Updated App Runner service $SERVICE_NAME"
fi

# Wait briefly for service URL
sleep 5
SERVICE_ARN="$(aws apprunner list-services --region "$REGION" \
  --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
  --output text)"
API_URL="$(aws apprunner describe-service --region "$REGION" --service-arn "$SERVICE_ARN" \
  --query 'Service.ServiceUrl' --output text)"
API_URL="https://${API_URL}"

echo "==> Build + sync web (VITE_API_BASE_URL=$API_URL)"
(
  cd "$ROOT/web"
  npm ci
  VITE_API_BASE_URL="$API_URL" npm run build
)
aws s3 sync "$ROOT/web/dist/" "s3://${BUCKET}/" --delete --region "$REGION"
aws cloudfront create-invalidation --region "$REGION" \
  --distribution-id "$CF_ID" \
  --paths "/*" >/dev/null

# Persist outputs for CI / smoke
OUT_DIR="$ROOT/deploy/.out"
mkdir -p "$OUT_DIR"
cat > "$OUT_DIR/urls.env" <<EOF
API_URL=$API_URL
WEB_URL=$CF_URL
CLOUDFRONT_DISTRIBUTION_ID=$CF_ID
ECR_URI=$ECR_URI
RDS_HOST=$RDS_HOST
STACK_NAME=$STACK
AWS_REGION=$REGION
SERVICE_NAME=$SERVICE_NAME
EOF

echo ""
echo "Deploy listo."
echo "  API: $API_URL"
echo "  Web: $CF_URL"
echo "  Outputs: $OUT_DIR/urls.env"
echo ""
echo "Importante: ejecuta deploy/scripts/teardown.sh al terminar las pruebas."
