# easel — Architektura a plán vývoje

CLI nástroj pro vývojáře canvas aplikací: statická analýza, dependency analýza, metriky, secrets scan, sémantický diff a refaktoring nad `pa.yaml` zdrojáky.

> Pracovní název `easel` — finální název dořešit před prvním releasem (kolize na NuGet/GitHub, vyslovitelnost).

---

## 1. Cíle a principy

- **Jeden binární CLI** (self-contained .exe / binárka pro win-x64, osx-arm64, linux-x64) + `dotnet tool` varianta.
- **pac CLI je nutný předpoklad** — veškerý unpack/pack .msapp a solution probíhá výhradně přes `pac`, nikdy vlastní implementací. Výjimka: již rozbalená složka (Git integrace) se čte přímo.
- **Read-only default** — jediný zapisující příkaz je `rename`. Vše ostatní nikdy nemodifikuje vstup.
- **Deterministické jádro** — AI (explain/fix) je volitelná vrstva nad výsledky, ne součást analýzy.
- **CI-first** — každý výstup existuje ve strojové podobě (JSON, SARIF), exit codes umožňují gate v pipeline.
- **Pre-YAML formát mimo scope** — detekovat a vrátit instrukci k převodu (otevřít a uložit v Power Apps Studio).

## 2. Technologický základ

| Oblast | Volba | Poznámka |
|---|---|---|
| Runtime | .NET 10 (LTS), C# 14 | publish: self-contained, trimmed, AOT kde to závislosti dovolí |
| CLI framework | System.CommandLine | subcommands, completion, help |
| Konzolový výstup | Spectre.Console | tabulky, barvy, progress |
| Power Fx parsing | Microsoft.PowerFx.Core (NuGet) | AST, CheckResult, seznam identifikátorů |
| YAML | YamlDotNet | čtení pa.yaml dle oficiálního schématu v3.0 |
| Unpack/pack | pac CLI (externí proces) | pinovaná minimální verze |
| Testy | xUnit + Verify (snapshot testy) | fixture aplikace v repu |
| CI/CD | GitHub Actions | build, test, release binárek, NuGet publish |

## 3. Struktura solution

```
easel/
├── src/
│   ├── Easel.Cli/            # entrypoint, definice příkazů, DI kompozice
│   ├── Easel.Core/           # doménový model, loader, symbol table, dep graph
│   ├── Easel.Fx/             # wrapper nad Microsoft.PowerFx (parse, cache, AST walk)
│   ├── Easel.Pac/            # PacRunner — detekce, verze, unpack/pack, temp správa
│   ├── Easel.Rules/          # lint pravidla (IRule implementace) + rule engine
│   ├── Easel.Analysis/       # analyze, stats, secrets, diff, rename engines
│   └── Easel.Output/         # renderery: Console, JSON, SARIF, HTML
├── tests/
│   ├── Easel.Tests/          # unit + snapshot testy
│   └── fixtures/               # sada testovacích aplikací (unpacked YAML + msapp)
├── action/                     # GitHub Action (composite: install pac → run easel)
├── docs/                       # dokumentace, pravidla, příklady configů
└── .easel.yml                # ukázkový config
```

Závislosti mezi projekty: `Cli → (Core, Pac, Rules, Analysis, Output)`, `Rules/Analysis → Core + Fx`, `Core → Fx`. `Pac` je izolovaný (žádná závislost na Core), aby šel testovat samostatně.

## 4. Datový tok (pipeline)

```
vstup (cesta)
  → InputResolver          rozpozná: unpacked složka | .msapp | solution zip
  → PacRunner              (jen msapp/solution) pac canvas unpack → temp
  → YamlLoader             pa.yaml → surové dokumenty dle schématu
  → AppModelBuilder        → AppModel (App, Screens, Controls, Properties,
                              DataSources, Components, NamedFormulas, Media)
  → FxParseService         každá property s formulí → Power Fx AST (lazy + cache)
  → SymbolTableBuilder     definice a použití: proměnné, kolekce, named formulas
  → DependencyGraphBuilder graf: screen/control/proměnná/datový zdroj/konektor
  → Command executor       lint | analyze | stats | secrets | diff | rename
  → Renderer               console | json | sarif | html
  → exit code
```

Model, symbol table i graf se staví **jednou** a sdílí mezi příkazy — kombinace `easel lint --with-stats` nesmí parsovat dvakrát.

## 5. Klíčové komponenty

### 5.1 PacRunner (Easel.Pac)
- Detekce `pac` v PATH + typických instalačních cestách (Win/Mac/Linux), zjištění verze, porovnání s `MinSupportedPacVersion`.
- Spuštění procesu s explicitními argumenty; stderr pac přeposílat s prefixem `[pac]`; mapování exit codes na vlastní chyby.
- Temp správa: `%TEMP%/easel/<hash vstupu>/`, úklid po doběhu, `--keep-temp` pro debugging.
- Fail-fast hláška při chybějícím pac s doslovnou instalační instrukcí (`dotnet tool install --global Microsoft.PowerApps.CLI.Tool`).

### 5.2 AppModel (Easel.Core)
Imutabilní objektový model aplikace nezávislý na YAML reprezentaci. Každý element nese `SourceLocation` (soubor, řádek, sloupec) kvůli přesnému reportování a SARIF. Verzování controls (`Control@verze`) zachovat v modelu.

### 5.3 FxParseService (Easel.Fx)
- Parse přes Microsoft.PowerFx, výsledky cachovat per property.
- Tolerantní režim: neparsovatelná formule = diagnostika `PF0001` (nikdy crash celé analýzy).
- AST walker API pro pravidla: návštěvník funkcí, identifikátorů, literálů.
- **Smoke test na začátku projektu:** ověřit parser na sadě reálných produkčních formulí (verze funkcí, chování mimo host kontext). Jediné zásadní technické riziko projektu.

### 5.4 SymbolTable + DependencyGraph
- Zápisy: `Set`, `UpdateContext`, `Collect`, `ClearCollect`, `Patch` do kolekce, named formulas, `Navigate` context.
- Čtení: výskyt identifikátoru v AST.
- Graf: uzly (screen, control, proměnná, kolekce, datový zdroj, konektor, media asset), hrany (defines, reads, navigates-to, binds-to).
- Dotazy: find-usages, dead-code (uzly bez příchozích read hran), impact (tranzitivní uzávěr), export Mermaid/DOT.

### 5.5 RuleEngine (Easel.Rules)
```csharp
public interface IRule
{
    string Id { get; }              // "PA1003"
    string Name { get; }            // "unused-variable"
    RuleCategory Category { get; }  // Performance | Naming | Maintainability | Accessibility | Security
    Severity DefaultSeverity { get; }
    IEnumerable<Finding> Evaluate(RuleContext ctx); // ctx: AppModel + SymbolTable + DepGraph + config
}
```
- Registrace přes DI + reflexi; každé pravidlo má vlastní config sekci (např. naming regexy).
- Baseline: `easel lint --write-baseline` → `.easel-baseline.json` (fingerprint nálezů); následné běhy reportují jen nové nálezy.
- Potlačení: config (`ignore` per pravidlo/cesta/element).

### 5.6 DiffEngine (Easel.Analysis)
- Dva AppModely → matching elementů: primárně dle jména, sekundárně heuristika pro přejmenování (shoda typu + vysoká podobnost properties → kandidát na rename).
- Klasifikace změn: Added / Removed / Renamed / PropertyChanged (s AST-level diffem formule) / Moved (změna parenta).
- Výstup: čitelný souhrn, JSON changelog, Markdown pro PR komentář.

### 5.7 SecretsScanner
- Zdroj: string literály z AST + vybrané properties (URL, connection related).
- Detektory: regex sada (API klíče, connection stringy, tokeny, URL s credentials) + Shannonova entropie pro generické tajnosti.
- Inventář konektorů a datových zdrojů (DLP relevance) jako součást výstupu.

### 5.8 RenameEngine
- Vstup: **pouze .msapp** (pa.yaml z Git integrace je read-only — odmítnout s vysvětlením).
- Flow: pac unpack → rename v AST → serializace zpět do YAML → pac pack → nový .msapp (nikdy nepřepisovat vstup, výstup vedle).
- Předletová kontrola: kolize cílového jména v symbol table.
- Označeno jako **preview**; po pack vyzvat uživatele k otevření a ověření ve Studiu.

### 5.9 Output (Easel.Output)
- Console (default): seskupení dle souboru/screenu, barvy dle severity, souhrn.
- JSON: stabilní schéma, verzované (`schemaVersion`).
- SARIF 2.1.0: pro GitHub code scanning / DevOps.
- HTML: samostatný report soubor (pro konzultanty a audit u klienta).
- Exit codes: `0` OK, `1` nálezy ≥ threshold (`--fail-on warning|error`), `2` chyba vstupu, `3` chybí/nekompatibilní pac, `4` interní chyba.

### 5.10 Konfigurace
`.easel.yml` v kořeni repa (hledá se směrem nahoru):
```yaml
rules:
  naming-convention:
    severity: warning
    patterns: { variable: "^var[A-Z]", collection: "^col[A-Z]", screen: "^scr[A-Z]", control: "^(btn|lbl|txt|gal|con)[A-Z]" }
  screen-control-limit: { max: 300 }
  unused-media: off
ignore:
  - "**/LegacyScreen.pa.yaml"
output: { format: console }
```

### 5.11 Doctor
`easel doctor`: verze nástroje, pac (nalezen/verze/min. verze), PowerFx verze, podporovaná verze pa.yaml schématu, zápis do temp, výsledek testovacího unpacku (volitelně).

## 6. Sada lint pravidel (cílový stav)

| ID | Pravidlo | Kategorie |
|---|---|---|
| PA1001 | Nedelegovatelná funkce nad delegovatelným zdrojem | Performance |
| PA1002 | N+1 pattern (`ForAll` + `Patch`/`LookUp`/`Collect`) | Performance |
| PA1003 | Nepoužitá proměnná / kolekce | Maintainability |
| PA1004 | Nepoužitý media asset | Maintainability |
| PA1005 | Překročen limit controls na screen (default 300) | Performance |
| PA1006 | Těžká logika v `App.OnStart` (doporučit named formulas) | Performance |
| PA1007 | Naming conventions (konfigurovatelné regexy) | Naming |
| PA1008 | Hardcoded barva mimo theme | Maintainability |
| PA1009 | Chybějící `AccessibleLabel` u interaktivního controlu | Accessibility |
| PA1010 | Hluboce vnořené `If` (→ `Switch`), práh konfigurovatelný | Maintainability |
| PA1011 | `Timer`/`OnVisible` anti-patterny (skryté side-effecty) | Maintainability |
| PA1012 | Duplicitní formule nad prahem délky (kandidát na named formula / komponentu) | Maintainability |
| PA1013 | Nekonzistentní verze téhož controlu napříč aplikací | Maintainability |
| PA1014 | Text bez lokalizace tam, kde app používá lokalizační pattern | Maintainability |
| PA2001 | Podezřelý literál — API klíč / token / connection string (regex) | Security |
| PA2002 | Vysokoentropický literál | Security |
| PA2003 | URL s vloženými credentials | Security |
| PF0001 | Neparsovatelná formule | Error |

(PA2xxx sdílí detektory se `secrets`; v lint běhu jako pravidla, v `secrets` jako samostatný report s inventářem konektorů.)

---

## 7. Vývojové tasky — celé řešení

Velikosti: **S** ≤ 1 den, **M** 2–4 dny, **L** ~1 týden, **XL** > 1 týden. Pořadí uvnitř fáze = doporučené pořadí realizace.

### Fáze 0 — Základy (před první řádkou logiky)
- [ ] **T0.1 (S)** Ověřovací spike: Microsoft.PowerFx na net10.0 — naparsovat 30–50 reálných produkčních formulí, ověřit AST API a chování mimo host. *Go/no-go celého projektu.*
- [ ] **T0.2 (S)** Ověřovací spike: `pac canvas unpack`/`pack` round-trip na 3 reálných aplikacích — je pack výstup otevíratelný ve Studiu beze změn chování?
- [ ] **T0.3 (S)** Repo setup: solution struktura dle kap. 3, editorconfig, nullable, analyzery, licence (MIT/Apache dle dřívějšího rozhodnutí), README kostra.
- [ ] **T0.4 (M)** CI: build + test workflow, release workflow (self-contained binárky win/osx/linux + dotnet tool push na NuGet), verzování (MinVer/GitVersion).
- [ ] **T0.5 (M)** PacRunner: detekce, verze, min-version gate, spuštění unpack, temp správa, `--keep-temp`, chybové hlášky s instalační instrukcí.
- [ ] **T0.6 (S)** InputResolver: složka vs .msapp vs solution zip, detekce pre-YAML formátu → chyba s instrukcí.
- [ ] **T0.7 (S)** `easel doctor` v1 (nástroj, pac, PowerFx, schéma, temp).
- [ ] **T0.8 (M)** Fixture sada: 3–5 malých aplikací pokrývajících proměnné, kolekce, komponenty, média, více screenů; uložit unpacked i jako .msapp; skript na regeneraci.

### Fáze 1 — Jádro modelu
- [ ] **T1.1 (M)** YamlLoader dle pa.yaml schématu v3.0 (App, Screens, controls strom, properties, verze controls, komponenty, named formulas, media).
- [ ] **T1.2 (M)** AppModel: imutabilní model + SourceLocation pro každý element a property.
- [ ] **T1.3 (M)** FxParseService: parse + cache, tolerantní režim (PF0001), AST walker API.
- [ ] **T1.4 (L)** SymbolTableBuilder: zápisy (Set/UpdateContext/Collect/ClearCollect/Patch/named formulas), čtení, scoping (globální vs context proměnné per screen).
- [ ] **T1.5 (M)** DependencyGraphBuilder: uzly, hrany, dotazy find-usages / dead-code / impact.
- [ ] **T1.6 (M)** Snapshot testy jádra nad fixtures (Verify): model, symbol table, graf.

### Fáze 2 — lint + stats (→ release v0.1)
- [ ] **T2.1 (M)** RuleEngine: IRule, RuleContext, registrace, severities, per-rule config.
- [ ] **T2.2 (M)** Config loader `.easel.yml`: hledání nahoru, validace, defaulty, `ignore` glob.
- [ ] **T2.3 (L)** Pravidla PA1003–PA1010 (8 pravidel; každé = implementace + testy + dokumentační stránka).
- [ ] **T2.4 (M)** Pravidla PA1001–PA1002 (delegace, N+1) — heuristiky nad AST, konzervativně (raději méně false positives).
- [ ] **T2.5 (M)** Baseline: `--write-baseline`, fingerprinting nálezů stabilní vůči posunu řádků.
- [ ] **T2.6 (M)** Renderery: Console (Spectre) + JSON (verzované schéma).
- [ ] **T2.7 (M)** SARIF 2.1.0 renderer + ověření v GitHub code scanning.
- [ ] **T2.8 (S)** Exit codes + `--fail-on`.
- [ ] **T2.9 (M)** `stats`: metriky (controls/screen, media velikosti, počty zdrojů, komplexita formulí), console + JSON.
- [ ] **T2.10 (M)** GitHub Action (composite: install pac → install easel → run → upload SARIF) + ukázkový workflow do docs.
- [ ] **T2.11 (M)** Dokumentace v0.1: instalace, quickstart, referenc pravidel, config referenc. **→ Release v0.1**

### Fáze 3 — analyze + secrets (→ v0.2)
- [ ] **T3.1 (M)** `analyze --find <symbol>`: definice + všechna použití s lokacemi.
- [ ] **T3.2 (M)** `analyze --dead-code`: nepoužité proměnné/kolekce/media/screeny bez navigace.
- [ ] **T3.3 (M)** `analyze --impact <symbol>`: tranzitivní dopad změny.
- [ ] **T3.4 (M)** `analyze --graph mermaid|dot [--scope screen|app]`: export grafu.
- [ ] **T3.5 (M)** SecretsScanner: regex sada + entropie + allowlist config.
- [ ] **T3.6 (S)** `secrets`: report + inventář konektorů/datových zdrojů; PA2001–2003 zapojit i do lint.
- [ ] **T3.7 (S)** Dokumentace + release v0.2.

### Fáze 4 — diff (→ v0.3)
- [ ] **T4.1 (M)** Matching elementů dle jména + klasifikace Added/Removed/PropertyChanged/Moved.
- [ ] **T4.2 (L)** Rename heuristika (typ + podobnost properties) — konfigurovatelný práh, označovat jako „pravděpodobné přejmenování".
- [ ] **T4.3 (M)** AST-level diff formulí (ne textový): co se změnilo sémanticky.
- [ ] **T4.4 (M)** Výstupy: console souhrn, JSON changelog, Markdown pro PR komentář.
- [ ] **T4.5 (S)** Integrace do GitHub Action (diff base vs head v PR, komentář).
- [ ] **T4.6 (S)** Dokumentace + release v0.3.

### Fáze 5 — rename (preview) (→ v0.4)
- [ ] **T5.1 (M)** YAML serializace zpět (round-trip zachovávající formát v mezích možností pac pack).
- [ ] **T5.2 (M)** RenameEngine: přejmenování v AST všech výskytů, předletová kontrola kolizí.
- [ ] **T5.3 (M)** Orchestrace: pac unpack → rename → pac pack → nový .msapp vedle vstupu; odmítnutí Git-integration složky s vysvětlením.
- [ ] **T5.4 (M)** E2E testy: rename → pack → unpack → ověřit konzistenci; manuální checklist pro Studio ověření.
- [ ] **T5.5 (S)** Dokumentace s výrazným preview označením + release v0.4.

### Fáze 6 — Distribuce a ekosystém
- [ ] **T6.1 (M)** HTML report renderer (lint + stats + secrets v jednom souboru, offline, vhodný pro klienty).
- [ ] **T6.2 (M)** Azure DevOps task (wrapper analogický GitHub Action).
- [ ] **T6.3 (S)** winget manifest + Homebrew formula.
- [ ] **T6.4 (M)** Docs web (GitHub Pages) + web playground sekce s příklady výstupů.
- [ ] **T6.5 (S)** Šablony issues, contributing guide, roadmapa — příprava na komunitní příspěvky (hlavně nová pravidla).
- [ ] **T6.6 (M)** Telemetrie rozhodnutí: buď žádná, nebo opt-in anonymní počty běhů — rozhodnout a zdokumentovat.

### Fáze 7 — AI vrstva (volitelná, opt-in)
- [ ] **T7.1 (M)** `explain <finding-id>`: kontext nálezu (formule + okolí) → LLM vysvětlení; provider abstrakce (lokální endpoint / API), žádná data bez explicitního flagu.
- [ ] **T7.2 (L)** `fix --suggest`: návrh opravy formule jako diff (nikdy autoapply), validace návrhu zpětným parsem PowerFx.
- [ ] **T7.3 (S)** Config: provider, endpoint, model; privacy dokumentace (co se kam posílá).

### Průběžné (každá fáze)
- [ ] **TX.1** Aktualizace min. podporované verze pac + test matrix proti nejnovější pac verzi (CI job).
- [ ] **TX.2** Sledování změn pa.yaml schématu a Microsoft.PowerFx releasů; kompatibilitní testy.
- [ ] **TX.3** Rozšiřování fixtures o reálné vzory z praxe (anonymizované).

---

## 8. Hlavní rizika

| Riziko | Dopad | Mitigace |
|---|---|---|
| PowerFx parser se chová jinak mimo host kontext | blokující | T0.1 spike před vším ostatním |
| pac pack round-trip nespolehlivý | rename nepoužitelný | T0.2 spike; rename až fáze 5, preview označení |
| Změna pa.yaml schématu (aktivní vývoj) | rozbití loaderu | verze schématu v doctor, kompatibilitní testy (TX.2), tolerantní parsing neznámých klíčů |
| Nová verze pac změní výstup/argumenty | rozbití unpacku | pinovaná min. verze, CI matrix (TX.1) |
| False positives u delegace/N+1 | ztráta důvěry uživatelů | konzervativní heuristiky, snadné potlačení, baseline |
| Microsoft vydá vlastní ekvivalent | konkurence | rychlost, CI-first zaměření, komunitní pravidla — Power CAT cílí na review v tenantu, ne na dev loop |
