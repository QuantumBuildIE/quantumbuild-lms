using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;

/// <summary>
/// The authored structure content for the RSA "Road Transport Regulations" document, transcribed
/// from the EUR-Lex consolidated text of Regulation (EC) No 561/2006 (drivers' hours), current
/// through Regulation (EU) 2024/1258 (source: docs/source-docs/eu-561-2006-drivers-hours.pdf).
/// 4 chapters, 9 articles, 40 provisions. Seeded as a Draft map; a human must verify it.
///
/// SEED SOURCE ONLY — see <see cref="HiqaStructureMap"/>. The DB is the runtime source of truth.
///
/// Mapping: Chapter -> principle (Roman numeral as int; the principle entity has no title field, so
/// chapter headings are not stored), Article -> standard (<c>Id</c> is the article number),
/// numbered paragraph -> feature. Where a paragraph mixes in-scope and out-of-scope content, the
/// feature is the contiguous in-scope point or subparagraph run (10(5)(a); 16(4) first two
/// subparagraphs). Articles 7 and 12 have unnumbered paragraphs, so their features are identified
/// by paragraph ordinal ("7(para 1)"), following the Regulation's own wording ("third paragraph").
/// Points and subparagraphs inside a feature are kept on their own lines (newline-separated).
///
/// Block: every feature is <see cref="RequirementBlock.Provider"/>. RSA has no person/provider
/// split; <c>Block</c> is only an internal identity key (with Identifier) for dedup and
/// map-coverage checks and is not shown to users. The label does not describe the duty holder:
/// many of these duties fall on drivers (Arts 6, 7, 8, 12, 20), not providers.
///
/// Text is verbatim from the consolidated PDF with only these changes: consolidation markers
/// (B, M1-M4, C1) and page-footer lines stripped; the leading paragraph number removed (the
/// identifier carries it); and line-wrap hyphenation rejoined where the PDF extraction left a
/// space (estab lishment, compen sation x2, under takings, under taking, Regu lation). Footnote
/// reference "(1)" in 6(5) is kept as printed; its definition is in the FootnoteDefinition field.
///
/// Excluded (not obligations on drivers or undertakings): Arts 1-4 (aim, scope, non-application,
/// definitions), 8a, 9a, 11, 13, 14, 15, 17-19, 21-29 (Member State / Commission duties,
/// exceptions, amendments, final provisions), and within included articles: 8(2a), 8(6a), 8(10),
/// 9(1), 7 para 3, 10(5)(b)-(c), 16(4) third to fifth subparagraphs, 16(5). Point (a) of 8(6a) is
/// shown as deleted in the consolidated text.
/// </summary>
public static class RsaStructureMap
{
    /// <summary>Matches RegulatoryBody.Code.</summary>
    public const string RegulatoryBodyCode = "RSA";

    /// <summary>Exact RegulatoryDocument.Title this map binds to (seeded by RegulatoryProfileSeedData).</summary>
    public const string DocumentTitle = "Road Transport Regulations";

    private const string Article6Paragraph5Footnote =
        "Regulation (EU) No 165/2014 of the European Parliament and of the Council of 4 February 2014 on tachographs in road transport, repealing Council Regulation (EEC) No 3821/85 on recording equipment in road transport and amending Regulation (EC) No 561/2006 of the European Parliament and of the Council on the harmonisation of certain social legislation relating to road transport (OJ L 60, 28.2.2014, p. 1).";

    private static StructureFeature Obligation(string id, string text, string? footnote = null) =>
        new(id, RequirementBlock.Provider, text, footnote);

    private static StructureStandard Standard(string id, params StructureFeature[] features) =>
        new(id, features);

    private static StructurePrinciple Principle(int number, params StructureStandard[] standards) =>
        new(number, standards);

    public static StructureMapSeedSource Source => new(RegulatoryBodyCode, DocumentTitle, Principles);

    public static readonly IReadOnlyList<StructurePrinciple> Principles = new[]
        {
            Principle(2,
                Standard("5",
                    Obligation("5(1)", "The minimum age for conductors shall be 18 years."),
                    Obligation("5(2)", "The minimum age for drivers' mates shall be 18 years. However, Member States may reduce the minimum age for drivers' mates to 16 years, provided that:\n(a) the carriage by road is carried out within one Member State within a 50 kilometre radius of the place where the vehicle is based, including local administrative areas the centre of which is situated within that radius;\n(b) the reduction is for the purposes of vocational training; and\n(c) there is compliance with the limits imposed by the Member State's national rules on employment matters.")),
                Standard("6",
                    Obligation("6(1)", "The daily driving time shall not exceed nine hours.\nHowever, the daily driving time may be extended to at most 10 hours not more than twice during the week."),
                    Obligation("6(2)", "The weekly driving time shall not exceed 56 hours and shall not result in the maximum weekly working time laid down in Directive 2002/15/EC being exceeded."),
                    Obligation("6(3)", "The total accumulated driving time during any two consecutive weeks shall not exceed 90 hours."),
                    Obligation("6(4)", "Daily and weekly driving times shall include all driving time on the territory of the Community or of a third country."),
                    Obligation("6(5)", "A driver shall record as other work any time spent as described in point (e) of Article 4 as well as any time spent driving a vehicle used for commercial operations that do not fall within the scope of this Regulation, and shall record any periods of availability, as defined in point (b) of Article 3 of Directive 2002/15/EC, in accordance with point (b)(iii) of Article 34(5) of Regulation (EU) No 165/2014 of the European Parliament and of the Council (1). This record shall be entered either manually on a record sheet or printout or by use of manual input facilities on recording equipment.", Article6Paragraph5Footnote)),
                Standard("7",
                    Obligation("7(para 1)", "After a driving period of four and a half hours a driver shall take an uninterrupted break of not less than 45 minutes, unless he takes a rest period."),
                    Obligation("7(para 2)", "This break may be replaced by a break of at least 15 minutes followed by a break of at least 30 minutes each distributed over the period in such a way as to comply with the provisions of the first paragraph."),
                    Obligation("7(para 4)", "A driver engaged in multi-manning may take a break of 45 minutes in a vehicle driven by another driver provided that the driver taking the break is not involved in assisting the driver driving the vehicle.")),
                Standard("8",
                    Obligation("8(1)", "A driver shall take daily and weekly rest periods."),
                    Obligation("8(2)", "Within each period of 24 hours after the end of the previous daily rest period or weekly rest period a driver shall have taken a new daily rest period.\nIf the portion of the daily rest period which falls within that 24 hour period is at least nine hours but less than 11 hours, then the daily rest period in question shall be regarded as a reduced daily rest period."),
                    Obligation("8(3)", "A daily rest period may be extended to make a regular weekly rest period or a reduced weekly rest period."),
                    Obligation("8(4)", "A driver may have at most three reduced daily rest periods between any two weekly rest periods."),
                    Obligation("8(5)", "By way of derogation from paragraph 2, within 30 hours of the end of a daily or weekly rest period, a driver engaged in multi-manning must have taken a new daily rest period of at least nine hours."),
                    Obligation("8(6)", "In any two consecutive weeks a driver shall take at least:\n(a) two regular weekly rest periods; or\n(b) one regular weekly rest period and one reduced weekly rest period of at least 24 hours.\nA weekly rest period shall start no later than at the end of six 24-hour periods from the end of the previous weekly rest period.\nBy way of derogation from the first subparagraph, a driver engaged in international transport of goods may, outside the Member State of establishment, take two consecutive reduced weekly rest periods provided that the driver in any four consecutive weeks takes at least four weekly rest periods, of which at least two shall be regular weekly rest periods.\nFor the purpose of this paragraph, a driver shall be considered to be engaged in international transport where the driver starts the two consecutive reduced weekly rest periods outside the Member State of the employer's establishment and the country of the drivers' place of residence."),
                    Obligation("8(6b)", "Any reduction in weekly rest period shall be compensated by an equivalent period of rest taken en bloc before the end of the third week following the week in question.\nWhere two reduced weekly rest periods have been taken consecutively in accordance with the third subparagraph of paragraph 6, the next weekly rest period shall be preceded by a rest period taken as compensation for those two reduced weekly rest periods."),
                    Obligation("8(7)", "Any rest taken as compensation for a reduced weekly rest period shall be attached to another rest period of at least nine hours."),
                    Obligation("8(8)", "The regular weekly rest periods and any weekly rest period of more than 45 hours taken in compensation for previous reduced weekly rest periods shall not be taken in a vehicle. They shall be taken in suitable gender-friendly accommodation with adequate sleeping and sanitary facilities.\nAny costs for accommodation outside the vehicle shall be covered by the employer."),
                    Obligation("8(8a)", "Transport undertakings shall organise the work of drivers in such a way that the drivers are able to return to the employer's operational centre where the driver is normally based and where the driver's weekly rest period begins, in the Member State of the employer's establishment, or to return to the drivers' place of residence, within each period of four consecutive weeks, in order to spend at least one regular weekly rest period or a weekly rest period of more than 45 hours taken in compensation for reduced weekly rest period.\nHowever, where the driver has taken two consecutive reduced weekly rest periods in accordance with paragraph 6, the transport undertaking shall organise the work of the driver in such a way that the driver is able to return before the start of the regular weekly rest period of more than 45 hours taken in compensation.\nThe undertaking shall document how it fulfils that obligation and shall keep the documentation at its premises in order to present it at the request of control authorities."),
                    Obligation("8(9)", "A weekly rest period that falls in two weeks may be counted in either week, but not in both.")),
                Standard("9",
                    Obligation("9(2)", "Any time spent travelling to a location to take charge of a vehicle falling within the scope of this Regulation, or to return from that location, when the vehicle is neither at the driver's home nor at the employer's operational centre where the driver is normally based, shall not be counted as a rest or break unless the driver is on a ferry or train and has access to a sleeper cabin, bunk or couchette."),
                    Obligation("9(3)", "Any time spent by a driver driving a vehicle which falls outside the scope of this Regulation to or from a vehicle which falls within the scope of this Regulation, which is not at the driver's home or at the employer's operational centre where the driver is normally based, shall count as other work."))),
            Principle(3,
                Standard("10",
                    Obligation("10(1)", "A transport undertaking shall not give drivers it employs or who are put at its disposal any payment, even in the form of a bonus or wage supplement, related to distances travelled, the speed of delivery and/or the amount of goods carried if that payment is of such a kind as to endanger road safety and/or encourages infringement of this Regulation."),
                    Obligation("10(2)", "A transport undertaking shall organise the work of drivers referred to in paragraph 1 in such a way that the drivers are able to comply with Regulation (EEC) No 3821/85 and Chapter II of this Regulation. The transport undertaking shall properly instruct the driver and shall make regular checks to ensure that Regulation (EEC) No 3821/85 and Chapter II of this Regulation are complied with."),
                    Obligation("10(3)", "A transport undertaking shall be liable for infringements committed by drivers of the undertaking, even if the infringement was committed on the territory of another Member State or a third country.\nWithout prejudice to the right of Member States to hold transport undertakings fully liable, Member States may make this liability conditional on the undertaking's infringement of paragraphs 1 and 2. Member States may consider any evidence that the transport undertaking cannot reasonably be held responsible for the infringement committed."),
                    Obligation("10(4)", "Undertakings, consignors, freight forwarders, tour operators, principal contractors, subcontractors and driver employment agencies shall ensure that contractually agreed transport time schedules respect this Regulation."),
                    Obligation("10(5)(a)", "(a) A transport undertaking which uses vehicles that are fitted with recording equipment complying with Annex IB of Regulation (EEC) No 3821/85 and that fall within the scope of this Regulation, shall:\n(i) ensure that all data are downloaded from the vehicle unit and driver card as regularly as is stipulated by the Member State and that relevant data are downloaded more frequently so as to ensure that all data concerning activities undertaken by or for that undertaking are downloaded;\n(ii) ensure that all data downloaded from both the vehicle unit and driver card are kept for at least 12 months following recording and, should an inspecting officer request it, such data are accessible, either directly or remotely, from the premises of the undertaking;"))),
            Principle(4,
                Standard("12",
                    Obligation("12(para 1)", "Provided that road safety is not thereby jeopardised and to enable the vehicle to reach a suitable stopping place, the driver may depart from Articles 6 to 9 to the extent necessary to ensure the safety of persons, of the vehicle or its load. The driver shall indicate the reason for such departure manually on the record sheet of the recording equipment or on a printout from the recording equipment or in the duty roster, at the latest on arrival at the suitable stopping place."),
                    Obligation("12(para 2)", "Provided that road safety is not thereby jeopardised, in exceptional circumstances, the driver may also depart from Article 6(1) and (2) and Article 8(2) by exceeding the daily and weekly driving time by up to one hour in order to reach the employer's operational centre or the driver's place of residence to take a weekly rest period."),
                    Obligation("12(para 3)", "Under the same conditions, the driver may exceed the daily and weekly driving time by up to two hours, provided that an uninterrupted break of 30 minutes was taken immediately prior to the additional driving in order to reach the employer's operational centre or the driver's place of residence for taking a regular weekly rest period."),
                    Obligation("12(para 4)", "The driver shall indicate the reason for such departure manually on the record sheet of the recording equipment, or on a printout from the recording equipment or in the duty roster, at the latest on arrival at the destination or the suitable stopping place."),
                    Obligation("12(para 5)", "Any period of extension shall be compensated by an equivalent period of rest taken en bloc with any rest period, by the end of the third week following the week in question."))),
            Principle(5,
                Standard("16",
                    Obligation("16(1)", "Where no recording equipment has been fitted to the vehicle in accordance with Regulation (EEC) No 3821/85, paragraphs 2 and 3 of this Article shall apply to:\n(a) regular national passenger services, and\n(b) regular international passenger services whose route terminals are located within a distance of 50 km as the crow flies from a border between two Member States and whose route length does not exceed 100 km."),
                    Obligation("16(2)", "A service timetable and a duty roster shall be drawn up by the transport undertaking and shall show, in respect of each driver, the name, place where he is based and the schedule laid down in advance for various periods of driving, other work, breaks and availability.\nEach driver assigned to a service referred to in paragraph 1 shall carry an extract from the duty roster and a copy of the service timetable."),
                    Obligation("16(3)", "The duty roster shall:\n(a) include all the particulars specified in paragraph 2 for a minimum period covering the day of control and the previous 56 days; those particulars must be updated on regular intervals, the duration of which may not exceed one month;\n(b) be signed by the head of the transport undertaking or by a person authorised to represent him;\n(c) be kept by the transport undertaking for one year after expiry of the period covered by it. The transport undertaking shall give an extract from the roster to the drivers concerned upon request; and\n(d) be produced and handed over at the request of an authorised inspecting officer."),
                    Obligation("16(4)", "For the purpose of roadside checks, until a digital journey form is available, the driver shall be able to justify the use of the derogations under Article 7, third paragraph, and Article 8(2a) and (6a) by:\n(a) carrying on board the vehicle a completed journey form, containing the information required in accordance with Regulation (EC) No 1073/2009, which the transport undertaking shall be responsible for providing the driver with prior to each journey; and\n(b) carrying on board the vehicle paper or electronic copies of such journey forms which cover the previous 28 days, and, from 31 December 2024, the previous 56 days.\nThe obligation in point (b) of the first subparagraph shall cease to apply at the latest when the vehicle uses a tachograph allowing the recording of the type of passenger service referred to in paragraph 5.")),
                Standard("20",
                    Obligation("20(1)", "The driver shall keep any evidence provided by a Member State concerning penalties imposed or the initiation of proceedings until such time as the same infringement of this Regulation can no longer lead to a second proceeding or penalty pursuant to this Regulation."),
                    Obligation("20(2)", "The driver shall produce the evidence referred to in paragraph 1 upon request."),
                    Obligation("20(3)", "A driver who is employed or at the disposal of more than one transport undertaking shall provide sufficient information to each undertaking to enable it to comply with Chapter II.")))
        };
}
