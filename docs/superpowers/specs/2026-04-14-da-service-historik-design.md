# Design Spec: DA Service Historik

**Dato:** 2026-04-14
**Status:** Godkendt
**Projekt:** DA Service Historik — automatisk service-reminder system for Deres Auto

---

## Formål

Bygge et automatisk reminder-system der dagligt tjekker service-historik for Deres Autos kunder og sender SMS og email reminders 30 og 14 dage før bilen skal til service.

---

## Tech Stack

| Komponent | Valg | Begrundelse |
|---|---|---|
| Backend | .NET 8 Web API (C#) | Naturlig fit til SQL Server-økosystem |
| Scheduler | Hangfire | Gratis built-in dashboard, retry-logik, job-historik |
| Database | Azure SQL | State for sendte reminders |
| SMS | Reimund API | Eksisterende aftale |
| Email | Resend | Simpel API, god deliverability |
| Hosting | Azure App Service | Auto-deploy via GitHub Actions |
| Dashboard UI | Razor Pages | Enkel søgeside ovenpå samme .NET app |

---

## Datakilder

- **Deres Auto SQL Server API**: eksisterende HTTP API med views der returnerer:
  - `nummerplade` — bilens nummerplade
  - `serviceDato` — dato for seneste service
  - `serviceType` — hvilken type service der er udført
  - `bilMaerke` — bilmærke (bruges til at slå service-interval op)
  - `telefonNummer` — kundens mobilnummer
  - `email` — kundens email
  - `kmTal` — km-stand ved seneste service

---

## Service-regler (`servicerules.json`)

Fil placeret i roden af projektet. Redigeres direkte og pushes til GitHub — Azure opdaterer automatisk.

```json
{
  "rules": [
    { "make": "Mercedes-Benz", "intervalMonths": 12, "intervalKm": 8000 },
    { "make": "DEFAULT",       "intervalMonths": 6,  "intervalKm": 8000 }
  ]
}
```

- Regler matches på `bilMaerke` (case-insensitive)
- `DEFAULT` bruges som fallback hvis mærket ikke er defineret
- Nye mærker tilføjes ved at indsætte en ny linje i filen

---

## Azure SQL Database

### Tabel: `SentReminders`

| Kolonne | Type | Beskrivelse |
|---|---|---|
| Id | INT IDENTITY PK | |
| LicensePlate | NVARCHAR(20) | Nummerplade |
| ReminderType | NVARCHAR(10) | `"30-day"` eller `"14-day"` |
| ServiceDate | DATE | Den service-dato reminderne refererer til |
| SentAt | DATETIME | Hvornår reminderne blev sendt |

Ingen `ServiceRules`-tabel — regler styres via `servicerules.json`.

---

## Dagligt Reminder-job (kl. 16:00)

```
1. Hent alle service-records fra Deres Auto API
2. For hver record:
   a. Slå bilmærke op i servicerules.json → hent intervalMonths + intervalKm
   b. Beregn næste service-dato:
        næsteDato = sidsteDato + intervalMonths
        (km-baseret: sidsteKm + intervalKm → konverteres til estimeret dato hvis muligt)
   c. Beregn dage til næste service
   d. Er dage == 30 ELLER dage == 14?
        → Tjek SentReminders: er (nummerplade + reminderType + serviceDato) allerede sendt?
        → Nej:
            - Har kunden telefonnummer? → send SMS via Reimund
            - Har kunden email?        → send email via Resend
            - Mangler telefon eller email?
                → send notifikation til info@deresauto.gl med nummerplade + hvad der mangler
            - Gem record i SentReminders
        → Ja: skip
3. Log antal sendt, antal skippet, eventuelle fejl → synligt i Hangfire dashboard
```

---

## Fejlhåndtering

| Situation | Handling |
|---|---|
| Reimund API nede | Log fejl, gem IKKE i SentReminders — reminder sendes næste dag automatisk |
| Deres Auto API nede | Job markeres som fejlet i Hangfire — synligt i dashboard |
| Manglende telefon/email | Email til info@deresauto.gl med detaljer |
| Ukendt bilmærke | Brug DEFAULT-regel |

---

## Dashboard

Tilgås på `/hangfire` (beskyttet med brugernavn/kodeord).

Razor Pages tilføjer:

- **`/search`** — søg på nummerplade, se komplet servicehistorik fra API
- **`/reminders`** — se alle sendte reminders for en given nummerplade

---

## SMS-beskedskabelon

> "Hej! Din [Mærke] ([nummerplade]) skal til service om [X] dage. Book tid på tlf. [nummer] eller [link]. Mvh Deres Auto, Grønland"

## Email-beskedskabelon

Samme indhold, HTML-formateret med Deres Auto logo via Resend template.

---

## Notifikation ved manglende kontaktinfo

**Til:** info@deresauto.gl
**Emne:** Manglende kontaktinfo — [nummerplade]
**Indhold:** "Bilen [nummerplade] ([mærke]) skal til service om [X] dage, men mangler [telefonnummer / email]. Opdater venligst i systemet."

---

## Repo-struktur

```
da-service-historik/
├── src/
│   └── DA.ServiceHistorik.Api/
│       ├── Jobs/
│       │   └── ReminderJob.cs
│       ├── Services/
│       │   ├── ServiceRuleEngine.cs
│       │   ├── ReminderSender.cs
│       │   ├── ReimundSmsService.cs
│       │   └── ResendEmailService.cs
│       ├── Data/
│       │   └── AppDbContext.cs
│       ├── Pages/
│       │   ├── Search.cshtml
│       │   └── Reminders.cshtml
│       ├── Models/
│       ├── appsettings.json
│       └── Program.cs
├── servicerules.json
├── .github/
│   └── workflows/
│       └── deploy.yml
├── docs/
│   └── superpowers/specs/
│       └── 2026-04-14-da-service-historik-design.md
└── README.md
```

---

## Deployment

- GitHub Actions: push til `main` → auto-deploy til Azure App Service
- Secrets (API-nøgler, connection strings) gemmes i Azure App Service Configuration — aldrig i kode
- `servicerules.json` er en del af kodebasen og deployes med appen

---

## Fremtidigt (ikke i scope nu)

- CRM-integration via REST endpoints
- Km-baserede reminders (når løbende km-data er tilgængeligt)
- Udvidede service-regler per model (ikke kun mærke)
- Statistik-dashboard
