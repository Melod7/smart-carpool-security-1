#!/usr/bin/env bash
# Tear down Kubix AWS test resources (KBX-30) — leave no billable leftovers.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
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
SERVICE_NAME="${SERVICE_NAME:-${PROJECT}-${ENV_NAME}-api}"

echo "==> Teardown profile=$PROFILE_LABEL region=$REGION stack=$STACK service=$SERVICE_NAME"
if ! aws sts get-caller-identity --region "$REGION" >/dev/null 2>&1; then
  echo "AWS credentials are unavailable or expired ($PROFILE_LABEL)."
  if [[ "${CI:-false}" != "true" ]]; then
    echo "Run:  aws login --profile $AWS_PROFILE"
  fi
  exit 1
fi
aws sts get-caller-identity --region "$REGION" --output table || true

# 1) ECS service (must delete before cluster/ALB in stack)
ECS_CLUSTER="$(aws cloudformation describe-stacks --region "$REGION" --stack-name "$STACK" \
  --query "Stacks[0].Outputs[?OutputKey=='EcsClusterName'].OutputValue" --output text 2>/dev/null || true)"
if [[ -n "${ECS_CLUSTER:-}" && "$ECS_CLUSTER" != "None" ]]; then
  if aws ecs describe-services --region "$REGION" --cluster "$ECS_CLUSTER" --services "$SERVICE_NAME" \
    --query 'services[?status==`ACTIVE`].serviceName' --output text 2>/dev/null | grep -q "$SERVICE_NAME"; then
    echo "Deleting ECS service $SERVICE_NAME"
    aws ecs update-service --region "$REGION" --cluster "$ECS_CLUSTER" --service "$SERVICE_NAME" \
      --desired-count 0 >/dev/null || true
    aws ecs delete-service --region "$REGION" --cluster "$ECS_CLUSTER" --service "$SERVICE_NAME" --force >/dev/null || true
    echo "Waiting for ECS service drain..."
    sleep 20
  fi
fi

# 2) App Runner (si existiera)
SERVICE_ARN="$(aws apprunner list-services --region "$REGION" \
  --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
  --output text 2>/dev/null || true)"
if [[ -n "${SERVICE_ARN:-}" && "$SERVICE_ARN" != "None" ]]; then
  echo "Deleting App Runner $SERVICE_NAME"
  aws apprunner delete-service --region "$REGION" --service-arn "$SERVICE_ARN" >/dev/null
  for _ in $(seq 1 60); do
    STILL="$(aws apprunner list-services --region "$REGION" \
      --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
      --output text 2>/dev/null || true)"
    [[ -z "$STILL" || "$STILL" == "None" ]] && break
    sleep 10
  done
fi

# 3) Empty S3 before stack delete (CFN DeletionPolicy=Delete still needs empty bucket)
if aws cloudformation describe-stacks --region "$REGION" --stack-name "$STACK" >/dev/null 2>&1; then
  BUCKET="$(aws cloudformation describe-stacks \
    --region "$REGION" \
    --stack-name "$STACK" \
    --query "Stacks[0].Outputs[?OutputKey=='WebBucketName'].OutputValue" \
    --output text 2>/dev/null || true)"
  if [[ -n "${BUCKET:-}" && "$BUCKET" != "None" ]]; then
    echo "Emptying s3://$BUCKET"
    aws s3 rm "s3://${BUCKET}" --recursive --region "$REGION" || true
  fi

  # Empty ECR images
  REPO="$(aws cloudformation describe-stacks \
    --region "$REGION" \
    --stack-name "$STACK" \
    --query "Stacks[0].Outputs[?OutputKey=='EcrRepositoryName'].OutputValue" \
    --output text 2>/dev/null || true)"
  if [[ -n "${REPO:-}" && "$REPO" != "None" ]]; then
    echo "Deleting images in ECR $REPO"
    IMAGES="$(aws ecr list-images --region "$REGION" --repository-name "$REPO" \
      --query 'imageIds[*]' --output json 2>/dev/null || echo '[]')"
    if [[ "$IMAGES" != "[]" && "$IMAGES" != "null" ]]; then
      aws ecr batch-delete-image --region "$REGION" --repository-name "$REPO" \
        --image-ids "$IMAGES" >/dev/null || true
    fi
  fi

  echo "Deleting CloudFormation stack $STACK"
  aws cloudformation delete-stack --region "$REGION" --stack-name "$STACK"
  aws cloudformation wait stack-delete-complete --region "$REGION" --stack-name "$STACK"
  echo "Stack deleted."
else
  echo "Stack $STACK not found (already gone)."
fi

rm -rf "$ROOT/deploy/.out" 2>/dev/null || true

echo ""
echo "Teardown completo. Verifica en consola AWS (ECS, ALB, RDS, ECR, S3, CloudFront, IAM)"
echo "que no queden recursos ${PROJECT}-${ENV_NAME}-*."
