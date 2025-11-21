#!/bin/bash
export AWS_REGION=eu-west-2
export AWS_DEFAULT_REGION=eu-west-2
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

QUEUE_NAME="trade_imports_data_upserted_gmr_processor_gto"
SQS_ENDPOINT_URL="http://localhost:4566"

aws --endpoint-url="$SQS_ENDPOINT_URL" \
    sqs create-queue \
    --queue-name "$QUEUE_NAME"

is_queue_ready() {
    [ "$(aws --endpoint-url="$SQS_ENDPOINT_URL" sqs list-queues --region "$AWS_REGION" \
        --query "QueueUrls[?contains(@, '$1')] | [0] != null")" = "true" ]
}

while ! is_queue_ready "$QUEUE_NAME"; do
    echo "Waiting until ready"
    sleep 1
done

touch /tmp/ready