#!/bin/bash
export AWS_REGION=eu-west-2
export AWS_DEFAULT_REGION=eu-west-2
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_data_upserted_gmr_processor_gto

function is_ready() {
    list_queues="$(aws --endpoint-url=http://localhost:4566 \
    sqs list-queues --region eu-west-2 --query "QueueUrls[?contains(@, 'trade_imports_data_upserted_gmr_processor_gto')] | [0] != null"
    )" && [[ "$list_queues" == "true" ]]
}

while ! is_ready; do
    echo "Waiting until ready"
    sleep 1
done

touch /tmp/ready