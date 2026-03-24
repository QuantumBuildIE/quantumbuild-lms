using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds default system-level safety glossary sectors and terms.
/// System defaults have TenantId = null.
/// </summary>
public static class SafetyGlossarySeedData
{
    /// <summary>
    /// Seed all 6 default safety glossary sectors with their terms (including general fallback)
    /// </summary>
    public static async Task SeedAsync(DbContext context, ILogger logger)
    {
        var existingSectorKeys = await context.Set<SafetyGlossary>()
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == null)
            .Select(g => g.SectorKey)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var glossaries = new List<SafetyGlossary>();
        var terms = new List<SafetyGlossaryTerm>();

        var seedDefinitions = new (string Key, string Name, string Icon, Func<Guid, DateTime, List<SafetyGlossaryTerm>> TermFactory)[]
        {
            ("food_hospitality", "Food & Hospitality", "\U0001F374", CreateFoodHospitalityTerms),
            ("construction", "Construction", "\U0001F3D7\uFE0F", CreateConstructionTerms),
            ("homecare", "Homecare", "\U0001F3E0", CreateHomecareTerms),
            ("transport", "Transport", "\U0001F69B", CreateTransportTerms),
            ("manufacturing", "Manufacturing", "\U0001F3ED", CreateManufacturingTerms),
            ("general", "General", "🛡️", CreateGeneralTerms),
        };

        foreach (var (key, name, icon, termFactory) in seedDefinitions)
        {
            if (existingSectorKeys.Contains(key))
                continue;

            var glossary = CreateGlossary(key, name, icon, now);
            glossaries.Add(glossary);
            terms.AddRange(termFactory(glossary.Id, now));
        }

        if (glossaries.Count == 0)
        {
            logger.LogInformation("All system safety glossaries already exist, skipping");
            return;
        }

        await context.Set<SafetyGlossary>().AddRangeAsync(glossaries);
        await context.Set<SafetyGlossaryTerm>().AddRangeAsync(terms);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {GlossaryCount} safety glossary sectors with {TermCount} terms", glossaries.Count, terms.Count);
    }

    private static SafetyGlossary CreateGlossary(string sectorKey, string sectorName, string sectorIcon, DateTime now)
    {
        return new SafetyGlossary
        {
            Id = Guid.NewGuid(),
            TenantId = null, // System default
            SectorKey = sectorKey,
            SectorName = sectorName,
            SectorIcon = sectorIcon,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "system"
        };
    }

    private static SafetyGlossaryTerm CreateTerm(Guid glossaryId, string englishTerm, string category, bool isCritical, string translationsJson, DateTime now)
    {
        return new SafetyGlossaryTerm
        {
            Id = Guid.NewGuid(),
            GlossaryId = glossaryId,
            EnglishTerm = englishTerm,
            Category = category,
            IsCritical = isCritical,
            Translations = translationsJson,
            CreatedAt = now,
            CreatedBy = "system"
        };
    }

    private static List<SafetyGlossaryTerm> CreateFoodHospitalityTerms(Guid glossaryId, DateTime now)
    {
        return
        [
            CreateTerm(glossaryId, "HACCP", "regulatory", true,
                """{"fr":"HACCP","pl":"HACCP","ro":"HACCP","uk":"НАССР","pt":"HACCP","es":"HACCP","lt":"RVASVT","de":"HACCP","lv":"HACCP"}""", now),
            CreateTerm(glossaryId, "critical control point", "regulatory", true,
                """{"fr":"point critique pour la maîtrise","pl":"krytyczny punkt kontroli","ro":"punct critic de control","uk":"критична контрольна точка","pt":"ponto crítico de controlo","es":"punto de control crítico","lt":"kritinis kontrolės taškas","de":"kritischer Kontrollpunkt","lv":"kritiskais kontroles punkts"}""", now),
            CreateTerm(glossaryId, "allergen", "allergen", true,
                """{"fr":"allergène","pl":"alergen","ro":"alergen","uk":"алерген","pt":"alergénio","es":"alérgeno","lt":"alergenas","de":"Allergen","lv":"alergēns"}""", now),
            CreateTerm(glossaryId, "celery", "allergen", true,
                """{"fr":"céleri","pl":"seler","ro":"țelină","uk":"селера","pt":"aipo","es":"apio","lt":"salierai","de":"Sellerie","lv":"selerija"}""", now),
            CreateTerm(glossaryId, "gluten", "allergen", true,
                """{"fr":"gluten","pl":"gluten","ro":"gluten","uk":"глютен","pt":"glúten","es":"gluten","lt":"gliutenas","de":"Gluten","lv":"lipeklis"}""", now),
            CreateTerm(glossaryId, "sulphites", "allergen", true,
                """{"fr":"sulfites","pl":"siarczyny","ro":"sulfiți","uk":"сульфіти","pt":"sulfitos","es":"sulfitos","lt":"sulfitai","de":"Sulfite","lv":"sulfīti"}""", now),
            CreateTerm(glossaryId, "peanuts", "allergen", true,
                """{"fr":"arachides","pl":"orzeszki ziemne","ro":"arahide","uk":"арахіс","pt":"amendoim","es":"cacahuetes","lt":"žemės riešutai","de":"Erdnüsse","lv":"zemesrieksti"}""", now),
            CreateTerm(glossaryId, "shellfish", "allergen", true,
                """{"fr":"crustacés","pl":"skorupiaki","ro":"crustacee","uk":"ракоподібні","pt":"crustáceos","es":"mariscos","lt":"vėžiagyviai","de":"Krebstiere","lv":"vēžveidīgie"}""", now),
            CreateTerm(glossaryId, "molluscs", "allergen", true,
                """{"fr":"mollusques","pl":"mięczaki","ro":"moluște","uk":"молюски","pt":"moluscos","es":"moluscos","lt":"moliuskai","de":"Weichtiere","lv":"mīkstmieši"}""", now),
            CreateTerm(glossaryId, "do not mix", "prohibition", true,
                """{"fr":"ne pas mélanger","pl":"nie mieszać","ro":"nu amestecați","uk":"не змішувати","pt":"não misturar","es":"no mezclar","lt":"nemaišyti","de":"nicht mischen","lv":"nejauciet"}""", now),
            CreateTerm(glossaryId, "anaphylaxis", "emergency", true,
                """{"fr":"anaphylaxie","pl":"anafilaksja","ro":"anafilaxie","uk":"анафілаксія","pt":"anafilaxia","es":"anafilaxia","lt":"anafilaksija","de":"Anaphylaxie","lv":"anafilakse"}""", now),
            CreateTerm(glossaryId, "cross contamination", "food_safety", true,
                """{"fr":"contamination croisée","pl":"zanieczyszczenie krzyżowe","ro":"contaminare încrucișată","uk":"перехресне забруднення","pt":"contaminação cruzada","es":"contaminación cruzada","lt":"kryžminė tarša","de":"Kreuzkontamination","lv":"krusteniski piesārņojums"}""", now),
            CreateTerm(glossaryId, "temperature danger zone", "food_safety", true,
                """{"fr":"zone de danger de température","pl":"niebezpieczna strefa temperatury","ro":"zona de pericol termic","uk":"небезпечна температурна зона","pt":"zona de perigo de temperatura","es":"zona de peligro de temperatura","lt":"pavojingoji temperatūros zona","de":"Temperaturgefährdungszone","lv":"temperatūras bīstamā zona"}""", now),
            CreateTerm(glossaryId, "call 999", "emergency", true,
                """{"fr":"appelez le 999","pl":"zadzwoń pod 999","ro":"sunați la 999","uk":"зателефонуйте 999","pt":"ligue para o 999","es":"llame al 999","lt":"skambinkite 999","de":"rufen Sie 999 an","lv":"zvaniet uz 999"}""", now),
            CreateTerm(glossaryId, "epinephrine", "emergency", true,
                """{"fr":"épinéphrine","pl":"epinefryna","ro":"epinefrină","uk":"адреналін","pt":"epinefrina","es":"epinefrina","lt":"epinefrinas","de":"Epinephrin","lv":"epinefrīns"}""", now),
            CreateTerm(glossaryId, "food poisoning", "hazard", true,
                """{"fr":"intoxication alimentaire","pl":"zatrucie pokarmowe","ro":"intoxicație alimentară","uk":"харчове отруєння","pt":"intoxicação alimentar","es":"intoxicación alimentaria","lt":"apsinuodijimas maistu","de":"Lebensmittelvergiftung","lv":"saindēšanās ar pārtiku"}""", now),
        ];
    }

    private static List<SafetyGlossaryTerm> CreateConstructionTerms(Guid glossaryId, DateTime now)
    {
        return
        [
            CreateTerm(glossaryId, "PPE", "equipment", true,
                """{"fr":"EPI","pl":"ŚOI","ro":"EIP","uk":"ЗІЗ","pt":"EPI","es":"EPI","lt":"AAP","de":"PSA","lv":"IAL"}""", now),
            CreateTerm(glossaryId, "do not enter", "prohibition", true,
                """{"fr":"ne pas entrer","pl":"nie wchodzić","ro":"nu intrați","uk":"не входити","pt":"não entrar","es":"no entrar","lt":"neiti","de":"nicht betreten","lv":"neieiet"}""", now),
            CreateTerm(glossaryId, "lockout tagout", "procedure", true,
                """{"fr":"consignation déconsignation","pl":"blokada i oznakowanie","ro":"blocare și etichetare","uk":"блокування та маркування","pt":"bloqueio e etiquetagem","es":"bloqueo y etiquetado","lt":"užrakinimas ir ženklinimas","de":"Lockout Tagout","lv":"bloķēšana un marķēšana"}""", now),
            CreateTerm(glossaryId, "COSHH", "regulatory", true,
                """{"fr":"COSHH","pl":"COSHH","ro":"COSHH","uk":"COSHH","pt":"COSHH","es":"COSHH","lt":"COSHH","de":"COSHH","lv":"COSHH"}""", now),
            CreateTerm(glossaryId, "risk assessment", "regulatory", true,
                """{"fr":"évaluation des risques","pl":"ocena ryzyka","ro":"evaluarea riscurilor","uk":"оцінка ризиків","pt":"avaliação de riscos","es":"evaluación de riesgos","lt":"rizikos vertinimas","de":"Risikobeurteilung","lv":"riska novērtējums"}""", now),
            CreateTerm(glossaryId, "working at height", "hazard", true,
                """{"fr":"travail en hauteur","pl":"praca na wysokości","ro":"lucrul la înălțime","uk":"робота на висоті","pt":"trabalho em altura","es":"trabajo en altura","lt":"darbas aukštyje","de":"Arbeiten in der Höhe","lv":"darbs augstumā"}""", now),
            CreateTerm(glossaryId, "permit to work", "procedure", true,
                """{"fr":"permis de travail","pl":"zezwolenie na pracę","ro":"permis de lucru","uk":"дозвіл на роботу","pt":"licença de trabalho","es":"permiso de trabajo","lt":"leidimas dirbti","de":"Arbeitserlaubnis","lv":"darba atļauja"}""", now),
            CreateTerm(glossaryId, "emergency stop", "emergency", true,
                """{"fr":"arrêt d'urgence","pl":"wyłącznik awaryjny","ro":"oprire de urgență","uk":"аварійна зупинка","pt":"paragem de emergência","es":"parada de emergencia","lt":"avarinė stabdymo sistema","de":"Not-Aus","lv":"avārijas apstāšanās"}""", now),
            CreateTerm(glossaryId, "confined space", "hazard", true,
                """{"fr":"espace confiné","pl":"przestrzeń ograniczona","ro":"spațiu închis","uk":"замкнений простір","pt":"espaço confinado","es":"espacio confinado","lt":"uždara erdvė","de":"enger Raum","lv":"ierobežota telpa"}""", now),
            CreateTerm(glossaryId, "asbestos", "hazard", true,
                """{"fr":"amiante","pl":"azbest","ro":"azbest","uk":"азбест","pt":"amianto","es":"asbesto","lt":"asbestas","de":"Asbest","lv":"asbests"}""", now),
            CreateTerm(glossaryId, "fall arrest", "equipment", true,
                """{"fr":"arrêt de chute","pl":"zatrzymanie upadku","ro":"oprire cădere","uk":"страхування від падіння","pt":"travamento de queda","es":"detención de caída","lt":"kritimo sustabdymas","de":"Auffangsystem","lv":"krišanas aizturs"}""", now),
            CreateTerm(glossaryId, "method statement", "regulatory", true,
                """{"fr":"mode opératoire","pl":"instrukcja bezpiecznej pracy","ro":"declarație de metodă","uk":"методична документація","pt":"declaração de método","es":"declaración de método","lt":"darbo metodikos aprašas","de":"Arbeitsanweisung","lv":"metodes apraksts"}""", now),
            CreateTerm(glossaryId, "isolation", "procedure", true,
                """{"fr":"isolation","pl":"izolacja","ro":"izolare","uk":"ізоляція","pt":"isolamento","es":"aislamiento","lt":"izoliacija","de":"Isolierung","lv":"izolācija"}""", now),
        ];
    }

    private static List<SafetyGlossaryTerm> CreateHomecareTerms(Guid glossaryId, DateTime now)
    {
        return
        [
            CreateTerm(glossaryId, "manual handling", "procedure", true,
                """{"fr":"manutention manuelle","pl":"ręczne przemieszczanie","ro":"manipulare manuală","uk":"ручне переміщення","pt":"movimentação manual","es":"manipulación manual","lt":"rankinis krovimas","de":"manuelle Handhabung","lv":"manuāla pārvietošana"}""", now),
            CreateTerm(glossaryId, "medication administration", "procedure", true,
                """{"fr":"administration de médicaments","pl":"podawanie leków","ro":"administrarea medicamentelor","uk":"введення ліків","pt":"administração de medicação","es":"administración de medicación","lt":"vaistų skyrimas","de":"Medikamentengabe","lv":"zāļu ievadīšana"}""", now),
            CreateTerm(glossaryId, "do not administer", "prohibition", true,
                """{"fr":"ne pas administrer","pl":"nie podawać","ro":"nu administrați","uk":"не вводити","pt":"não administrar","es":"no administrar","lt":"neskirkite","de":"nicht verabreichen","lv":"nepārvaldiet"}""", now),
            CreateTerm(glossaryId, "safeguarding", "regulatory", true,
                """{"fr":"protection","pl":"ochrona","ro":"protecție","uk":"захист","pt":"salvaguarda","es":"salvaguarda","lt":"apsauga","de":"Schutzmaßnahmen","lv":"aizsardzība"}""", now),
            CreateTerm(glossaryId, "HIQA", "regulatory", true,
                """{"fr":"HIQA","pl":"HIQA","ro":"HIQA","uk":"HIQA","pt":"HIQA","es":"HIQA","lt":"HIQA","de":"HIQA","lv":"HIQA"}""", now),
            CreateTerm(glossaryId, "infection control", "procedure", true,
                """{"fr":"contrôle des infections","pl":"kontrola zakażeń","ro":"controlul infecțiilor","uk":"інфекційний контроль","pt":"controlo de infeções","es":"control de infecciones","lt":"infekcijų kontrolė","de":"Infektionskontrolle","lv":"infekciju kontrole"}""", now),
            CreateTerm(glossaryId, "do not resuscitate", "emergency", true,
                """{"fr":"ne pas réanimer","pl":"nie reanimować","ro":"nu resuscitați","uk":"не реанімувати","pt":"não reanimar","es":"no reanimar","lt":"nereanimuoti","de":"nicht wiederbeleben","lv":"nereanimēt"}""", now),
            CreateTerm(glossaryId, "lone worker", "procedure", true,
                """{"fr":"travailleur isolé","pl":"pracownik samotny","ro":"muncitor solitar","uk":"одинокий працівник","pt":"trabalhador isolado","es":"trabajador en solitario","lt":"vienas dirbantis darbuotojas","de":"Alleinarbeiter","lv":"vientuļais darbinieks"}""", now),
        ];
    }

    private static List<SafetyGlossaryTerm> CreateTransportTerms(Guid glossaryId, DateTime now)
    {
        return
        [
            CreateTerm(glossaryId, "tachograph", "regulatory", true,
                """{"fr":"tachygraphe","pl":"tachograf","ro":"tahograf","uk":"тахограф","pt":"tacógrafo","es":"tacógrafo","lt":"tachografas","de":"Tachograph","lv":"tahografs"}""", now),
            CreateTerm(glossaryId, "dangerous goods", "regulatory", true,
                """{"fr":"marchandises dangereuses","pl":"towary niebezpieczne","ro":"mărfuri periculoase","uk":"небезпечні вантажі","pt":"mercadorias perigosas","es":"mercancías peligrosas","lt":"pavojingi kroviniai","de":"Gefahrgut","lv":"bīstamas preces"}""", now),
            CreateTerm(glossaryId, "do not drive", "prohibition", true,
                """{"fr":"ne pas conduire","pl":"nie prowadzić","ro":"nu conduceți","uk":"не керуйте","pt":"não conduzir","es":"no conducir","lt":"nevairuoti","de":"nicht fahren","lv":"nebrauciet"}""", now),
            CreateTerm(glossaryId, "fatigue", "hazard", true,
                """{"fr":"fatigue","pl":"zmęczenie","ro":"oboseală","uk":"втома","pt":"fadiga","es":"fatiga","lt":"nuovargis","de":"Ermüdung","lv":"nogurums"}""", now),
            CreateTerm(glossaryId, "driving hours", "regulatory", true,
                """{"fr":"heures de conduite","pl":"czas jazdy","ro":"ore de condus","uk":"години водіння","pt":"horas de condução","es":"horas de conducción","lt":"vairavimo valandos","de":"Lenkzeiten","lv":"braukšanas stundas"}""", now),
            CreateTerm(glossaryId, "ADR", "regulatory", true,
                """{"fr":"ADR","pl":"ADR","ro":"ADR","uk":"ДОПНВГ","pt":"ADR","es":"ADR","lt":"ADR","de":"ADR","lv":"ADR"}""", now),
        ];
    }

    private static List<SafetyGlossaryTerm> CreateManufacturingTerms(Guid glossaryId, DateTime now)
    {
        return
        [
            CreateTerm(glossaryId, "ATEX", "regulatory", true,
                """{"fr":"ATEX","pl":"ATEX","ro":"ATEX","uk":"ATEX","pt":"ATEX","es":"ATEX","lt":"ATEX","de":"ATEX","lv":"ATEX"}""", now),
            CreateTerm(glossaryId, "ISO 45001", "regulatory", true,
                """{"fr":"ISO 45001","pl":"ISO 45001","ro":"ISO 45001","uk":"ISO 45001","pt":"ISO 45001","es":"ISO 45001","lt":"ISO 45001","de":"ISO 45001","lv":"ISO 45001"}""", now),
            CreateTerm(glossaryId, "lockout tagout", "procedure", true,
                """{"fr":"consignation déconsignation","pl":"blokada i oznakowanie","ro":"blocare și etichetare","uk":"блокування та маркування","pt":"bloqueio e etiquetagem","es":"bloqueo y etiquetado","lt":"užrakinimas ir ženklinimas","de":"Lockout Tagout","lv":"bloķēšana un marķēšana"}""", now),
            CreateTerm(glossaryId, "flammable", "hazard", true,
                """{"fr":"inflammable","pl":"łatwopalny","ro":"inflamabil","uk":"горючий","pt":"inflamável","es":"inflamable","lt":"degus","de":"entflammbar","lv":"uzliesmojošs"}""", now),
            CreateTerm(glossaryId, "explosive atmosphere", "hazard", true,
                """{"fr":"atmosphère explosive","pl":"atmosfera wybuchowa","ro":"atmosferă explozivă","uk":"вибухонebезпечна атмосфера","pt":"atmosfera explosiva","es":"atmósfera explosiva","lt":"sprogioji aplinka","de":"explosionsfähige Atmosphäre","lv":"sprādzienbīstama atmosfēra"}""", now),
            CreateTerm(glossaryId, "machine guarding", "equipment", true,
                """{"fr":"protège-machine","pl":"osłona maszyny","ro":"protecție mașinii","uk":"захист машини","pt":"proteção de máquina","es":"protección de máquina","lt":"mašinos apsauga","de":"Maschinenverkleidung","lv":"mašīnas aizsardzība"}""", now),
            CreateTerm(glossaryId, "do not bypass", "prohibition", true,
                """{"fr":"ne pas contourner","pl":"nie omijać","ro":"nu ocoliți","uk":"не обходити","pt":"não contornar","es":"no eludir","lt":"neapeinant","de":"nicht überbrücken","lv":"neapiet"}""", now),
            CreateTerm(glossaryId, "hazardous substance", "hazard", true,
                """{"fr":"substance dangereuse","pl":"substancja niebezpieczna","ro":"substanță periculoasă","uk":"небезпечна речовина","pt":"substância perigosa","es":"sustancia peligrosa","lt":"pavojinga medžiaga","de":"Gefahrstoff","lv":"bīstama viela"}""", now),
        ];
    }

    private static List<SafetyGlossaryTerm> CreateGeneralTerms(Guid glossaryId, DateTime now)
    {
        return
        [
            // Emergency terms
            CreateTerm(glossaryId, "emergency", "emergency", true,
                """{"fr":"urgence","pl":"nagły wypadek","ro":"urgență","uk":"надзвичайна ситуація","pt":"emergência","es":"emergencia","lt":"avarija","de":"Notfall","lv":"ārkārtas situācija"}""", now),
            CreateTerm(glossaryId, "emergency exit", "emergency", true,
                """{"fr":"sortie de secours","pl":"wyjście awaryjne","ro":"ieșire de urgență","uk":"аварійний вихід","pt":"saída de emergência","es":"salida de emergencia","lt":"avarinis išėjimas","de":"Notausgang","lv":"avārijas izeja"}""", now),
            CreateTerm(glossaryId, "emergency procedure", "emergency", true,
                """{"fr":"procédure d'urgence","pl":"procedura awaryjna","ro":"procedură de urgență","uk":"аварійна процедура","pt":"procedimento de emergência","es":"procedimiento de emergencia","lt":"avarinė procedūra","de":"Notfallverfahren","lv":"ārkārtas procedūra"}""", now),
            CreateTerm(glossaryId, "evacuation", "emergency", true,
                """{"fr":"évacuation","pl":"ewakuacja","ro":"evacuare","uk":"евакуація","pt":"evacuação","es":"evacuación","lt":"evakuacija","de":"Evakuierung","lv":"evakuācija"}""", now),

            // Prohibition terms
            CreateTerm(glossaryId, "do not", "prohibition", true,
                """{"fr":"ne pas","pl":"nie","ro":"nu","uk":"не","pt":"não","es":"no","lt":"ne","de":"nicht","lv":"ne"}""", now),
            CreateTerm(glossaryId, "do not enter", "prohibition", true,
                """{"fr":"ne pas entrer","pl":"nie wchodzić","ro":"nu intrați","uk":"не входити","pt":"não entrar","es":"no entrar","lt":"neiti","de":"nicht betreten","lv":"neieiet"}""", now),
            CreateTerm(glossaryId, "must not", "prohibition", true,
                """{"fr":"ne doit pas","pl":"nie wolno","ro":"nu trebuie","uk":"не повинен","pt":"não deve","es":"no debe","lt":"neturi","de":"darf nicht","lv":"nedrīkst"}""", now),
            CreateTerm(glossaryId, "prohibited", "prohibition", true,
                """{"fr":"interdit","pl":"zabronione","ro":"interzis","uk":"заборонено","pt":"proibido","es":"prohibido","lt":"draudžiama","de":"verboten","lv":"aizliegts"}""", now),

            // Hazard terms
            CreateTerm(glossaryId, "warning", "hazard", true,
                """{"fr":"avertissement","pl":"ostrzeżenie","ro":"avertisment","uk":"попередження","pt":"aviso","es":"advertencia","lt":"įspėjimas","de":"Warnung","lv":"brīdinājums"}""", now),
            CreateTerm(glossaryId, "danger", "hazard", true,
                """{"fr":"danger","pl":"niebezpieczeństwo","ro":"pericol","uk":"небезпека","pt":"perigo","es":"peligro","lt":"pavojus","de":"Gefahr","lv":"bīstami"}""", now),
            CreateTerm(glossaryId, "hazard", "hazard", true,
                """{"fr":"risque","pl":"zagrożenie","ro":"pericol","uk":"небезпека","pt":"perigo","es":"peligro","lt":"pavojus","de":"Gefährdung","lv":"apdraudējums"}""", now),
            CreateTerm(glossaryId, "caution", "hazard", true,
                """{"fr":"attention","pl":"uwaga","ro":"atenție","uk":"обережно","pt":"cuidado","es":"precaución","lt":"atsargiai","de":"Vorsicht","lv":"uzmanību"}""", now),

            // Regulatory terms
            CreateTerm(glossaryId, "risk assessment", "regulatory", true,
                """{"fr":"évaluation des risques","pl":"ocena ryzyka","ro":"evaluarea riscurilor","uk":"оцінка ризиків","pt":"avaliação de riscos","es":"evaluación de riesgos","lt":"rizikos vertinimas","de":"Risikobeurteilung","lv":"riska novērtējums"}""", now),

            // Equipment terms
            CreateTerm(glossaryId, "personal protective equipment", "equipment", true,
                """{"fr":"équipement de protection individuelle","pl":"środki ochrony indywidualnej","ro":"echipament individual de protecție","uk":"засоби індивідуального захисту","pt":"equipamento de proteção individual","es":"equipo de protección personal","lt":"asmeninės apsaugos priemonės","de":"persönliche Schutzausrüstung","lv":"individuālie aizsardzības līdzekļi"}""", now),
            CreateTerm(glossaryId, "PPE", "equipment", true,
                """{"fr":"EPI","pl":"ŚOI","ro":"EIP","uk":"ЗІЗ","pt":"EPI","es":"EPI","lt":"AAP","de":"PSA","lv":"IAL"}""", now),

            // Procedure terms
            CreateTerm(glossaryId, "first aid", "procedure", true,
                """{"fr":"premiers secours","pl":"pierwsza pomoc","ro":"prim ajutor","uk":"перша допомога","pt":"primeiros socorros","es":"primeros auxilios","lt":"pirmoji pagalba","de":"Erste Hilfe","lv":"pirmā palīdzība"}""", now),
        ];
    }
}
