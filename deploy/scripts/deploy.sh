#!/usr/bin/env bash
# Deploy Kubix UTN 2.0 to AWS (KBX-30).
# Prereqs: aws CLI v2, docker, jq, node/npm, .NET SDK (optional if image already built).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# Local deploys always use the Kubix account. In GitHub Actions, preserve the
# temporary credentials configured through OIDC/access-key secrets.
if [[ "${CI:-false}" == "true" ]]; then
  unset AWS_PROFILE
  PROFILE_LABEL="GitHub Actions credentials"
else
  unset AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY AWS_SESSION_TOKEN AWS_SECURITY_TOKEN AWS_PROFILE
  export AWS_PROFILE="${KUBIX_AWS_PROFILE:-kx-dev}"
  PROFILE_LABEL="$AWS_PROFILE"
fi
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

echo "==> Profile=$PROFILE_LABEL Region=$REGION Stack=$STACK Tag=$IMAGE_TAG"
if ! aws sts get-caller-identity --region "$REGION" >/dev/null 2>&1; then
  echo "AWS credentials are unavailable or expired ($PROFILE_LABEL)."
  if [[ "${CI:-false}" != "true" ]]; then
    echo "Run:  aws login --profile $AWS_PROFILE"
  fi
  exit 1
fi
aws sts get-caller-identity --region "$REGION" --output table || true

# Resolve default VPC + subnets for RDS
VPC_ID="${VPC_ID:-$(aws ec2 describe-vpcs --region "$REGION" \
  --filters Name=isDefault,Values=true \
  --query 'Vpcs[0].VpcId' --output text)}"
if [[ -z "$VPC_ID" || "$VPC_ID" == "None" ]]; then
  echo "No default VPC in $REGION. Set VPC_ID and SUBNET_IDS."
  exit 1
fi
SUBNET_IDS="${SUBNET_IDS:-$(aws ec2 describe-subnets --region "$REGION" \
  --filters Name=vpc-id,Values="$VPC_ID" \
  --query 'Subnets[].SubnetId' --output text | tr '\t' ',')}"
if [[ -z "$SUBNET_IDS" || "$SUBNET_IDS" == "None" ]]; then
  echo "No subnets in VPC $VPC_ID. Set SUBNET_IDS=subnet-a,subnet-b"
  exit 1
fi
echo "==> VPC=$VPC_ID Subnets=$SUBNET_IDS"

# ROLLBACK_COMPLETE blocks redeploy — delete first
STACK_STATUS="$(aws cloudformation describe-stacks --region "$REGION" --stack-name "$STACK" \
  --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo 'NONE')"
if [[ "$STACK_STATUS" == "ROLLBACK_COMPLETE" || "$STACK_STATUS" == "ROLLBACK_FAILED" ]]; then
  echo "==> Deleting failed stack $STACK ($STACK_STATUS)"
  aws cloudformation delete-stack --region "$REGION" --stack-name "$STACK"
  aws cloudformation wait stack-delete-complete --region "$REGION" --stack-name "$STACK"
fi

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
    AllowedCidr="$ALLOWED_CIDR" \
    VpcId="$VPC_ID" \
    "SubnetIds=$SUBNET_IDS"


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
ECS_CLUSTER="$(cfn_out EcsClusterName)"
ECS_EXEC_ROLE="$(cfn_out EcsExecutionRoleArn)"
ECS_TASK_ROLE="$(cfn_out EcsTaskRoleArn)"
TG_ARN="$(cfn_out ApiTargetGroupArn)"
TASK_SG="$(cfn_out TaskSecurityGroupId)"
ALB_DNS="$(cfn_out AlbDnsName)"
LOG_GROUP="$(cfn_out ApiLogGroupName)"
SUBNETS_CSV="$(cfn_out SubnetIdsCsv)"

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
ECR_REGISTRY="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com"

if [[ "${SKIP_IMAGE_BUILD:-0}" != "1" ]]; then
  echo "==> ECR login + build/push API image"
  aws ecr get-login-password --region "$REGION" \
    | docker login --username AWS --password-stdin "$ECR_REGISTRY"

  docker build \
    --platform linux/amd64 \
    --provenance=false \
    -f "$ROOT/backend/src/Kubix.Api/Dockerfile" \
    -t "${ECR_URI}:${IMAGE_TAG}" \
    -t "${ECR_URI}:latest" \
    "$ROOT"

  docker push "${ECR_URI}:${IMAGE_TAG}"
  docker push "${ECR_URI}:latest"
else
  echo "==> SKIP_IMAGE_BUILD=1 — usando imagen ya en ECR ($ECR_URI:$IMAGE_TAG / latest)"
fi

CONN="Host=${RDS_HOST};Port=${RDS_PORT};Database=kubix;Username=${DB_USERNAME};Password=${DB_PASSWORD}"
SERVICE_NAME="${PROJECT}-${ENV_NAME}-api"
TASK_FAMILY="${PROJECT}-${ENV_NAME}-api"

# Prefer App Runner when the account has it; otherwise ECS Fargate + ALB.
API_RUNTIME="${KUBIX_API_RUNTIME:-auto}"
if [[ "$API_RUNTIME" == "auto" ]]; then
  if aws apprunner list-services --region "$REGION" >/dev/null 2>&1; then
    API_RUNTIME=apprunner
  else
    API_RUNTIME=ecs
    echo "==> App Runner no disponible en esta cuenta → usando ECS Fargate + ALB"
  fi
fi

if [[ "$API_RUNTIME" == "apprunner" ]]; then
  APPRUNNER_ACCESS_ROLE="$(cfn_out AppRunnerAccessRoleArn)"
  APPRUNNER_INSTANCE_ROLE="$(cfn_out AppRunnerInstanceRoleArn)"
  echo "==> App Runner service ($SERVICE_NAME)"
  EXISTING="$(aws apprunner list-services --region "$REGION" \
    --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
    --output text)"

  ENV_MAP=$(jq -n \
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
    }')

  if [[ -z "$EXISTING" || "$EXISTING" == "None" ]]; then
    aws apprunner create-service --region "$REGION" --cli-input-json "$(jq -n \
      --arg name "$SERVICE_NAME" \
      --arg image "$ECR_URI:$IMAGE_TAG" \
      --arg accessRole "$APPRUNNER_ACCESS_ROLE" \
      --arg instanceRole "$APPRUNNER_INSTANCE_ROLE" \
      --argjson env "$ENV_MAP" \
      '{
        ServiceName: $name,
        SourceConfiguration: {
          AuthenticationConfiguration: { AccessRoleArn: $accessRole },
          AutoDeploymentsEnabled: false,
          ImageRepository: {
            ImageIdentifier: $image,
            ImageRepositoryType: "ECR",
            ImageConfiguration: { Port: "8080", RuntimeEnvironmentVariables: $env }
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
      --argjson env "$ENV_MAP" \
      '{
        ServiceArn: $arn,
        SourceConfiguration: {
          AuthenticationConfiguration: { AccessRoleArn: $accessRole },
          AutoDeploymentsEnabled: false,
          ImageRepository: {
            ImageIdentifier: $image,
            ImageRepositoryType: "ECR",
            ImageConfiguration: { Port: "8080", RuntimeEnvironmentVariables: $env }
          }
        }
      }')" >/dev/null
    echo "Updated App Runner service $SERVICE_NAME"
  fi
  sleep 5
  SERVICE_ARN="$(aws apprunner list-services --region "$REGION" \
    --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
    --output text)"
  API_URL="https://$(aws apprunner describe-service --region "$REGION" --service-arn "$SERVICE_ARN" \
    --query 'Service.ServiceUrl' --output text)"
else
  echo "==> ECS Fargate service ($SERVICE_NAME) on cluster $ECS_CLUSTER"
  # Pick 2 subnets for awsvpc
  SUBNET_A="$(echo "$SUBNETS_CSV" | cut -d, -f1)"
  SUBNET_B="$(echo "$SUBNETS_CSV" | cut -d, -f2)"

  TASK_DEF=$(jq -n \
    --arg family "$TASK_FAMILY" \
    --arg execRole "$ECS_EXEC_ROLE" \
    --arg taskRole "$ECS_TASK_ROLE" \
    --arg image "$ECR_URI:$IMAGE_TAG" \
    --arg logGroup "$LOG_GROUP" \
    --arg region "$REGION" \
    --arg conn "$CONN" \
    --arg cors "$CF_URL" \
    --arg email "$SUPER_ADMIN_EMAIL" \
    --arg pass "$SUPER_ADMIN_PASSWORD" \
    --arg maps "$GOOGLE_MAPS_API_KEY" \
    '{
      family: $family,
      networkMode: "awsvpc",
      requiresCompatibilities: ["FARGATE"],
      cpu: "256",
      memory: "512",
      executionRoleArn: $execRole,
      taskRoleArn: $taskRole,
      containerDefinitions: [{
        name: "api",
        image: $image,
        essential: true,
        portMappings: [{ containerPort: 8080, protocol: "tcp" }],
        environment: [
          { name: "ConnectionStrings__Default", value: $conn },
          { name: "Cors__WebOrigin", value: $cors },
          { name: "Cors__AllowedOrigins__0", value: $cors },
          { name: "Database__MigrateOnStartup", value: "true" },
          { name: "Database__SeedOnStartup", value: "true" },
          { name: "SUPER_ADMIN_EMAIL", value: $email },
          { name: "SUPER_ADMIN_PASSWORD", value: $pass },
          { name: "GoogleMaps__ApiKey", value: $maps },
          { name: "ASPNETCORE_ENVIRONMENT", value: "Production" },
          { name: "Swagger__Enabled", value: "false" },
          { name: "ASPNETCORE_URLS", value: "http://+:8080" }
        ],
        logConfiguration: {
          logDriver: "awslogs",
          options: {
            "awslogs-group": $logGroup,
            "awslogs-region": $region,
            "awslogs-stream-prefix": "api"
          }
        }
      }]
    }')

  TASK_ARN="$(aws ecs register-task-definition --region "$REGION" --cli-input-json "$TASK_DEF" \
    --query 'taskDefinition.taskDefinitionArn' --output text)"
  echo "Registered task definition $TASK_ARN"

  EXISTS="$(aws ecs describe-services --region "$REGION" --cluster "$ECS_CLUSTER" --services "$SERVICE_NAME" \
    --query 'services[?status==`ACTIVE`].serviceName' --output text 2>/dev/null || true)"

  if [[ -z "$EXISTS" || "$EXISTS" == "None" ]]; then
    aws ecs create-service --region "$REGION" \
      --cluster "$ECS_CLUSTER" \
      --service-name "$SERVICE_NAME" \
      --task-definition "$TASK_ARN" \
      --desired-count 1 \
      --launch-type FARGATE \
      --network-configuration "awsvpcConfiguration={subnets=[$SUBNET_A,$SUBNET_B],securityGroups=[$TASK_SG],assignPublicIp=ENABLED}" \
      --load-balancers "targetGroupArn=$TG_ARN,containerName=api,containerPort=8080" \
      --health-check-grace-period-seconds 120 >/dev/null
    echo "Created ECS service $SERVICE_NAME"
  else
    aws ecs update-service --region "$REGION" \
      --cluster "$ECS_CLUSTER" \
      --service "$SERVICE_NAME" \
      --task-definition "$TASK_ARN" \
      --force-new-deployment >/dev/null
    echo "Updated ECS service $SERVICE_NAME"
  fi

  echo "==> Esperando ECS service stable (puede tardar unos minutos)..."
  aws ecs wait services-stable --region "$REGION" --cluster "$ECS_CLUSTER" --services "$SERVICE_NAME"
  # Same-origin via CloudFront /api/* → ALB (evita mixed content HTTPS→HTTP)
  API_URL="$CF_URL"
fi

echo "==> Build + sync web (VITE_API_URL=$API_URL)"
(
  cd "$ROOT/web"
  npm ci
  VITE_API_URL="$API_URL" npm run build
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
API_RUNTIME=$API_RUNTIME
ECS_CLUSTER=$ECS_CLUSTER
EOF

echo ""
echo "Deploy listo (runtime=$API_RUNTIME)."
echo "  API: $API_URL"
echo "  Web: $CF_URL"
echo "  Outputs: $OUT_DIR/urls.env"
echo ""
echo "Importante: ejecuta deploy/scripts/teardown.sh al terminar las pruebas."
