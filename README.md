# DA Service Historik

Automatisk service-reminder system for Deres Auto.

Systemet henter service-historik via API, beregner hvornår biler skal til service og sender automatiske SMS og email reminders 30 og 14 dage før.

## Tech Stack

- .NET 8 Web API (C#)
- Hangfire (scheduler + dashboard)
- Azure SQL (reminder-state)
- Reimund API (SMS)
- Resend (email)
- Azure App Service (hosting)

## Kom i gang

Se `docs/superpowers/specs/2026-04-14-da-service-historik-design.md` for fuld arkitektur og design.

## Service-regler

Rediger `servicerules.json` i roden — push til `main` for at opdatere.

## Dashboard

Tilgås på `/hangfire` — viser job-historik, fejl og mulighed for manuel kørsel.
