# Logic App Registration Notifications

Sprint 2 publishes registration events to Service Bus through the backend notification service. A Logic App can subscribe to that queue or topic subscription and send the production email notifications.

## Expected Service Bus Entity

Default entity name:
- `classfinder-registration-events`

Each message is JSON with these fields:
- `recipientName`
- `recipientEmail`
- `action`
- `studentId`
- `classId`
- `classTitle`
- `department`
- `instructor`
- `location`
- `scheduleSummary`
- `credits`
- `availableSeats`
- `occurredAtUtc`

The backend also sets these message properties:
- `Subject = registration.<action>`
- `ApplicationProperties[eventType] = registration.<action>`
- `ApplicationProperties[studentId]`
- `ApplicationProperties[classId]`

## Logic App Shape

Use a Consumption or Standard Logic App with:
1. Service Bus trigger:
   - Queue: `classfinder-registration-events`
   - Or a topic subscription if you switch the backend sender to a topic.
2. Parse JSON action using the payload schema in `registration-event.schema.json`.
3. Send email action using Microsoft 365 Outlook, Outlook.com, SMTP, or another mail connector.

Suggested email subject:
- `ClassFinder @{body('Parse_JSON')?['action']} confirmation - @{body('Parse_JSON')?['classId']}`

Suggested email body:
- Student ID: `@{body('Parse_JSON')?['studentId']}`
- Class: `@{body('Parse_JSON')?['classId']} - @{body('Parse_JSON')?['classTitle']}`
- Department: `@{body('Parse_JSON')?['department']}`
- Instructor: `@{body('Parse_JSON')?['instructor']}`
- Schedule: `@{body('Parse_JSON')?['scheduleSummary']}`
- Location: `@{body('Parse_JSON')?['location']}`
- Credits: `@{body('Parse_JSON')?['credits']}`
- Available seats: `@{body('Parse_JSON')?['availableSeats']}`
- Processed at: `@{body('Parse_JSON')?['occurredAtUtc']}`

## Verification

After the Logic App is wired:
1. Enroll a student through `/api/students/{id}/schedule`.
2. Confirm a Service Bus message lands on the entity.
3. Confirm the Logic App run succeeds.
4. Confirm the recipient gets the enrollment email.
5. Drop the class and repeat for the drop email.
