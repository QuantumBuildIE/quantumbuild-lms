using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;

/// <summary>
/// The authored, ground-truth structure content for HIQA's "Draft National Standards for Home
/// Support Services" (November 2024) — the single document targeted by
/// docs/faithful-extraction-build-recon.md's Deliverable 1 inventory. 17 standards across 4
/// principles, 83 person-experience features + 68 provider features = 151 total, reconciled
/// exactly against that recon's independently-derived count.
///
/// SEED SOURCE ONLY — not the runtime source of truth. RegulatoryStructureMapSeedData reads
/// <see cref="Principles"/> once (idempotently) and writes it into the DB-backed
/// RegulatoryStructureMap tree as a Draft map. At runtime, IRegulatoryStructureMapProvider reads
/// exclusively from the DB — this class is never consulted after the seed has run. This keeps
/// exactly one runtime source of truth (the DB) while preserving the authored content here as the
/// human-reviewable origin of that seed.
///
/// Every <see cref="StructureFeature.Identifier"/> and <see cref="StructureFeature.VerbatimText"/>
/// below is copied verbatim from the recon's Deliverable 1 transcription (itself cross-checked
/// against the source PDF's own printed page numbers). Structural irregularities are preserved
/// exactly as the recon documents them, not normalised:
///   - Standard 2.3's provider block is numbered 2.3.2-2.3.8 — there is no printed 2.3.1 in that
///     block. This is the document's own numbering, confirmed present in the source itself.
///   - Standard 4.5 uses trailing-period identifiers ("4.5.1.", not "4.5.1") uniquely among all
///     17 standards. The trailing period is kept literally.
///   - Count asymmetry between blocks (e.g. Standard 4.1: 5 person / 8 provider; Standard 3.2: 3
///     person / 2 provider) is the document's own content, not a transcription error to "fix".
///
/// Footnote definitions are attached to the requirement that references the defined term (edge
/// decision 2), where the recon supplies the footnote's verbatim text:
///   - 1.1.4 and 4.1.5 (person) both reference the same Equal Status Act "nine grounds" footnote
///     — the document itself repeats this footnote verbatim on two different pages.
///   - 1.3.6 (person) references the "decision supporter" definition.
///   - 2.3.5 (person) references the "collection agent" definition.
/// One known gap: provider feature 4.5.5.'s "telecare" footnote is confirmed to exist by the
/// recon but its verbatim text was not captured (the recon's own extraction pass could not
/// reliably resolve the footnote marker glyph for it) — left null here rather than invented.
/// Closing this gap needs a manual re-read of the source PDF, not a code change.
/// </summary>
public static class HiqaStructureMap
{
    /// <summary>Matches RegulatoryBody.Code — used by RegulatoryStructureMapSeedData to locate the HIQA RegulatoryDocument row to seed against.</summary>
    public const string RegulatoryBodyCode = "HIQA";

    private const string EqualStatusActFootnote =
        "The Equal Status Act 2000-2015 (the Acts) prohibit discrimination in the provisions of goods and services, accommodation and education. They cover the nine grounds of gender, marital status, family status, age, disability, sexual orientation, race, religion and membership of the Traveller Community.";

    private const string DecisionSupporterFootnote =
        "Decision supporter: means a person defined in accordance with the Assisted Decision-Making (Capacity) Act 2015, 2022 whose legal authority is based on their registration status with the decision support service, that is decision-making assistant, co-decision-maker, decision-making representative, attorney, designated healthcare representative.";

    private const string CollectionAgentFootnote =
        "A collection agent means a person who collects, on behalf of a person using a service, a payment due to that person, including, but not limited to, payments under the Social Welfare (Consolidation) Act, 2005.";

    private static StructureFeature Person(string id, string text, string? footnote = null) =>
        new(id, RequirementBlock.Person, text, footnote);

    private static StructureFeature Provider(string id, string text, string? footnote = null) =>
        new(id, RequirementBlock.Provider, text, footnote);

    private static StructureStandard Standard(string id, params StructureFeature[] features) =>
        new(id, features);

    private static StructurePrinciple Principle(int number, params StructureStandard[] standards) =>
        new(number, standards);

    public static readonly IReadOnlyList<StructurePrinciple> Principles = new[]
        {
            Principle(1,
                Standard("1.1",
                    Person("1.1.1", """My human rights are clearly communicated to me by the service provider in a way that meets my needs, and I am supported to understand and realise my human rights in a way that best suits me."""),
                    Person("1.1.2", """I am confident that staff will recognise if I need additional help and support to ensure my human rights are upheld or to get the care and support I need. I am provided with information regarding decision and advocacy support services that can support me to realise my human rights, express my views or access the services I need."""),
                    Person("1.1.3", """I am confident that staff providing my care and support recognise that my home is my personal space and they respect my home environment and my right to live as I choose."""),
                    Person("1.1.4", """My values, beliefs and way of life are respected by the staff caring for me and I am not treated differently to other people receiving home support for any reason.""", EqualStatusActFootnote),
                    Person("1.1.5", """I am recognised as an individual and staff communicate with me in a respectful way. I experience kindness and compassion when using home support services."""),
                    Person("1.1.6", """I am supported to complete everyday tasks and activities myself rather than my home support worker carrying them out for me."""),
                    Person("1.1.7", """My privacy and dignity are respected and protected when delivering home support, particularly with personal and intimate care."""),
                    Person("1.1.8", """My information is stored safely and securely in line with legislation, so it cannot be seen by people who do not need to see it. I am confident that my personal support plan is kept in a safe place in my house and I know who has access to it. The sharing of my personal information is carried out in a way that respects my rights."""),
                    Provider("1.1.1", """The service provider places human rights at the centre of its governance, management, culture and delivery of care and support. The service provider ensures that human rights principles are considered in the development of all policies, procedures and practices in order to protect, promote and uphold the human rights of people using services, as set out in legislation and national policy. These policies and procedures are implemented in practice and are regularly reviewed."""),
                    Provider("1.1.2", """The service provider has agreed processes in place to ensure that people using services are informed and aware of relevant advocacy services that can support them to achieve their human rights, express their views or access the services. People using services are supported to access these services, as necessary."""),
                    Provider("1.1.3", """The service provider has systems in place to ensure that the personal information of people using the service is protected at all times, in line with legislation and best practice.""")),
                Standard("1.2",
                    Person("1.2.1", """The home support service I receive is based on my assessed needs and I do not experience discrimination of any kind."""),
                    Person("1.2.2", """I can easily access information about the home support services available to me, how to apply for a service, any eligibility requirements and if there are any direct financial costs to me. This information is easy to understand, and is available in a way that suits my needs."""),
                    Person("1.2.3", """Accessible modes and formats of communication with my service provider are available to me."""),
                    Person("1.2.4", """Any forms that I, or my family or advocate, need to complete when applying for and using the home support service are user-friendly and we can receive help to complete the forms, if we need it."""),
                    Person("1.2.5", """My communication needs and abilities, and where relevant that of my family, are acknowledged and supported by the service. For example, if I need information provided in a different format or language, my service provider does all it can to meet my needs."""),
                    Provider("1.2.1", """The service provider ensures that information on the home support services that are available, the process for accessing these services and any direct financial costs for these services, is provided to people using the service in a timely fashion."""),
                    Provider("1.2.2", """The service provider ensures that access for those using the service is based on the individual's needs assessment, and is in line with relevant eligibility criteria."""),
                    Provider("1.2.3", """The service provider proactively identifies the diversity of needs of the population served, including their physical, sensory, cultural and language needs, and puts arrangements in place to meet these needs and support its service users, in line with relevant legislation.""")),
                Standard("1.3",
                    Person("1.3.1", """I am respected as the expert on my own life and supported to make decisions relating to my home support and be involved in planning my care and support as much as possible. My care and support focuses on what is important to me, how I want to live, and what support I need to achieve my goals."""),
                    Person("1.3.2", """Staff communicate with me effectively, listen to me and seek my views to make sure their understanding of my needs, preferences and goals are up to date."""),
                    Person("1.3.3", """I have the relevant information to help me to participate in decisions in a timely way."""),
                    Person("1.3.4", """I know that staff will use plain language that I understand when talking to me about my home support. I am encouraged to ask questions and staff check that I understand the information. I am given sufficient time to consider the information given and all available choices."""),
                    Person("1.3.5", """I am confident that staff will recognise if I need additional help and support to make a decision and provide me with information on how to access this additional decision support."""),
                    Person("1.3.6", """I, and where relevant my decision supporters, participate in decision-making around my care and support, particularly relating to how this will be provided, when it will be provided and by whom.""", DecisionSupporterFootnote),
                    Person("1.3.7", """If my views and preferences for my care and support are in conflict with my family's views and preferences, I know that staff will respect my wishes and support my autonomy."""),
                    Person("1.3.8", """My service provider prepares a service agreement with me that sets out the home support services that will be provided to me and arrangements for how the service is delivered. This agreement is expressed in a way that I can understand and in a format that meets my needs. Any changes to this service agreement are agreed by me and the service provider before they come into effect."""),
                    Provider("1.3.1", """The need to support people to participate in and make decisions about their home support, and to ensure people have the relevant information they need to do so is reflected in the service provider's policies and procedures. The service provider ensures that these policies and procedures are informed by decision-making legislation, are implemented in practice and regularly reviewed and updated."""),
                    Provider("1.3.2", """Service agreements are prepared with all people who are using the services. These agreements are worded in clear language and are provided in a format that is understandable and best suited to the person using the service.""")),
                Standard("1.4",
                    Person("1.4.1", """I understand that I have a right to express my opinion on the service and how staff care for and support me. I am encouraged and supported to provide feedback on the home support service and on the care and support I receive."""),
                    Person("1.4.2", """I am provided with a safe place and space to express my views when giving feedback. For example, I can provide feedback anonymously if I prefer to do so."""),
                    Person("1.4.3", """I know how to make a complaint as I am provided with my service provider's complaints policy in my preferred format. This clearly outlines the mechanism for complaints and independent appeals process. I am informed about independent advocacy services that can support me when making a complaint."""),
                    Person("1.4.4", """If I need to make a complaint, I am supported to do so and I am reassured that there will be no negative consequences to the care and support I receive. I am confident that any concerns that I express about my care and support or any complaints that I make will be responded to and addressed at the earliest opportunity to minimise the impact on me and others."""),
                    Person("1.4.5", """I am informed of the outcome of any complaint I make. If there is a delay, staff keep me up to date. I can request an explanation if I am unhappy with the outcome of my complaint, without concern of repercussions."""),
                    Provider("1.4.1", """The service provider has mechanism in place to receive feedback from service users"""),
                    Provider("1.4.2", """The service provider has a complaints policy and clear, transparent, open and accessible arrangements in place to invite, receive, review and respond to any complaints or concerns about the services provided. These arrangements take account of legislation, relevant regulations, national guidelines and best available evidence."""),
                    Provider("1.4.3", """The service provider addresses complaints and concerns promptly, effectively and fairly, while supporting service users throughout the process and if necessary facilitating them to access support or independent advocacy services."""),
                    Provider("1.4.4", """The service provider ensures that people who make a complaint are not disadvantaged in any way. There is a fair and timely appeals procedure that is consistent with relevant legislation, regulations and best practice guidelines."""))),
            Principle(2,
                Standard("2.1",
                    Person("2.1.1", """My home support needs are assessed and reviewed with me in a standardised way to ensure I receive the right care and support at the right time. This includes a comprehensive assessment of my health, physical, sensory, emotional and social care needs as well as identification of my preferences, strengths and goals."""),
                    Person("2.1.2", """My needs assessment has a focus on optimising my quality of life, strengths, skills and interests through meaningful activities that are based on my preferences and goals."""),
                    Person("2.1.3", """I can make decisions about whether family, friends, carers or others, such as advocates, are involved in my support. If care and support is also provided to me by family members or friends, service providers work to support positive interactions between home support workers and informal caregivers."""),
                    Person("2.1.4", """The service provider informs me of the process for seeking a reassessment, should my circumstances or needs change."""),
                    Provider("2.1.1", """The service provider ensures an evidence-based assessment tool is used to assess the needs of the person using the service, in collaboration with that person. This includes a comprehensive assessment of the health, physical, sensory, emotional and social care needs of the person using the service."""),
                    Provider("2.1.2", """The service provider ensures that the needs assessment has a focus on optimising the independence, health, wellbeing and quality of life of the person using the service, in accordance with their identified needs, strengths and stated goals and preferences."""),
                    Provider("2.1.3", """The service provider has arrangements in place to respond to changes in the home support requirements of the individual using the service and discusses with them and the HSE as commissioner of services (where applicable) when a re-assessment is needed.""")),
                Standard("2.2",
                    Person("2.2.1", """I experience high-quality care and support because my home support workers have the necessary information and resources to support me."""),
                    Person("2.2.2", """I am given the choice to be fully involved in developing and reviewing my personal support plan. My personal support plan is right for me because it sets out how my needs will be met, as well as my strengths, goals and preferences. The support required to achieve these is clearly documented and communicated to those providing my care and support."""),
                    Person("2.2.3", """I am confident that, when implementing my personal support plan, the provision of any service is consistent with and contributes to meeting my assessed needs, goals and preferences. My care and support is provided in a planned and safe way, including if there is an emergency or unexpected event."""),
                    Person("2.2.4", """The service provider agrees the timings of my home support visits with me and they are arranged to enable my daily activities and routines."""),
                    Person("2.2.5", """I am treated as an individual by people who respect my needs, choices and preferences. I am empowered and enabled to be as independent and in control of my life as I want and can be."""),
                    Person("2.2.6", """I can maintain and develop my interests, activities and what matters to me, in the way that I like and these are included in my personal support plan. This includes being supported to continue to participate fully as a citizen in my community in the way that I want. If this involves some element of risk, this has been discussed and agreed with me and is included in my personal support plan."""),
                    Person("2.2.7", """If I am receiving support with my nutrition and hydration either by meal provision, assistance to eat or drink, shopping or preparing food - food choices are in line with my preferences and dietary plan or nutritional needs for maintaining my health and wellbeing."""),
                    Person("2.2.8", """My personal support plan is updated in accordance with the outcomes I achieve, my assessed or re-assessed needs and my home support requirements."""),
                    Provider("2.2.1", """The service provider has a policy in place which outlines the process for the development and review of a personal support plan with the person using the service, based on their individual needs assessment. This includes how their families, carers or advocates have been included in the review in accordance with the preferences of the person using the service."""),
                    Provider("2.2.2", """The service provider ensures that each person using the service has an up-to-date personal support plan developed in partnership with the person using the service. The service provider ensures that the support plan is easy-to read and accessible to the person using the service, home support worker and if applicable, other health and social care professionals involved in their care and support."""),
                    Provider("2.2.3", """The service provider ensures that the development of personal support plans have a focus on optimising the independence, health, wellbeing and quality of life of the person using the service in accordance with their identified needs, strengths and goals and preferences."""),
                    Provider("2.2.4", """The service provider has a system in place to ensure that the timing of home support visits are agreed with the person using the service and arranged so that they fit in with individual's needs, enable their daily activities and routines and, where relevant and possible, coordinates with informal carers. These timings are documented in the personal support plan and monitored in practice."""),
                    Provider("2.2.5", """Personal support plans are implemented and monitored by the service provider to ensure they are delivered in accordance with the needs of the person using a service. The service provider ensures that regular reviews of personal support plans take place with the person using the service, and that plans are updated in accordance with outcomes achieved, the individual's changing needs and home support requirements.""")),
                Standard("2.3",
                    Person("2.3.1", """I am confident that my service provider works to protect me from all forms of abuse including coercion, harassment, physical (including neglect), emotional (including bullying), sexual, financial or other exploitation."""),
                    Person("2.3.2", """The service provider and staff understand their role and responsibilities in protecting me from harm. This includes following legislation, standards, guidance and policies that help to keep me safe, as well as knowing the correct way to report any concerns they may have about me or my care and support."""),
                    Person("2.3.3", """I am listened to and taken seriously if I have a concern about the protection and safety of myself or others."""),
                    Person("2.3.4", """Staff respect the place where I receive care and support as my home and respect the security of my home and my possessions."""),
                    Person("2.3.5", """I am confident that staff are working in line with financial policies and procedures including, for example, that staff working in my home support service will not act as my collection agent nor do they ask for or try to obtain loans or gifts from me.""", CollectionAgentFootnote),
                    Person("2.3.6", """I am confident that staff know what to look out for to keep me safe. My home support worker is alert to and responds to signs of any significant changes in my health and wellbeing."""),
                    Person("2.3.7", """The home support worker(s) who support me, create an environment that is safe and is the least restrictive possible, and I am confident that they are trained to do this."""),
                    // Provider block is document-native 2.3.2-2.3.8 — there is no printed 2.3.1 provider item.
                    Provider("2.3.2", """The service provider has a range of policies and procedures in place to support the safety and wellbeing of people who use the service and to ensure the security, safety and protection of the individual and their home when the service is being delivered."""),
                    Provider("2.3.3", """The service provider has an up-to-date, person-centred safeguarding policy and associated processes and procedures in place, which are in line with relevant national standards, legislation, regulations, national policy, procedures and best practice guidance. These clearly set out the roles and responsibilities of the service provider and staff in identifying and managing safeguarding concerns and are consistently implemented across the service in a timely way."""),
                    Provider("2.3.4", """The service provider has a clearly defined reporting pathway for the person using the service and home support worker where safeguarding concerns arise. This is supported by clear policies and procedures to facilitate timely communication between the service provider and other relevant services and professionals (including up-to-date contact and or organisational details) to ensure people are safe, especially when there is an immediate risk to a person."""),
                    Provider("2.3.5", """Staff are trained and supported to understand their role and responsibilities in safeguarding people who are receiving home support services. For example, service providers ensure that home support workers have completed safeguarding awareness training that includes the recognition and reporting of suspected abuse and the recognition of (signs of) self-neglect and making protected disclosures about the home support service."""),
                    Provider("2.3.6", """The service provider ensures that the system of supervision and development for staff includes safeguarding as a core component."""),
                    Provider("2.3.7", """The service provider has a system in place to successfully implement learnings from investigations into safeguarding concerns."""),
                    Provider("2.3.8", """Service providers have policies, systems and processes in place to ensure that people using a service are free from the use of any unnecessary restrictive practices in the provision of home support services. Service providers monitor, record and review the use of any restrictive practices included in a personal support plan in line with any assessed needs.""")),
                Standard("2.4",
                    Person("2.4.1", """I am confident that my provider has arrangements in place to identify and address any potential risks to me in the delivery of my care and support."""),
                    Person("2.4.2", """I know that staff take all the precautions they can to prevent the risk of transmission of infection and have been trained to do so."""),
                    Person("2.4.3", """The service provider works with other services when I am transferring from one service to another, for example between hospital and home or from one home support service to another, to plan, coordinate and manage my transfer effectively."""),
                    Person("2.4.4", """If I need help with my medication, I am confident that staff can support me to manage my medication safely, as set out in my personal support plan. This may include collecting prescriptions and or prescribed medicines, prompting me if necessary regarding the timing of medication, assisting me to take prescribed medication, and observing for medication missed doses or errors."""),
                    Provider("2.4.1", """The service provider has arrangements in place to proactively identify and assess areas of home support delivery where there may be an increased risk of harm to the person using the service. These areas may include, but are not limited to, transitions of care, infection prevention and control, medication support, use of equipment, restrictive practices, deterioration of condition and falls prevention. Service providers put structured arrangements in place to identify and minimise these risks."""),
                    Provider("2.4.2", """The service provider has an infection prevention and control policy in place, in line with national standards and guidance. Staff are trained in relevant infection prevention and control practices. This includes, for example, adhering to policies and procedures, practising good hand hygiene and respiratory and cough etiquette, transmission-based precautions and the safe use of personal protective equipment."""),
                    Provider("2.4.3", """Staff have access to adequate supplies of personal protective equipment to meet the circumstances of the person using the service and know how to use and dispose of it correctly."""),
                    Provider("2.4.4", """The service provider has an up-to-date policy on medication support and monitors adherence to the policy, taking appropriate action where safety risks are identified. The service provider ensures that home support workers who undertake medication management support receive appropriate training and are competent to do so.""")),
                Standard("2.5",
                    Person("2.5.1", """Staff communicate with me in an open, honest, timely and compassionate manner if something goes wrong during my care and support. I am confident that if something goes wrong in my care and support, my service provider communicates with me openly and honestly and involves me in the review of any incident."""),
                    Person("2.5.2", """I am confident that the outcome of any review that may take place is available to me and any learning from the review is used to help improve the service."""),
                    Provider("2.5.1", """The service provider has robust arrangements in place, including policies, procedures and staff training, so that staff can identify, respond to, report, review and learn from incidents, in line with national standards, legislation, policy, guidelines and guidance."""),
                    Provider("2.5.2", """The service provider and staff communicate openly and honestly with people if something goes wrong in their home support and involves them in the review of any incidents. The outcome of any review that may take place and any action arising from the review is made available to the person using the service."""))),
            Principle(3,
                Standard("3.1",
                    Person("3.1.1", """I experience continuity of care and support from the same team of staff. I know who will provide my care and support on a day-to-day basis and what they are expected to do."""),
                    Person("3.1.2", """Staff take the time to develop a relationship with me and listen to me, in order to get to know me and what is important to me. They speak and listen to me in a way that is courteous and respectful, with my care and support being the main focus of their attention"""),
                    Person("3.1.3", """I am made aware of the circumstances when an alternative home support worker may be required to provide care or support to me. If there is a change due to unforeseen circumstance or planned leave, my service provider notifies me in advance, in a way that suits my needs."""),
                    Person("3.1.4", """I am supported and cared for in a sensitive manner by people who know me and my circumstances. They can anticipate issues that may arise for me and are aware of and plan for any known vulnerability or frailty that I may experience."""),
                    Person("3.1.5", """I am confident that staff advocate for support that is tailored to my individual needs and circumstances and is delivered in the right way, at the right time, and for as long as required."""),
                    Provider("3.1.1", """The service provider ensures that there are sufficient staff with the right skills and levels of experience to provide consistent care and support to each person using a service, in line with the requirements of the service being provided and the needs of the person."""),
                    Provider("3.1.2", """The service provider has safe and effective systems, strategies, policies and procedures in place to recruit and retain home support workers who are sufficiently competent, skilled and experienced to build trusting relationships and meet the needs of the person using the service."""),
                    Provider("3.1.3", """Service providers ensure that staff receive training on effective communication and have the ability to communicate with people using the service in a meaningful way that best suits their needs."""),
                    Provider("3.1.4", """Staff take their time to build a trusting relationship with the person, in order to understand and respond to the person's needs in a timely way."""),
                    Provider("3.1.5", """The service provider has a system in place to ensure continuity of care. People using the service are notified in advance when a home support worker previously unknown to them is assigned to deliver their home support. The service provider has contingency plans in place in the event that a home support worker cannot attend at a person's home as agreed.""")),
                Standard("3.2",
                    Person("3.2.1", """My care and support is consistent and reliable because staff are supported to work together well and learn from each other to ensure the best outcomes for me are achieved. I experience kind and compassionate care and support because there are good working relationships."""),
                    Person("3.2.2", """I am involved in planning and managing any move between different home support services. I receive home support that is well coordinated and flexible enough to suit my changing needs and reduce the risk of harm to me during any transition period."""),
                    Person("3.2.3", """I receive appropriate notice if the service I use can no longer meet my needs and wishes."""),
                    // Standard 3.2 breaks from 3.1/3.3's provider-heading wording ("Features of a service
                    // provider..." like Principles 1/4, not "Features of service provider..." like every
                    // other Principle 2/3 standard) — content/attribution below is unaffected; noted only
                    // because a future heading-based parser must not treat the wording as a per-principle rule.
                    Provider("3.2.1", """The service provider has arrangements in place to ensure that people using the service receive care and support that is integrated within and between home support services. The service provider supports staff to work together to achieve continuity of care for those using the service."""),
                    Provider("3.2.2", """Staff are supported and trained to understand their role and responsibilities in advocating for people using the service within and between services, to ensure that people get the right services in a way that meets their needs.""")),
                Standard("3.3",
                    Person("3.3.1", """I receive safe and high-quality care and support that meets my needs, supports my wellbeing and is based on the best available evidence."""),
                    Person("3.3.2", """I am confident that staff who support and care for me have been recruited in line with the relevant policies and procedures."""),
                    Person("3.3.3", """Staff supporting me are competent and have the qualifications, skills, knowledge and experience necessary to care for and support me effectively with empathy and compassion."""),
                    Person("3.3.4", """I am confident that staff are supported in their role and receive regular and ongoing supervision."""),
                    Person("3.3.5", """I am confident that staff receive regular training and education to retain, reflect and build on new skills and knowledge to provide the best care and support to meet my needs. I am confident that staff are given time and support from their employers to put their learning into practice."""),
                    Provider("3.3.1", """The service provider has a workforce recruitment and retention plan that is regularly reviewed and updated. This plan sets out the staffing levels to ensure adequate cover, skill-mix, competencies, experience and capabilities required to meet the needs of people using the home support service. The service provider monitors and evaluates the effectiveness of recruitment processes and addresses identified gaps."""),
                    Provider("3.3.2", """The service provider ensures that the workforce has the skills required to support people using the service, through regular staff training needs analysis and taking appropriate action to address any knowledge gaps and training required. This includes matching skills within the workforce with individual needs assessments of people using the service."""),
                    Provider("3.3.3", """The service provider ensures that all new home support workers complete induction training. This process includes ensuring that all home support workers are supervised by a suitably experienced worker as part of the practical training and are formally assessed and deemed competent prior to working alone."""),
                    Provider("3.3.4", """Staff are supported to understand their roles and responsibilities and work in line with relevant legislation, regulations and standards, as well as national and local policies and procedures at all times. The performance of staff is assessed at regular specified intervals and all staff receive support and supervision to ensure that they perform their role to the best of their ability."""),
                    Provider("3.3.5", """The service provider demonstrates a commitment to the continuous professional development of the workforce through the development and implementation of an annual training programme and by facilitating staff to achieve or maintain relevant care and support qualifications and training to address the identified needs of people using the service. The service provider ensures that staff are supported through, for example, education, training and opportunities for reflective practice."""))),
            Principle(4,
                Standard("4.1",
                    Person("4.1.1", """I know what my home support service does, and how it does it, because it is written down in a statement of purpose about the service. This document is made available to me and explained in a way that meets my needs. I am kept informed of any significant changes to the statement of purpose."""),
                    Person("4.1.2", """My service provider communicates clearly with me in a timely manner to keep me updated on how essential home support services will be provided to me in the event of a business disruption, for example, as a result of severe weather."""),
                    Person("4.1.3", """I know who I can contact in my home support service if I have a concern, during office hours, at night and at weekends."""),
                    Person("4.1.4", """I can access my service provider's charter of service delivery on their website. This charter sets out the quality of service and the conduct that I can expect in all interactions with my service provider and their staff."""),
                    Person("4.1.5", """I know what to expect from the service and I am treated the same way as other people using the service, because there are policies in place that are consistently followed.""", EqualStatusActFootnote),
                    Provider("4.1.1", """The service provider has a clear and accessible statement of purpose which is publicly available on their website."""),
                    Provider("4.1.2", """The service provider has a charter of service delivery ("charter") in place which is publicly available on their website."""),
                    Provider("4.1.3", """The service provider has clearly defined governance and management arrangements in place that are regularly reviewed to ensure that they are fit for their intended purpose and are effective. These arrangements define lines of authority and accountability, roles and responsibilities for ensuring the quality and safety of the service."""),
                    Provider("4.1.4", """The service provider has a comprehensive risk management system in place which identifies and addresses risk to individuals who use the service, the workforce and the continuity of services provided by the organisation."""),
                    Provider("4.1.5", """The service provider has arrangements in place to regularly review national standards, guidance, alerts and recommendations formally issued by regulatory bodies in order to determine what is relevant to the home support services provided, and take action to address any identified gaps. This includes recommendations made following an investigation or review into the service."""),
                    Provider("4.1.6", """The service provider adheres to the legislation relevant to its service. There is ongoing regular review of existing and new legislation to ensure compliance with all relevant Irish and European legislation."""),
                    Provider("4.1.7", """The service provider has a business continuity plan in place to maintain essential home support services in the event of a business disruption. This plan includes how the service provider will communicate with people using the service in a timely manner to keep them up-to-date on the home support that can be provided."""),
                    Provider("4.1.8", """The service provider produces and shares information on making protected disclosures. Members of the workforce are facilitated to exercise their personal, professional and collective responsibility to report, in good faith, any concerns that they have in relation to the safety and quality of the service, in line with legislative requirements.""")),
                Standard("4.2",
                    Person("4.2.1", """I get the care and support I need, with consideration of my daily routine because my home support services have been planned to meet my needs."""),
                    Person("4.2.2", """I know how long I am going to get home support services for, and the reasons for any changes to this are explained to me in a way that I can understand."""),
                    Provider("4.2.1", """Service providers have an up-to-date plan in place detailing how the service will be planned, managed, staffed and resourced to consistently meet the needs of the people who use the home support service. Service providers consider the use of relevant and appropriate emerging technologies to assess and plan the use of resources."""),
                    Provider("4.2.2", """Staff have access to, and knowledge of the policies and procedures which support them in their role in achieving the best quality of care and support.""")),
                Standard("4.3",
                    Person("4.3.1", """I experience joined-up care and support from the different home support services I need, who work together so that my needs are met at the right time and in the right way for me. I am aware of what each service should be doing to support me and who is responsible for this."""),
                    Person("4.3.2", """I am confident that the staff providing these services have the skills and information to plan and coordinate my home support."""),
                    Person("4.3.3", """If care and support is also provided to me by family members or friends, my service provider works to support positive interactions between staff and informal caregivers."""),
                    Provider("4.3.1", """The service provider has protocols, policies and procedures in place that set out the organisational and staff responsibilities within and between services to ensure coordinated care and support to people using services who need care and support from more than one service."""),
                    Provider("4.3.2", """The service provider facilitates a cooperative approach in the planning and delivery of home support where there is more than one home support worker, family carers and or multiple agencies involved.""")),
                Standard("4.4",
                    Person("4.4.1", """I know that staff caring for and supporting me will look for ways to improve the care and support they give me and other people using the service."""),
                    Person("4.4.2", """I know that the service provider who provides me with my home support services is always looking for ways to make the service safer and better for me and other people using the service. This includes sharing good practice and looking at times when things go wrong, to identify how the service can improve."""),
                    Person("4.4.3", """I am regularly asked to give my views on the service in an open and transparent way. My views and feedback are listened to and incorporated in any improvement programmes or initiatives. My views are taken seriously and I am told how they have been used."""),
                    Person("4.4.4", """I know that staff will also be asked for their views on how the service can be improved."""),
                    Person("4.4.5", """I have opportunities to participate in the planning, design and evaluation of the service and I am encouraged to do so."""),
                    Person("4.4.6", """I am confident that my service provider will review and take on board the outcomes of inspections, audits and reviews, and appropriate action(s) will be taken to ensure improvement."""),
                    Provider("4.4.1", """The service provider uses information as a resource in planning, delivering, managing and improving its services to meet the needs of the people using the service."""),
                    Provider("4.4.2", """The service provider has arrangements in place to ensure the collective interests of people who use the service are taken into consideration when decisions are being made about the planning, design and delivery of services."""),
                    Provider("4.4.3", """The service provider has arrangements in place to conduct regular evaluations of services to assess how well they are meeting the identified needs and preferences of people using the service. This includes having a process in place for consulting with people who use the service and using their feedback to continuously improve their experiences."""),
                    Provider("4.4.4", """The service provider formally plans and documents, in a quality improvement plan, what it is going to do to meet people's needs and improve the quality of its service in the short, medium and long-term. The service measures whether they have done this and reports this in an annual report."""),
                    Provider("4.4.5", """There is a proactive approach to learning from the findings and recommendations from national and international reviews and investigations."""),
                    Provider("4.4.6", """The service provider encourages and supports reporting throughout the service, especially when things go wrong and reviews any concerns about the quality and safety of the service which are brought to their attention by people who use the service or by members of the workforce. There are appropriate governance and accountability structures in place to support open disclosure.""")),
                Standard("4.5",
                    // Standard 4.5 is the only standard using trailing-period identifiers in the source
                    // ("4.5.1." not "4.5.1") — kept literally, not normalised.
                    Person("4.5.1.", """I am confident that the service provider shares relevant information in a timely way within, and between, relevant organisations, in line with legislation. This is done in a manner that facilitates effective home support for me, while protecting my privacy and confidentiality and keeping my information safe and secure."""),
                    Person("4.5.2.", """Information about me and the home support I receive is used by the service to improve my care and support."""),
                    Person("4.5.3.", """I have access to and can request to see any information written about me, in line with legislation."""),
                    Provider("4.5.1.", """The service provider has systems, policies, procedures and practices in place to ensure that high-quality information is available and shared in a timely way within, and between, relevant organisations, in line with legislation. These arrangements facilitate effective home support services and protect the privacy and confidentiality of the person using the service."""),
                    Provider("4.5.2.", """The service provider uses information from monitoring performance and other sources to improve the quality, safety and reliability of home support services."""),
                    Provider("4.5.3.", """The performance of the service against the service provider's quality and safety objectives is monitored, managed and reported through the relevant governance structures."""),
                    Provider("4.5.4.", """Service providers take part in and provide data to any relevant national home support quality and safety improvement programmes."""),
                    // Footnote confirmed to exist (terms "telecare" and, on the provider item above,
                    // "data ... shared") but its verbatim text was not captured by the recon — left null,
                    // not invented. See the class-level doc comment.
                    Provider("4.5.5.", """Where applicable, the service provider has a policy on the use of telecare interventions, including the use of artificial intelligence, which includes obtaining the consent of the person using the service.""")))
        };
}
