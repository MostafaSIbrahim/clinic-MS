# Logs and health check (hosted sites)

## Health

`https://<site>/health` answers `Healthy` (HTTP 200) when the application runs and reaches its database, otherwise `Unhealthy` (HTTP 503). It shows no details. Point an uptime monitor at it (e.g. a free external monitor, checking every 5 minutes).

## Logs

The application writes one JSON file per day to `App_Data/logs/safya-YYYYMMDD.json` (31 days kept, 20 MB per file). Open them with the MonsterASP File Manager. Each line is one event; useful fields:

| Field | Meaning |
|---|---|
| `@t` | time (UTC) |
| `@l` | level (missing = Information) |
| `@mt` / `@m` | message |
| `RequestId` | the reference shown on the error page — search for it to find what went wrong |
| `UserId` | signed-in user's id |
| `@x` | exception details |

The site's application pool needs write access to `App_Data` (the same as for attachments, S3). If no file appears after a few requests, check that permission in the control panel.

Startup failures (before logging starts) are only visible with the IIS stdout log: temporarily set `stdoutLogEnabled="true"` in the server's `web.config`, reproduce, then set it back to `false` (it is not rotated).