# Standard Logic App Notifier

This Logic App Standard project subscribes to the registration event queue and forwards each event to the backend email delivery endpoint.

## Required app settings

- `servicebus_connectionString`
- `servicebus_queue_name`
- `notification_callback_url`
- `notification_webhook_key`

## Deployment

1. Create a Logic App Standard resource with a Workflow Standard plan and storage account.
2. Set the app settings above.
3. Zip the contents of this folder.
4. Deploy with `az logicapp deployment source config-zip`.

## Workflow

- Trigger: Service Bus built-in `receiveQueueMessages`
- Delivery: HTTP `POST` to `/api/notifications/registration-email`
