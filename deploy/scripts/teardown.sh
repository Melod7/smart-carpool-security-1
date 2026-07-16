#!/usr/bin/env bash
# Tear down Kubix AWS test resources (KBX-30) — leave no billable leftovers.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
REGION="${AWS_REGION:-us-east-1}"
STACK="${STACK_NAME:-kubix-test}"
PROJECT="${PROJECT_NAME:-kubix}"
ENV_NAME="${ENVIRONMENT_NAME:-test}"
SERVICE_NAME="${SERVICE_NAME:-${PROJECT}-${ENV_NAME}-api}"

echo "==> Teardown region=$REGION stack=$STACK service=$SERVICE_NAME"

# 1) App Runner
SERVICE_ARN="$(aws apprunner list-services --region "$REGION" \
  --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
  --output text 2>/dev/null || true)"
if [[ -n "${SERVICE_ARN:-}" && "$SERVICE_ARN" != "None" ]]; then
  echo "Deleting App Runner $SERVICE_NAME"
  aws apprunner delete-service --region "$REGION" --service-arn "$SERVICE_ARN" >/dev/null
  echo "Waiting for App Runner deletion..."
  for _ in $(seq 1 60); do
    STILL="$(aws apprunner list-services --region "$REGION" \
      --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
      --output text 2>/dev/null || true)"
    [[ -z "$STILL" || "$STILL" == "None" ]] && break
    sleep 10
  done
else
  echo "No App Runner service $SERVICE_NAME"
fi

# 2) Empty S3 before stack delete (CFN DeletionPolicy=Delete still needs empty bucket)
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
echo "Teardown completo. Verifica en consola AWS (App Runner, RDS, ECR, S3, CloudFront, IAM roles)"
echo "que no queden recursos ${PROJECT}-${ENV_NAME}-*."
