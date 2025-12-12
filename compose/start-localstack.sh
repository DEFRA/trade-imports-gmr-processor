#!/bin/bash
set -e

export AWS_REGION=eu-west-2
export AWS_DEFAULT_REGION=eu-west-2
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

QUEUE_NAMES=(
    "trade_imports_matched_gmrs_processor_eta"
    "trade_imports_data_upserted_gmr_processor_gto"
    "trade_imports_matched_gmrs_processor_gto"
    "trade_imports_matched_gmrs_gmr_processor_match"
)
SQS_ENDPOINT_URL="http://localhost:4566"

is_queue_ready() {
    local queue_name="$1"
    QUEUE_URL=$(aws --endpoint-url="$SQS_ENDPOINT_URL" sqs get-queue-url --region "$AWS_REGION" --queue-name "$queue_name" --query "QueueUrl" --output text)
    set +e
    aws --endpoint-url="$SQS_ENDPOINT_URL" sqs get-queue-attributes --queue-url "$QUEUE_URL" --attribute-names All
    STATUS=$?
    set -e
    return $STATUS
}

for queue in "${QUEUE_NAMES[@]}"; do
    aws --endpoint-url="$SQS_ENDPOINT_URL" sqs create-queue --queue-name "$queue"

    while ! is_queue_ready "$queue"; do
        echo "Waiting for $queue to be ready"
        sleep 1
    done
done

echo "Localstack Ready"

touch /tmp/ready
