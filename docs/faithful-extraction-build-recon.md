# Faithful Extraction — Build Recon

**Date:** 2026-07-31
**Status:** Read-only recon. No code changed, no data changed, no fix or design proposed. Facts only, file:line for every claim except Deliverable 1 (an inventory of the source document itself, per the task brief).
**Branch:** `transval`, HEAD `e86526a` ("Guarantee ingestion always reaches a truthful terminal state").
**Builds on:** `docs/faithful-extraction-recon.md` (2026-07-31, HEAD `4bbceb8`). That recon already established the 151-feature count, the two-block-per-standard structure, and the prompt/schema/completeness-check gaps at a summary level. This recon (a) re-verifies every line-number citation against current HEAD, since one commit (`e86526a`) landed on `RequirementIngestionJob.cs` between the two recons, (b) produces the full itemised 151-feature inventory the prior recon stopped short of, and (c) turns the prior recon's findings into a decision list, a schema spec, and a rework-surface spec.

**What changed in code between the two recons, and why it doesn't affect faithfulness findings:** `e86526a` touched `MarkFailedAsync` (now takes a `Guid` and re-queries), added a rethrow after recording `Failed`, and added `StaleIngestionSweepJob` — all terminal-state/retry-visibility concerns. It did not touch `BuildExtractionPrompt`, `FindMissingStandards`, `PrincipleNumbers`, `HiqaExpectedStandardsByPrinciple`, or `PersistDraftRequirementsAsync`. Line numbers for those did shift slightly (the file grew by ~40 lines) — every citation below is against current HEAD `e86526a`, not the prior recon's `4bbceb8` numbers.

---

## Deliverable 1: Authoritative Feature Inventory

**Source:** `web/public/documents/Draft-National-Standards-for-Home-Support-Services.pdf` ("Draft National Standards for Home Support Services", November 2024, HIQA). Confirmed to be the exact document the HIQA ingestion path targets — title matches `RegulatoryProfileSeedData.cs:131` verbatim. Text extracted with `pdftotext` (both raw and `-layout` modes) for this recon; every item below was cross-checked against the document's own printed page numbers (footer "Page N of 75") as the durable citation anchor, not extractor line numbers.

**Total confirmed: 151** (83 person-experience features + 68 provider features), reconciled exactly against the prior recon's independently-derived count (`docs/faithful-extraction-recon.md:101`, §B.4 table). My per-standard, per-block counts below match that table's numbers item-for-item; I additionally transcribed the full verbatim text of every one of the 151 items (the prior recon stopped at counts + ranges).

**Format:** each standard shows its own outcome/provider-arrangement statement (context, not itself a numbered feature), then its person-experience features (`X.Y.n`), then its provider features (`X.Y.n`), verbatim. Ellipses are never used — every quoted string is the complete feature text as printed, including footnote markers where a marker's target could be identified (flagged inline; see Deliverable 2 for the ones that couldn't be).

### Principle 1: A Human Rights-based Approach (Standards 1.1–1.4)

#### Standard 1.1 — p.18–19
*Outcome:* "My human rights are explained to me in a way that I can understand and are respected and upheld. I feel valued by the staff providing my home support services and treated with dignity, compassion and respect."
*Provider arrangement:* "The service provider has arrangements in place to ensure a person's human rights are explained to them in a way they can understand and are protected, promoted and upheld."

Person-experience features (8):
1. **1.1.1** — "My human rights are clearly communicated to me by the service provider in a way that meets my needs, and I am supported to understand and realise my human rights in a way that best suits me."
2. **1.1.2** — "I am confident that staff will recognise if I need additional help and support to ensure my human rights are upheld or to get the care and support I need. I am provided with information regarding decision and advocacy support services that can support me to realise my human rights, express my views or access the services I need."
3. **1.1.3** — "I am confident that staff providing my care and support recognise that my home is my personal space and they respect my home environment and my right to live as I choose."
4. **1.1.4** — "My values, beliefs and way of life are respected by the staff caring for me and I am not treated differently to other people receiving home support for any reason." *[carries a footnote marker; the printed footnote reads: "The Equal Status Act 2000-2015 (the Acts) prohibit discrimination in the provisions of goods and services, accommodation and education. They cover the nine grounds of gender, marital status, family status, age, disability, sexual orientation, race, religion and membership of the Traveller Community." — see Deliverable 2, edge case 2.]*
5. **1.1.5** — "I am recognised as an individual and staff communicate with me in a respectful way. I experience kindness and compassion when using home support services."
6. **1.1.6** — "I am supported to complete everyday tasks and activities myself rather than my home support worker carrying them out for me."
7. **1.1.7** — "My privacy and dignity are respected and protected when delivering home support, particularly with personal and intimate care."
8. **1.1.8** — "My information is stored safely and securely in line with legislation, so it cannot be seen by people who do not need to see it. I am confident that my personal support plan is kept in a safe place in my house and I know who has access to it. The sharing of my personal information is carried out in a way that respects my rights."

Provider features (3):
1. **1.1.1** — "The service provider places human rights at the centre of its governance, management, culture and delivery of care and support. The service provider ensures that human rights principles are considered in the development of all policies, procedures and practices in order to protect, promote and uphold the human rights of people using services, as set out in legislation and national policy. These policies and procedures are implemented in practice and are regularly reviewed."
2. **1.1.2** — "The service provider has agreed processes in place to ensure that people using services are informed and aware of relevant advocacy services that can support them to achieve their human rights, express their views or access the services. People using services are supported to access these services, as necessary."
3. **1.1.3** — "The service provider has systems in place to ensure that the personal information of people using the service is protected at all times, in line with legislation and best practice."

#### Standard 1.2 — p.20–21
*Outcome:* "I understand what the home support service offers and how to access the service. I can access these services without experiencing any form of discrimination."
*Provider arrangement:* "The service provider provides clear and accessible information about what they do and how to access the service. The service provider ensures people can access the service without discrimination."

Person-experience features (5):
1. **1.2.1** — "The home support service I receive is based on my assessed needs and I do not experience discrimination of any kind."
2. **1.2.2** — "I can easily access information about the home support services available to me, how to apply for a service, any eligibility requirements and if there are any direct financial costs to me. This information is easy to understand, and is available in a way that suits my needs."
3. **1.2.3** — "Accessible modes and formats of communication with my service provider are available to me."
4. **1.2.4** — "Any forms that I, or my family or advocate, need to complete when applying for and using the home support service are user-friendly and we can receive help to complete the forms, if we need it."
5. **1.2.5** — "My communication needs and abilities, and where relevant that of my family, are acknowledged and supported by the service. For example, if I need information provided in a different format or language, my service provider does all it can to meet my needs."

Provider features (3):
1. **1.2.1** — "The service provider ensures that information on the home support services that are available, the process for accessing these services and any direct financial costs for these services, is provided to people using the service in a timely fashion."
2. **1.2.2** — "The service provider ensures that access for those using the service is based on the individual's needs assessment, and is in line with relevant eligibility criteria."
3. **1.2.3** — "The service provider proactively identifies the diversity of needs of the population served, including their physical, sensory, cultural and language needs, and puts arrangements in place to meet these needs and support its service users, in line with relevant legislation."

#### Standard 1.3 — p.22–23
*Outcome:* "I am supported to be involved in planning and making decisions about my home support."
*Provider arrangement:* "The service provider has arrangements in place to ensure that a person is supported to participate and make decisions about their home support, and has the relevant information they need to do so."

Person-experience features (8):
1. **1.3.1** — "I am respected as the expert on my own life and supported to make decisions relating to my home support and be involved in planning my care and support as much as possible. My care and support focuses on what is important to me, how I want to live, and what support I need to achieve my goals."
2. **1.3.2** — "Staff communicate with me effectively, listen to me and seek my views to make sure their understanding of my needs, preferences and goals are up to date."
3. **1.3.3** — "I have the relevant information to help me to participate in decisions in a timely way."
4. **1.3.4** — "I know that staff will use plain language that I understand when talking to me about my home support. I am encouraged to ask questions and staff check that I understand the information. I am given sufficient time to consider the information given and all available choices."
5. **1.3.5** — "I am confident that staff will recognise if I need additional help and support to make a decision and provide me with information on how to access this additional decision support."
6. **1.3.6** — "I, and where relevant my decision supporters, participate in decision-making around my care and support, particularly relating to how this will be provided, when it will be provided and by whom." *[the term "decision supporters" is footnoted; the printed footnote — "Decision supporter: means a person defined in accordance with the Assisted Decision-Making (Capacity) Act 2015, 2022 whose legal authority is based on their registration status with the decision support service, that is decision-making assistant, co-decision-maker, decision-making representative, attorney, designated healthcare representative." — is printed at the bottom of the page containing the *provider* block below it, not this item; see Deliverable 2, edge case 2.]*
7. **1.3.7** — "If my views and preferences for my care and support are in conflict with my family's views and preferences, I know that staff will respect my wishes and support my autonomy."
8. **1.3.8** — "My service provider prepares a service agreement with me that sets out the home support services that will be provided to me and arrangements for how the service is delivered. This agreement is expressed in a way that I can understand and in a format that meets my needs. Any changes to this service agreement are agreed by me and the service provider before they come into effect."

Provider features (2):
1. **1.3.1** — "The need to support people to participate in and make decisions about their home support, and to ensure people have the relevant information they need to do so is reflected in the service provider's policies and procedures. The service provider ensures that these policies and procedures are informed by decision-making legislation, are implemented in practice and regularly reviewed and updated."
2. **1.3.2** — "Service agreements are prepared with all people who are using the services. These agreements are worded in clear language and are provided in a format that is understandable and best suited to the person using the service."

#### Standard 1.4 — p.24–25
*Outcome:* "I have regular opportunities to give feedback to the home support service and staff encourage and support me to do this. My feedback, concerns, complaints or compliments about the service are listened to, recorded, and managed in a timely way."
*Provider arrangement:* "The service provider facilitates and supports people using services to provide feedback and to express their concerns, complaints or compliments about the service and has arrangements in place for managing and responding to these in a timely way. The service provider ensures these arrangements are clearly communicated and accessible to people who use the service."

Person-experience features (5):
1. **1.4.1** — "I understand that I have a right to express my opinion on the service and how staff care for and support me. I am encouraged and supported to provide feedback on the home support service and on the care and support I receive."
2. **1.4.2** — "I am provided with a safe place and space to express my views when giving feedback. For example, I can provide feedback anonymously if I prefer to do so."
3. **1.4.3** — "I know how to make a complaint as I am provided with my service provider's complaints policy in my preferred format. This clearly outlines the mechanism for complaints and independent appeals process. I am informed about independent advocacy services that can support me when making a complaint."
4. **1.4.4** — "If I need to make a complaint, I am supported to do so and I am reassured that there will be no negative consequences to the care and support I receive. I am confident that any concerns that I express about my care and support or any complaints that I make will be responded to and addressed at the earliest opportunity to minimise the impact on me and others."
5. **1.4.5** — "I am informed of the outcome of any complaint I make. If there is a delay, staff keep me up to date. I can request an explanation if I am unhappy with the outcome of my complaint, without concern of repercussions."

Provider features (4):
1. **1.4.1** — "The service provider has mechanism in place to receive feedback from service users" *[printed without a closing period in the source]*
2. **1.4.2** — "The service provider has a complaints policy and clear, transparent, open and accessible arrangements in place to invite, receive, review and respond to any complaints or concerns about the services provided. These arrangements take account of legislation, relevant regulations, national guidelines and best available evidence."
3. **1.4.3** — "The service provider addresses complaints and concerns promptly, effectively and fairly, while supporting service users throughout the process and if necessary facilitating them to access support or independent advocacy services."
4. **1.4.4** — "The service provider ensures that people who make a complaint are not disadvantaged in any way. There is a fair and timely appeals procedure that is consistent with relevant legislation, regulations and best practice guidelines."

### Principle 2: Safety and Wellbeing (Standards 2.1–2.5)

#### Standard 2.1 — p.29–30
*Outcome:* "My individual needs are identified and assessed, and the care and support I receive helps to maintain and optimise my overall health and wellbeing."
*Provider arrangement:* "The service provider has arrangements in place to ensure that each individual's needs are identified and assessed. The service provider discusses with the service user and where applicable the HSE as commissioner of services, when a reassessment is needed."

Person-experience features (4):
1. **2.1.1** — "My home support needs are assessed and reviewed with me in a standardised way to ensure I receive the right care and support at the right time. This includes a comprehensive assessment of my health, physical, sensory, emotional and social care needs as well as identification of my preferences, strengths and goals."
2. **2.1.2** — "My needs assessment has a focus on optimising my quality of life, strengths, skills and interests through meaningful activities that are based on my preferences and goals."
3. **2.1.3** — "I can make decisions about whether family, friends, carers or others, such as advocates, are involved in my support. If care and support is also provided to me by family members or friends, service providers work to support positive interactions between home support workers and informal caregivers."
4. **2.1.4** — "The service provider informs me of the process for seeking a reassessment, should my circumstances or needs change."

Provider features (3, header printed **without** "a": "Features of service provider meeting this standard are likely to include:"):
1. **2.1.1** — "The service provider ensures an evidence-based assessment tool is used to assess the needs of the person using the service, in collaboration with that person. This includes a comprehensive assessment of the health, physical, sensory, emotional and social care needs of the person using the service."
2. **2.1.2** — "The service provider ensures that the needs assessment has a focus on optimising the independence, health, wellbeing and quality of life of the person using the service, in accordance with their identified needs, strengths and stated goals and preferences."
3. **2.1.3** — "The service provider has arrangements in place to respond to changes in the home support requirements of the individual using the service and discusses with them and the HSE as commissioner of services (where applicable) when a re-assessment is needed."

#### Standard 2.2 — p.31–33
*Outcome:* "My needs, strengths, preferences and goals are recognised as unique to me by my service provider and I am treated as a partner when planning my care and support. My care and support is provided in a tailored and timely way to achieve the best outcomes for me and my wellbeing."
*Provider arrangement:* "The service provider ensures that initial and ongoing planning and review of home support is undertaken in partnership with the person using a service to develop and deliver their individual support plan."

Person-experience features (8):
1. **2.2.1** — "I experience high-quality care and support because my home support workers have the necessary information and resources to support me."
2. **2.2.2** — "I am given the choice to be fully involved in developing and reviewing my personal support plan. My personal support plan is right for me because it sets out how my needs will be met, as well as my strengths, goals and preferences. The support required to achieve these is clearly documented and communicated to those providing my care and support."
3. **2.2.3** — "I am confident that, when implementing my personal support plan, the provision of any service is consistent with and contributes to meeting my assessed needs, goals and preferences. My care and support is provided in a planned and safe way, including if there is an emergency or unexpected event."
4. **2.2.4** — "The service provider agrees the timings of my home support visits with me and they are arranged to enable my daily activities and routines."
5. **2.2.5** — "I am treated as an individual by people who respect my needs, choices and preferences. I am empowered and enabled to be as independent and in control of my life as I want and can be."
6. **2.2.6** — "I can maintain and develop my interests, activities and what matters to me, in the way that I like and these are included in my personal support plan. This includes being supported to continue to participate fully as a citizen in my community in the way that I want. If this involves some element of risk, this has been discussed and agreed with me and is included in my personal support plan."
7. **2.2.7** — "If I am receiving support with my nutrition and hydration either by meal provision, assistance to eat or drink, shopping or preparing food - food choices are in line with my preferences and dietary plan or nutritional needs for maintaining my health and wellbeing."
8. **2.2.8** — "My personal support plan is updated in accordance with the outcomes I achieve, my assessed or re-assessed needs and my home support requirements."

Provider features (5, header without "a"):
1. **2.2.1** — "The service provider has a policy in place which outlines the process for the development and review of a personal support plan with the person using the service, based on their individual needs assessment. This includes how their families, carers or advocates have been included in the review in accordance with the preferences of the person using the service."
2. **2.2.2** — "The service provider ensures that each person using the service has an up-to-date personal support plan developed in partnership with the person using the service. The service provider ensures that the support plan is easy-to read and accessible to the person using the service, home support worker and if applicable, other health and social care professionals involved in their care and support."
3. **2.2.3** — "The service provider ensures that the development of personal support plans have a focus on optimising the independence, health, wellbeing and quality of life of the person using the service in accordance with their identified needs, strengths and goals and preferences."
4. **2.2.4** — "The service provider has a system in place to ensure that the timing of home support visits are agreed with the person using the service and arranged so that they fit in with individual's needs, enable their daily activities and routines and, where relevant and possible, coordinates with informal carers. These timings are documented in the personal support plan and monitored in practice."
5. **2.2.5** — "Personal support plans are implemented and monitored by the service provider to ensure they are delivered in accordance with the needs of the person using a service. The service provider ensures that regular reviews of personal support plans take place with the person using the service, and that plans are updated in accordance with outcomes achieved, the individual's changing needs and home support requirements."

#### Standard 2.3 — p.34–36 — **contains the source numbering gap**
*Outcome:* "I am supported to be safe and live a whole and fulfilling life, free from harm or abuse."
*Provider arrangement:* "The service provider has arrangements in place to ensure that people receiving services are safeguarded from harm and abuse through the consistent implementation of relevant national standards, legislation, regulation, national policy, procedures and guidance. The service provider works with other services as appropriate to safeguard people using services."

Person-experience features (7):
1. **2.3.1** — "I am confident that my service provider works to protect me from all forms of abuse including coercion, harassment, physical (including neglect), emotional (including bullying), sexual, financial or other exploitation."
2. **2.3.2** — "The service provider and staff understand their role and responsibilities in protecting me from harm. This includes following legislation, standards, guidance and policies that help to keep me safe, as well as knowing the correct way to report any concerns they may have about me or my care and support."
3. **2.3.3** — "I am listened to and taken seriously if I have a concern about the protection and safety of myself or others."
4. **2.3.4** — "Staff respect the place where I receive care and support as my home and respect the security of my home and my possessions."
5. **2.3.5** — "I am confident that staff are working in line with financial policies and procedures including, for example, that staff working in my home support service will not act as my collection agent nor do they ask for or try to obtain loans or gifts from me." *[the term "collection agent" is footnoted; the printed footnote — "A collection agent means a person who collects, on behalf of a person using a service, a payment due to that person, including, but not limited to, payments under the Social Welfare (Consolidation) Act, 2005." — is interleaved mid-page inside the *provider* block, between provider items 2.3.4 and 2.3.5, not adjacent to this item; see Deliverable 2, edge case 2.]*
6. **2.3.6** — "I am confident that staff know what to look out for to keep me safe. My home support worker is alert to and responds to signs of any significant changes in my health and wellbeing."
7. **2.3.7** — "The home support worker(s) who support me, create an environment that is safe and is the least restrictive possible, and I am confident that they are trained to do this."

Provider features (7) — **numbered `2.3.2`–`2.3.8`, i.e. there is no printed `2.3.1` in the provider block.** Confirmed present in the source itself (not an extraction artifact) via both raw and `-layout` extraction passes:
1. **2.3.2** — "The service provider has a range of policies and procedures in place to support the safety and wellbeing of people who use the service and to ensure the security, safety and protection of the individual and their home when the service is being delivered."
2. **2.3.3** — "The service provider has an up-to-date, person-centred safeguarding policy and associated processes and procedures in place, which are in line with relevant national standards, legislation, regulations, national policy, procedures and best practice guidance. These clearly set out the roles and responsibilities of the service provider and staff in identifying and managing safeguarding concerns and are consistently implemented across the service in a timely way."
3. **2.3.4** — "The service provider has a clearly defined reporting pathway for the person using the service and home support worker where safeguarding concerns arise. This is supported by clear policies and procedures to facilitate timely communication between the service provider and other relevant services and professionals (including up-to-date contact and or organisational details) to ensure people are safe, especially when there is an immediate risk to a person."
4. **2.3.5** — "Staff are trained and supported to understand their role and responsibilities in safeguarding people who are receiving home support services. For example, service providers ensure that home support workers have completed safeguarding awareness training that includes the recognition and reporting of suspected abuse and the recognition of (signs of) self-neglect and making protected disclosures about the home support service."
5. **2.3.6** — "The service provider ensures that the system of supervision and development for staff includes safeguarding as a core component."
6. **2.3.7** — "The service provider has a system in place to successfully implement learnings from investigations into safeguarding concerns."
7. **2.3.8** — "Service providers have policies, systems and processes in place to ensure that people using a service are free from the use of any unnecessary restrictive practices in the provision of home support services. Service providers monitor, record and review the use of any restrictive practices included in a personal support plan in line with any assessed needs."

#### Standard 2.4 — p.37–38
*Outcome:* "I receive safe home support services and potential risks to me in delivery of my home support are identified and reduced."
*Provider arrangement:* "The service provider has arrangements in place to identify aspects of home support delivery associated with possible increased risk of harm to people using the service. The service provider puts measures in place to reduce these risks and prevent or minimise harm to people using the service."

Person-experience features (4):
1. **2.4.1** — "I am confident that my provider has arrangements in place to identify and address any potential risks to me in the delivery of my care and support."
2. **2.4.2** — "I know that staff take all the precautions they can to prevent the risk of transmission of infection and have been trained to do so."
3. **2.4.3** — "The service provider works with other services when I am transferring from one service to another, for example between hospital and home or from one home support service to another, to plan, coordinate and manage my transfer effectively."
4. **2.4.4** — "If I need help with my medication, I am confident that staff can support me to manage my medication safely, as set out in my personal support plan. This may include collecting prescriptions and or prescribed medicines, prompting me if necessary regarding the timing of medication, assisting me to take prescribed medication, and observing for medication missed doses or errors."

Provider features (4, header without "a"):
1. **2.4.1** — "The service provider has arrangements in place to proactively identify and assess areas of home support delivery where there may be an increased risk of harm to the person using the service. These areas may include, but are not limited to, transitions of care, infection prevention and control, medication support, use of equipment, restrictive practices, deterioration of condition and falls prevention. Service providers put structured arrangements in place to identify and minimise these risks."
2. **2.4.2** — "The service provider has an infection prevention and control policy in place, in line with national standards and guidance. Staff are trained in relevant infection prevention and control practices. This includes, for example, adhering to policies and procedures, practising good hand hygiene and respiratory and cough etiquette, transmission-based precautions and the safe use of personal protective equipment."
3. **2.4.3** — "Staff have access to adequate supplies of personal protective equipment to meet the circumstances of the person using the service and know how to use and dispose of it correctly."
4. **2.4.4** — "The service provider has an up-to-date policy on medication support and monitors adherence to the policy, taking appropriate action where safety risks are identified. The service provider ensures that home support workers who undertake medication management support receive appropriate training and are competent to do so."

#### Standard 2.5 — p.39–40
*Outcome:* "I am confident that if something goes wrong with my home support, my service will respond appropriately and in a timely manner. The service will review what happened, learn from it and will work to make sure that it does not happen again. My service is open and honest with me throughout this process."
*Provider arrangement:* "The service provider has arrangements in place to identify, manage and report incidents in a timely manner, in line with relevant national legislation, policy, guidelines and guidance, and will use learnings to inform future policies and practices. Service providers fully and openly inform and support people using services throughout this process, in line with National Open Disclosure Policy and Frameworks."

Person-experience features (2):
1. **2.5.1** — "Staff communicate with me in an open, honest, timely and compassionate manner if something goes wrong during my care and support. I am confident that if something goes wrong in my care and support, my service provider communicates with me openly and honestly and involves me in the review of any incident."
2. **2.5.2** — "I am confident that the outcome of any review that may take place is available to me and any learning from the review is used to help improve the service."

Provider features (2, header without "a"):
1. **2.5.1** — "The service provider has robust arrangements in place, including policies, procedures and staff training, so that staff can identify, respond to, report, review and learn from incidents, in line with national standards, legislation, policy, guidelines and guidance."
2. **2.5.2** — "The service provider and staff communicate openly and honestly with people if something goes wrong in their home support and involves them in the review of any incidents. The outcome of any review that may take place and any action arising from the review is made available to the person using the service."

### Principle 3: Responsiveness (Standards 3.1–3.3)

#### Standard 3.1 — p.44–45
*Outcome:* "Staff take the time to get to know me as a person and understand my needs, preferences and goals in a wider context, and respond to my individual needs and circumstances in a timely and sensitive way."
*Provider arrangement:* "The service provider has arrangements in place to support staff to develop consistent and trusting relationships with service users and understand and respond to their needs, preferences and abilities to help achieve the best outcomes for them."

Person-experience features (5):
1. **3.1.1** — "I experience continuity of care and support from the same team of staff. I know who will provide my care and support on a day-to-day basis and what they are expected to do."
2. **3.1.2** — "Staff take the time to develop a relationship with me and listen to me, in order to get to know me and what is important to me. They speak and listen to me in a way that is courteous and respectful, with my care and support being the main focus of their attention" *[printed without a closing period]*
3. **3.1.3** — "I am made aware of the circumstances when an alternative home support worker may be required to provide care or support to me. If there is a change due to unforeseen circumstance or planned leave, my service provider notifies me in advance, in a way that suits my needs."
4. **3.1.4** — "I am supported and cared for in a sensitive manner by people who know me and my circumstances. They can anticipate issues that may arise for me and are aware of and plan for any known vulnerability or frailty that I may experience."
5. **3.1.5** — "I am confident that staff advocate for support that is tailored to my individual needs and circumstances and is delivered in the right way, at the right time, and for as long as required."

Provider features (5, header without "a"):
1. **3.1.1** — "The service provider ensures that there are sufficient staff with the right skills and levels of experience to provide consistent care and support to each person using a service, in line with the requirements of the service being provided and the needs of the person."
2. **3.1.2** — "The service provider has safe and effective systems, strategies, policies and procedures in place to recruit and retain home support workers who are sufficiently competent, skilled and experienced to build trusting relationships and meet the needs of the person using the service."
3. **3.1.3** — "Service providers ensure that staff receive training on effective communication and have the ability to communicate with people using the service in a meaningful way that best suits their needs."
4. **3.1.4** — "Staff take their time to build a trusting relationship with the person, in order to understand and respond to the person's needs in a timely way."
5. **3.1.5** — "The service provider has a system in place to ensure continuity of care. People using the service are notified in advance when a home support worker previously unknown to them is assigned to deliver their home support. The service provider has contingency plans in place in the event that a home support worker cannot attend at a person's home as agreed."

#### Standard 3.2 — p.46–47 — **the one count-asymmetric standard where the provider header still carries "a"**
*Outcome:* "All staff involved in my care and support communicate and work together so that I receive the best possible care and support at the right time. This includes communication within services and also between services when appropriate."
*Provider arrangement:* "The service provider has arrangements in place to ensure care and support is coordinated effectively so people receive the right supports at the right time. Services proactively work together to achieve this and provide continuity of care."

Person-experience features (3):
1. **3.2.1** — "My care and support is consistent and reliable because staff are supported to work together well and learn from each other to ensure the best outcomes for me are achieved. I experience kind and compassionate care and support because there are good working relationships."
2. **3.2.2** — "I am involved in planning and managing any move between different home support services. I receive home support that is well coordinated and flexible enough to suit my changing needs and reduce the risk of harm to me during any transition period."
3. **3.2.3** — "I receive appropriate notice if the service I use can no longer meet my needs and wishes."

Provider features (2, header **with** "a": "Features of a service provider meeting this standard are likely to include:"):
1. **3.2.1** — "The service provider has arrangements in place to ensure that people using the service receive care and support that is integrated within and between home support services. The service provider supports staff to work together to achieve continuity of care for those using the service."
2. **3.2.2** — "Staff are supported and trained to understand their role and responsibilities in advocating for people using the service within and between services, to ensure that people get the right services in a way that meets their needs."

#### Standard 3.3 — p.48–50
*Outcome:* "I receive care and support from skilled, experienced and trained staff who are clear about their role and responsibility in my care and are informed by the best available evidence and information and are supported to do their job well."
*Provider arrangement:* "The service provider has systems and structures in place to ensure staff have the skills, training and experience to deliver safe and effective care and support that is informed by the best available evidence and information. Staff are supported and supervised to do this."

Person-experience features (5):
1. **3.3.1** — "I receive safe and high-quality care and support that meets my needs, supports my wellbeing and is based on the best available evidence."
2. **3.3.2** — "I am confident that staff who support and care for me have been recruited in line with the relevant policies and procedures."
3. **3.3.3** — "Staff supporting me are competent and have the qualifications, skills, knowledge and experience necessary to care for and support me effectively with empathy and compassion."
4. **3.3.4** — "I am confident that staff are supported in their role and receive regular and ongoing supervision."
5. **3.3.5** — "I am confident that staff receive regular training and education to retain, reflect and build on new skills and knowledge to provide the best care and support to meet my needs. I am confident that staff are given time and support from their employers to put their learning into practice."

Provider features (5, header without "a"):
1. **3.3.1** — "The service provider has a workforce recruitment and retention plan that is regularly reviewed and updated. This plan sets out the staffing levels to ensure adequate cover, skill-mix, competencies, experience and capabilities required to meet the needs of people using the home support service. The service provider monitors and evaluates the effectiveness of recruitment processes and addresses identified gaps."
2. **3.3.2** — "The service provider ensures that the workforce has the skills required to support people using the service, through regular staff training needs analysis and taking appropriate action to address any knowledge gaps and training required. This includes matching skills within the workforce with individual needs assessments of people using the service."
3. **3.3.3** — "The service provider ensures that all new home support workers complete induction training. This process includes ensuring that all home support workers are supervised by a suitably experienced worker as part of the practical training and are formally assessed and deemed competent prior to working alone."
4. **3.3.4** — "Staff are supported to understand their roles and responsibilities and work in line with relevant legislation, regulations and standards, as well as national and local policies and procedures at all times. The performance of staff is assessed at regular specified intervals and all staff receive support and supervision to ensure that they perform their role to the best of their ability."
5. **3.3.5** — "The service provider demonstrates a commitment to the continuous professional development of the workforce through the development and implementation of an annual training programme and by facilitating staff to achieve or maintain relevant care and support qualifications and training to address the identified needs of people using the service. The service provider ensures that staff are supported through, for example, education, training and opportunities for reflective practice."

### Principle 4: Accountability (Standards 4.1–4.5)

#### Standard 4.1 — p.55–57
*Outcome:* "I am confident that the service providing my home support is well managed and follows relevant policies and procedures to make sure I get the right care and support."
*Provider arrangement:* "The service provider has effective leadership, governance and management arrangements in place that reflect the type of home support service being delivered. This includes compliance with relevant legislation, national standards and policies."

Person-experience features (5):
1. **4.1.1** — "I know what my home support service does, and how it does it, because it is written down in a statement of purpose about the service. This document is made available to me and explained in a way that meets my needs. I am kept informed of any significant changes to the statement of purpose."
2. **4.1.2** — "My service provider communicates clearly with me in a timely manner to keep me updated on how essential home support services will be provided to me in the event of a business disruption, for example, as a result of severe weather."
3. **4.1.3** — "I know who I can contact in my home support service if I have a concern, during office hours, at night and at weekends."
4. **4.1.4** — "I can access my service provider's charter of service delivery on their website. This charter sets out the quality of service and the conduct that I can expect in all interactions with my service provider and their staff."
5. **4.1.5** — "I know what to expect from the service and I am treated the same way as other people using the service, because there are policies in place that are consistently followed." *[the printed page also repeats, as a footnote, the same Equal Status Act text quoted under 1.1.4 above; see Deliverable 2, edge case 2.]*

Provider features (8, header with "a") — **the one standard where the provider block has substantially more items than the person block:**
1. **4.1.1** — "The service provider has a clear and accessible statement of purpose which is publicly available on their website."
2. **4.1.2** — "The service provider has a charter of service delivery (\"charter\") in place which is publicly available on their website."
3. **4.1.3** — "The service provider has clearly defined governance and management arrangements in place that are regularly reviewed to ensure that they are fit for their intended purpose and are effective. These arrangements define lines of authority and accountability, roles and responsibilities for ensuring the quality and safety of the service."
4. **4.1.4** — "The service provider has a comprehensive risk management system in place which identifies and addresses risk to individuals who use the service, the workforce and the continuity of services provided by the organisation."
5. **4.1.5** — "The service provider has arrangements in place to regularly review national standards, guidance, alerts and recommendations formally issued by regulatory bodies in order to determine what is relevant to the home support services provided, and take action to address any identified gaps. This includes recommendations made following an investigation or review into the service."
6. **4.1.6** — "The service provider adheres to the legislation relevant to its service. There is ongoing regular review of existing and new legislation to ensure compliance with all relevant Irish and European legislation."
7. **4.1.7** — "The service provider has a business continuity plan in place to maintain essential home support services in the event of a business disruption. This plan includes how the service provider will communicate with people using the service in a timely manner to keep them up-to-date on the home support that can be provided."
8. **4.1.8** — "The service provider produces and shares information on making protected disclosures. Members of the workforce are facilitated to exercise their personal, professional and collective responsibility to report, in good faith, any concerns that they have in relation to the safety and quality of the service, in line with legislative requirements."

#### Standard 4.2 — p.58
*Outcome:* "My care and support is consistent and I receive the care and support that I need to live in my own home."
*Provider arrangement:* "The service provider has arrangements in place to plan, manage, support and organise its resources, including its workforce, to ensure people receive responsive, coordinated and consistent care and support."

Person-experience features (2):
1. **4.2.1** — "I get the care and support I need, with consideration of my daily routine because my home support services have been planned to meet my needs."
2. **4.2.2** — "I know how long I am going to get home support services for, and the reasons for any changes to this are explained to me in a way that I can understand."

Provider features (2, header with "a"):
1. **4.2.1** — "Service providers have an up-to-date plan in place detailing how the service will be planned, managed, staffed and resourced to consistently meet the needs of the people who use the home support service. Service providers consider the use of relevant and appropriate emerging technologies to assess and plan the use of resources."
2. **4.2.2** — "Staff have access to, and knowledge of the policies and procedures which support them in their role in achieving the best quality of care and support."

#### Standard 4.3 — p.59–60
*Outcome:* "If I need home support from more than one service, this is planned and organised so I get the right services, at the right time. I know the person or service in charge of organising all of the different home support services for me and I do not experience any gaps in my care and support."
*Provider arrangement:* "The service provider advocates on behalf of people using their service and, where applicable, in consultation with the HSE as commissioner of services, to support people to receive coordinated care and support in a timely and integrated manner."

Person-experience features (3):
1. **4.3.1** — "I experience joined-up care and support from the different home support services I need, who work together so that my needs are met at the right time and in the right way for me. I am aware of what each service should be doing to support me and who is responsible for this."
2. **4.3.2** — "I am confident that the staff providing these services have the skills and information to plan and coordinate my home support."
3. **4.3.3** — "If care and support is also provided to me by family members or friends, my service provider works to support positive interactions between staff and informal caregivers."

Provider features (2, header with "a"):
1. **4.3.1** — "The service provider has protocols, policies and procedures in place that set out the organisational and staff responsibilities within and between services to ensure coordinated care and support to people using services who need care and support from more than one service."
2. **4.3.2** — "The service provider facilitates a cooperative approach in the planning and delivery of home support where there is more than one home support worker, family carers and or multiple agencies involved."

#### Standard 4.4 — p.61–63
*Outcome:* "The home support service I am using regularly looks at how it can improve the care and support given to me, and other people using the service, so that I get the best possible care and support. My views are important and are taken into account in the planning, review and delivery of services."
*Provider arrangement:* "The service provider fosters a just and open culture of continuous improvement, responding to and learning from audits, incidents and feedback to achieve the best possible outcomes for people receiving their services. The service provider has arrangements in place to ensure that the views of people receiving care and support are sought and inform service planning and development."

Person-experience features (6):
1. **4.4.1** — "I know that staff caring for and supporting me will look for ways to improve the care and support they give me and other people using the service."
2. **4.4.2** — "I know that the service provider who provides me with my home support services is always looking for ways to make the service safer and better for me and other people using the service. This includes sharing good practice and looking at times when things go wrong, to identify how the service can improve."
3. **4.4.3** — "I am regularly asked to give my views on the service in an open and transparent way. My views and feedback are listened to and incorporated in any improvement programmes or initiatives. My views are taken seriously and I am told how they have been used."
4. **4.4.4** — "I know that staff will also be asked for their views on how the service can be improved."
5. **4.4.5** — "I have opportunities to participate in the planning, design and evaluation of the service and I am encouraged to do so."
6. **4.4.6** — "I am confident that my service provider will review and take on board the outcomes of inspections, audits and reviews, and appropriate action(s) will be taken to ensure improvement."

Provider features (6, header with "a"):
1. **4.4.1** — "The service provider uses information as a resource in planning, delivering, managing and improving its services to meet the needs of the people using the service."
2. **4.4.2** — "The service provider has arrangements in place to ensure the collective interests of people who use the service are taken into consideration when decisions are being made about the planning, design and delivery of services."
3. **4.4.3** — "The service provider has arrangements in place to conduct regular evaluations of services to assess how well they are meeting the identified needs and preferences of people using the service. This includes having a process in place for consulting with people who use the service and using their feedback to continuously improve their experiences."
4. **4.4.4** — "The service provider formally plans and documents, in a quality improvement plan, what it is going to do to meet people's needs and improve the quality of its service in the short, medium and long-term. The service measures whether they have done this and reports this in an annual report."
5. **4.4.5** — "There is a proactive approach to learning from the findings and recommendations from national and international reviews and investigations."
6. **4.4.6** — "The service provider encourages and supports reporting throughout the service, especially when things go wrong and reviews any concerns about the quality and safety of the service which are brought to their attention by people who use the service or by members of the workforce. There are appropriate governance and accountability structures in place to support open disclosure."

#### Standard 4.5 — p.64–65 — **the only standard using trailing-period numbering (`4.5.1.` not `4.5.1`)**
*Outcome:* "I know that my home support service has access to, and uses, high-quality information effectively when making decisions with me about my care and support. I know that the service shares important information about me with other services in a timely and appropriate manner to ensure I get the care and support I need."
*Provider arrangement:* "The service provider has effective information management systems and structures in place to enable services to plan, manage, and deliver person-centred, safe and effective care and support. The service provider has arrangements in place to ensure adherence to relevant legislation, national standards, policies and initiatives for safe and effective collection, use and sharing of information."

Person-experience features (3, printed as `4.5.1.`/`4.5.2.`/`4.5.3.`):
1. **4.5.1.** — "I am confident that the service provider shares relevant information in a timely way within, and between, relevant organisations, in line with legislation. This is done in a manner that facilitates effective home support for me, while protecting my privacy and confidentiality and keeping my information safe and secure."
2. **4.5.2.** — "Information about me and the home support I receive is used by the service to improve my care and support."
3. **4.5.3.** — "I have access to and can request to see any information written about me, in line with legislation."

Provider features (5, printed as `4.5.1.`–`4.5.5.`) — **the second standard where the provider block has substantially more items than the person block:**
1. **4.5.1.** — "The service provider has systems, policies, procedures and practices in place to ensure that high-quality information is available and shared in a timely way within, and between, relevant organisations, in line with legislation. These arrangements facilitate effective home support services and protect the privacy and confidentiality of the person using the service."
2. **4.5.2.** — "The service provider uses information from monitoring performance and other sources to improve the quality, safety and reliability of home support services."
3. **4.5.3.** — "The performance of the service against the service provider's quality and safety objectives is monitored, managed and reported through the relevant governance structures."
4. **4.5.4.** — "Service providers take part in and provide data to any relevant national home support quality and safety improvement programmes."
5. **4.5.5.** — "Where applicable, the service provider has a policy on the use of telecare interventions, including the use of artificial intelligence, which includes obtaining the consent of the person using the service." *[the terms "telecare" and (in the provider block above) "data ... shared" are footnoted with definitions printed at the foot of the same page; see Deliverable 2, edge case 3.]*

### Reconciliation table (self-check against the prior recon's counts)

| Standard | Person count (mine) | Prior recon | Provider count (mine) | Prior recon |
|---|---|---|---|---|
| 1.1 | 8 | 8 | 3 | 3 |
| 1.2 | 5 | 5 | 3 | 3 |
| 1.3 | 8 | 8 | 2 | 2 |
| 1.4 | 5 | 5 | 4 | 4 |
| 2.1 | 4 | 4 | 3 | 3 |
| 2.2 | 8 | 8 | 5 | 5 |
| 2.3 | 7 | 7 | 7 | 7 |
| 2.4 | 4 | 4 | 4 | 4 |
| 2.5 | 2 | 2 | 2 | 2 |
| 3.1 | 5 | 5 | 5 | 5 |
| 3.2 | 3 | 3 | 2 | 2 |
| 3.3 | 5 | 5 | 5 | 5 |
| 4.1 | 5 | 5 | 8 | 8 |
| 4.2 | 2 | 2 | 2 | 2 |
| 4.3 | 3 | 3 | 2 | 2 |
| 4.4 | 6 | 6 | 6 | 6 |
| 4.5 | 3 | 3 | 5 | 5 |
| **Total** | **83** | **83** | **68** | **68** |

**Exact match, every standard, both blocks. 151 combined confirmed independently.**

**One refinement over the prior recon's §B.6 point 1** (provider-heading wording inconsistency): the prior recon characterised the "no `a`" heading variant as belonging to "Principles 2 and 3" specifically. A full pass over all 17 headings (this recon) shows the split does not follow principle boundaries cleanly: **2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.3** print "Features of service provider..." (no "a"), while **3.2** — despite sitting inside Principle 3 alongside 3.1 and 3.3 — prints "Features of **a** service provider..." like Principle 1 and Principle 4 do. Any future heading-based parser must treat this as a per-standard property, not a per-principle one.

---

## Deliverable 2: Verbatim-Unit Edge Cases (decisions for the user)

Each item below is a place where "what counts as the verbatim unit" is not self-evident from the document. Presented as a decision with options; no recommendation is made.

### Edge case 1 — Bundled obligations within a single numbered item

Several numbered items visibly bundle more than one distinct obligation in one sentence-group. None of these have internal sub-numbering in the source — the document itself treats each as **one** item. Examples (not exhaustive — the pattern recurs throughout):
- **1.1.8** (person) bundles three distinct claims: information stored safely/securely; personal support plan kept in a safe place with known access; sharing of information respects rights.
- **2.4.4** (person) bundles four distinct medication-support activities (collecting prescriptions, prompting on timing, assisting to take medication, observing for missed doses/errors) inside one item.
- **4.1.5** provider block, item **4.1.5** — reviewing standards/guidance/alerts/recommendations *and* taking action on gaps *and* acting on investigation/review recommendations, all in one item.
- **2.3.5** provider — safeguarding-awareness training content is itself a bundled list (recognition and reporting of suspected abuse; recognition of self-neglect signs; making protected disclosures).

**Decision:** (a) keep the document's own item as the atomic verbatim unit regardless of internal bundling (matches "the document's own numbering, unmodified" default already stated as a settled decision), or (b) split bundled items into sub-obligations for storage, accepting that the split point is an editorial judgment call not present in the source. Every instance of (b) would need its own sub-identifier scheme (e.g. `2.4.4a`, `2.4.4b`) that the document itself does not define.

### Edge case 2 — Footnotes carrying substantive definitions, and where they attach

The document defines several terms via footnote markers in the running text, with the footnote itself printed at the bottom of the physical page — which is not necessarily the same page, or even adjacent to, the item that used the term. Confirmed instances:
- **"Nine grounds" footnote** (Equal Status Act 2000-2015) — printed once at the foot of p.18 (associated with **1.1.4**'s "not treated differently... for any reason") and printed **again**, identically, at the foot of p.55 (associated with **4.1.5**'s "treated the same way as other people"). The document repeats the same substantive footnote for two different, distant features.
- **"Decision supporter" definition** — the term appears in person feature **1.3.6**, but the footnote text is printed at the foot of the page carrying the *provider* block for standard 1.3 (p.23), several items later.
- **"Collection agent" definition** — the term appears in person feature **2.3.5**, but the footnote is printed interleaved mid-page inside the *provider* block for standard 2.3 (p.35, between provider items 2.3.4 and 2.3.5).
- Other footnoted terms exist only in principle-level introductory prose, not inside any numbered feature (e.g. "positive risk-taking" in the Principle 1 narrative, "restrictive practices"/"incident" in the Principle 2 narrative, "enabling approach" in the Principle 4 narrative, "information governance"/"data" in the Principle 4 narrative, "data sharing"/"telecare" attached to standard 4.5's narrative and reused by feature **4.5.5.**).

**A technical caveat this recon must flag:** the footnote marker glyphs themselves (†, ‡, etc., or the document's own symbol set) did not survive plain-text extraction cleanly in either extraction pass used for this recon — they render as blank space or a corrupted character. This recon attached each footnote to its likely target by reading the surrounding prose and cross-referencing which term the footnote defines, not by tracing a surviving marker glyph. A production extraction pipeline reading the same way (plain PDF text, no marker-glyph preservation) faces the identical problem and cannot mechanically resolve footnote-to-feature attachment without either a marker-preserving extraction method or per-document manual verification.

**Decision:** for footnotes that plainly attach to a specific numbered feature (the Equal Status Act, Decision supporter, Collection agent cases above): (a) fold the footnote text into that feature's stored verbatim text as an appended clause, (b) store it as a separate, linked "definition" record referenced by the feature, or (c) drop it (glossary-adjacent content, out of requirement scope per the settled scoping decision — but note two of these three footnotes are *not* in Appendix 2's glossary at all, they are standard-specific footnotes unique to the features section). For footnotes attached only to principle-level narrative (not any numbered feature): the settled scoping decision already places principle-intro narrative outside the standards-and-features extraction target, so these are out of scope by that decision alone — flagged here only so the user can confirm that reading is intended to extend to *these* footnotes too, not just Appendix 2's glossary.

### Edge case 3 — Cross-references to other legislation, named inline

Every standard's features reference external legislation by name inline, e.g. "in line with legislation" (4.5.1.), "the Assisted Decision Making (Capacity) Act 2015" (Principle 1 narrative, not inside a numbered feature — but "decision-making legislation" is referenced inside provider feature **1.3.1**), "Social Welfare (Consolidation) Act, 2005" (footnote to 2.3.5), "Disability Act 2005" (Principle 1 narrative only). None of these referenced acts are themselves fetched, ingested, or cross-linked by the current codebase (confirmed — no such capability exists anywhere in the ingestion path).

**Decision:** confirm the referenced act's text itself is never chased or ingested — the requirement is transcribed exactly as the source names it ("in line with legislation", "in accordance with the Assisted Decision-Making (Capacity) Act 2015, 2022"), with no attempt to resolve *which* legislative sections apply. This appears to be the only workable boundary given no legislation-ingestion capability exists, but it is stated here as a decision to confirm rather than assumed, since a faithful-extraction effort could in principle be read as also requiring provenance on cited law.

### Edge case 4 — The "features are not exhaustive" caveat

Printed at least twice: once in the "Structure of the draft national standards" front-matter section ("The features detailed under each standard are not exhaustive and the service provider may meet the requirements in other ways.", p.10) and once in the standards-for-service-providers section header (p.14, "3. Features which, taken together, demonstrate how a person should experience a service that is meeting the standards and how a service provider may meet these standards."). It is not repeated per-standard in the standards-and-features section itself — it appears only in the document-wide front matter, not attached to any individual standard or feature.

**Decision:** (a) capture this caveat once, document-wide (e.g. a note on the `RegulatoryDocument` or `RegulatoryProfile` record) since it is not a per-standard statement in the source, (b) omit it entirely as non-requirement narrative, or (c) something else. Given it is front-matter (§3/§7 of the document, before the "Standards for service providers" section begins at p.14) it sits outside the settled standards-and-features scope boundary already — flagged here only to confirm that boundary is intended to exclude it too.

### Edge case 5 — Structural irregularities: confirm preserve-as-is, and which can't be cleanly preserved

Per the settled decision to preserve the document's actual numbering as-is, these are the concrete cases needing explicit sign-off (all independently reconfirmed by this recon's own extraction pass, not merely carried over from the prior recon):
- **Standard 2.3's provider block starts at `2.3.2`, not `2.3.1`** (Deliverable 1, Standard 2.3). A schema/UI that assumes item-N-of-block always starts at 1 will mis-render this block's first item as "missing item 1" if it infers presence by position rather than by the literal printed identifier.
- **Standard 4.5 uses trailing-period identifiers** (`4.5.1.`, not `4.5.1`) uniquely among all 17 standards. Confirm whether the stored identifier should preserve the trailing period literally (making `4.5.1.` a different string from `4.5.1` for any exact-match logic) or normalise it — normalising is itself a small unfaithfulness the settled decision may or may not intend to permit.
- **Provider-heading wording is not a clean per-principle split** (Deliverable 1's reconciliation-table footnote: 3.2 breaks from 3.1/3.3's pattern). This doesn't affect the numbered-item content itself, only confirms that any heading-based structural derivation (Deliverable 4) cannot use a principle-level rule.
- **Count asymmetry cannot be "fixed" by assuming a typo.** Standards 4.1 (5 person / 8 provider) and 4.5 (3 person / 5 provider) have provider blocks substantially larger than their person blocks; standard 3.2 has the reverse (3 person / 2 provider). Confirm none of these are to be treated as a suspected transcription error to "correct" — they are the document's own content as drafted for public consultation, not artifacts of this recon's extraction (independently confirmed via two extraction passes).

**Decision:** confirm all four points above are to be preserved literally (no normalisation, no assumed-typo correction, no positional inference) — consistent with the settled "preserve the document's own numbering" decision, but each is distinct enough (a real gap, a formatting variant, a heading-text variant, a count asymmetry) that blanket "preserve as-is" benefits from being confirmed against each concrete case rather than as one abstract principle.

---

## Deliverable 3: Schema Impact

### Current `RegulatoryRequirement` fields and how they're populated today

**Entity:** `RegulatoryRequirement.cs:11-45`. **Configuration:** `RegulatoryRequirementConfiguration.cs:8-100`. **DTOs:** `IngestionDtos.cs:39-75` (`DraftRequirementDto`, `RegulatoryRequirementDto` — both 1:1 mirrors of the entity plus two profile-lookup fields).

| Field | Type / max length | Populated from (current ingestion path) | Confirmed nature |
|---|---|---|---|
| `Title` | `string`, 200 | `extracted.Title` — `RequirementIngestionJob.cs:834` | Model-composed, per the prompt's own instruction ("A concise title... for the training/competency requirement", `:684`) |
| `Description` | `string`, 2000 | `extracted.Description`, or `Title` if blank — `:835-839` | Model-authored paraphrase ("A detailed description... of what the requirement entails", `:685`), not a transcription instruction |
| `Section` | `string?`, 20 | `extracted.Section` — `:840` | Standard-level reference only (prompt's own worked examples are `"Standard 2.3"`, `"Article 4"`, `"§7"`, `:686`) — no feature-level identifier concept anywhere in the prompt |
| `SectionLabel` | `string?`, 200 | `extracted.SectionLabel` — `:841` | Model-derived short label |
| `Principle` | `string?`, 20 | `extracted.Principle` — `:842` | Canonical (`P2`–`P4` per the hardcoded label block, `:693-696`) or model-inferred; note Principle 1 has no canonical entry (`:694-696` list only covers P2/P3/P4) |
| `PrincipleLabel` | `string?`, 200 | `extracted.PrincipleLabel` — `:843` | Canonical or model-derived |
| `Priority` | `string`, 20 | `ValidatePriority(extracted.Priority)` — `:844`, `:868-876` | Model-judged, clamped to `high`/`med`/`low` |
| `DisplayOrder` | `int`, required | Always overwritten to the item's position in the concatenated cross-principle list — `:162-165` | Assembly-assigned, not model- or document-derived; each segment's own model-supplied `displayOrder` is discarded |
| `IngestionSource` | enum, required | Hardcoded `Automated` — `:846` | — |
| `IngestionStatus` | enum, required | Hardcoded `Draft` — `:847` | — |
| `IngestionNotes` | `string?`, 1000 | Not set at ingestion time (only via reviewer approve/reject flows — `RequirementIngestionService.cs`) | — |
| `IsActive` | `bool` | Hardcoded `true` — `:848` | — |

**Conclusion:** the schema can technically hold a document-native identifier string in `Section` (20 chars fits `"Standard 1.1.7"` easily), but no current field, prompt instruction, or persistence code path treats `Section` as anything other than a standard-level reference. No field anywhere distinguishes person-experience vs. provider origin. Confirmed via full-repo grep (`PersonFeature`, `ProviderFeature`, `FeatureBlock`, `RequirementBlock` — zero hits in `src/`).

### Downstream consumers of `Section`/`SectionLabel`/`Principle`/`PrincipleLabel` (impact surface for any rename/add)

Grepped every read site of these four fields outside the entity/config/job itself:
- `RequirementIngestionService.cs:113-116, 143-146, 194-197, 309, 335-339, 346-347, 666-669` — pass-through in DTO projection (draft list, approve/reject/update handlers), and a `GroupBy(r => new { r.Principle, r.PrincipleLabel })` for the tenant-facing browse endpoint's principle grouping (`:335`).
- `RequirementMappingService.cs:344-345, 533-534` — pass-through in requirement-mapping DTOs (`RequirementSection`, `RequirementSectionLabel`).
- `InspectionReportService.cs:367-370` — renders `req.Section` as literal display text in the PDF inspection-readiness report (QuestPDF `.Text(req.Section)`).

None of these three consumers parse `Section` for structure (no substring matching, no regex) beyond the ingestion job's own `FindMissingStandards` (which does, standard-level only — see Deliverable 4). All three treat it as opaque display text. This means a schema change that adds new fields alongside `Section` (rather than repurposing it) has no breaking impact on these three consumers — they would simply continue reading the existing fields unchanged.

### What faithful storage requires, precisely

1. **A field for the document's own feature identifier** (e.g. `"1.1.7"`, `"4.5.1."` — note Edge Case 5's trailing-period question affects whether this is stored literally or normalised). Distinct from `Section`, which today means "standard-level reference" both by prompt instruction and by all three downstream consumers above — repurposing `Section` itself would change its meaning for those three consumers (low risk given they're pure pass-through, but a rename is cleaner than an implicit meaning change on the same field name).
2. **Whether verbatim text fits existing `Description` (2000 chars) or needs a distinct field.** Every one of the 151 transcribed items in Deliverable 1 is well under 2000 characters (the longest, provider **1.1.1**, is ~430 characters) — length is not a blocker either way. The open question is semantic, not capacity: `Description` is currently documented and used as "a paraphrase of what the requirement entails" (an authored summary); faithful verbatim text is a different kind of content occupying the same slot. Whether to (a) redefine `Description`'s contract to mean "verbatim source text" going forward, or (b) add a distinct field (e.g. an `AuthoritativeText` or `SourceText` field) and leave `Description` as an optional authored gloss, is a decision with the same shape as the person-vs-provider question in Deliverable 2 — not decided here.
3. **A person-vs-provider block marker.** No field exists today (confirmed in Deliverable 1/prior recon). Needed regardless of which option is chosen for the person-vs-provider representation question (surfaced, not decided, in the prior recon's §B.5) — even a "keep both, 151 total" choice needs a way to distinguish which block a given stored row came from, since the two blocks share identical identifier numbering per standard (e.g. both blocks have a `1.1.1`).
4. **An optional display-label field**, if the model-generated short label (settled as a UI decision, not this recon's) is adopted — this is additive only; `Title` already exists and could serve this purpose without a new column if the person-vs-provider and verbatim-text questions above are resolved first (i.e. `Title` stops being the authoritative content and becomes purely the display label once verbatim text has its own home).

### Migration

**Yes, an EF migration is needed** for at minimum the feature-identifier field (point 1) and the person/provider block marker (point 3) — both are new columns on an existing table. Per CLAUDE.md Note 28, this must be CLI-generated (`dotnet ef migrations add <Name>`, producing both `<Name>.cs` and `<Name>.Designer.cs`) — not hand-written.

**One correction to CLAUDE.md's own Note 28 worth flagging for whoever writes this migration:** Note 28's example command targets `--project ../Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure`, but `RegulatoryRequirement` (and every other regulatory-chain entity) is actually configured against `ApplicationDbContext`, whose migrations physically live in `src/Core/QuantumBuild.Core.Infrastructure/Migrations/` — confirmed by every existing regulatory-chain migration (e.g. `20260318162722_AddRegulatoryRequirements.cs`, `20260319102112_AddReviewNotesToRegulatoryRequirementMappings.cs`) residing there, and by `ApplicationDbContext.cs:110-111, 297-298` registering `RegulatoryRequirement`/`RegulatoryRequirementMapping` directly. A migration for this schema change should target `--project src/Core/QuantumBuild.Core.Infrastructure`, matching the existing regulatory-chain migration history, not the ToolboxTalks Infrastructure project Note 28's generic example names.

This is not written here — only specified, per non-scope.

---

## Deliverable 4: Extraction + Completeness-Check Rework Surface

### A. The current segmented extraction prompt — structure and what must change

**Current structure** (`RequirementIngestionJob.cs:677-710`, `BuildExtractionPrompt`, called once per principle number from `ExecuteAsync:127-129`):

1. Opening framing sentence naming a topical filter and the extraction unit as "requirements" (not the document's own "features").
2. A principle-scoping instruction ("You are extracting ONLY Principle {N}'s requirements").
3. An 8-field-per-item output spec (`title`, `description`, `section`, `sectionLabel`, `principle`, `principleLabel`, `priority`, `displayOrder`), with `title`/`description` explicitly framed as composed output, not transcription (`:684-685`, quoted in full in the prior recon `docs/faithful-extraction-recon.md:24-27`).
4. A hardcoded "CANONICAL PRINCIPLE LABELS" block covering only P2–P4 (`:693-696`), no P1 entry.
5. A restated topical-relevance filter under "IMPORTANT RULES" (`:701-705`).
6. A closing "JSON array only" instruction, followed by the full document text appended verbatim (`:708-709`).

**What must change, structurally, for verbatim transcription against the feature inventory:**
- The extraction unit must become the document's own numbered feature (per block), not a model-judged "requirement" — meaning the prompt needs to be handed (or derive) the expected feature identifiers for the principle it's extracting, the same way `HiqaExpectedStandardsByPrinciple` already hands it expected *standard* IDs today (`:437-444`), but at feature granularity and per block.
- `title`/`description` as composed-output fields need to be replaced or supplemented with a field whose instruction is transcription ("copy the exact text of feature X.Y.n"), not composition ("write a title for..." / "describe what this entails").
- The topical filter ("relate to staff training, competency, or compliance obligations", `:679`, restated `:702`) currently invites the model to silently exclude features it judges out-of-scope. Faithful transcription (per the settled decision: "ALL features the document distinguishes are preserved") means this filter must not apply to *which features get transcribed* — every feature in-scope by the feature inventory must be transcribed regardless of topical judgment. Whether a training-relevance filter still has a role *after* transcription (e.g. as a separate downstream classification) is not addressed here; it does not belong in the transcription step itself if the settled "no filtering of person-experience vs provider features" decision is to hold.
- The per-block distinction (person-experience vs. provider) needs a prompt-level home — nothing in the current prompt or its output schema is aware two independently-numbered blocks exist per standard; it currently produces one flat list per principle.
- The Principle 1 canonical-label gap (`:693-696`) needs closing regardless of the above — it's a pre-existing omission, not something this rework introduces or fixes implicitly.

### B. The current completeness check — what it validates today, and the gap to feature-level

**Current check:** `FindMissingStandards` (`RequirementIngestionJob.cs:576-596`), invoked after each principle segment's first attempt and again after its retry (`:497, 528`).

- For a principle number, looks up expected **standard IDs** from `HiqaExpectedStandardsByPrinciple` (`:437-444`) — e.g. Principle 1 → `["1.1", "1.2", "1.3", "1.4"]`.
- For each expected standard ID, checks whether **any** extracted requirement's `Section` field contains that ID as a digit-boundary-guarded substring (`:588-592`, regex `(?<!\d){standardId}(?!\d)`).
- Zero matches for a standard ID ⇒ "missing" ⇒ triggers retry (first attempt, `BuildStricterPrompt:552-565`) or an `Incomplete` segment failure (retry, `:526-536`) ⇒ all-or-nothing document failure (`:147-152`).
- **This is presence-only, at standard granularity, satisfied by exactly one requirement per standard.** A segment returning 4 requirements for Standard 1.1 and 1 each for 1.2–1.4 passes cleanly — the check has no concept of "how many features does 1.1 actually have."

**What it must become, precisely:**
- **Granularity: 17 standards → up to 151 features (or 83/68 depending on the Deliverable-2/prior-recon person-vs-provider decision), roughly a 9x increase** in the number of yes/no questions the check answers. Each question becomes "is feature `X.Y.n` (block: person or provider) present in the extracted set for this principle," not "does any requirement mention standard `X.Y`."
- **Matching mechanism must change.** Today's regex substring match against free-text `Section` works because it only needs to detect a standard-level token like `"1.1"` appearing anywhere in a short string. Detecting a specific feature identifier (`"1.1.7"`) requires either (a) the extraction schema to return a structured feature-identifier field per Deliverable 3's schema change (clean match, no regex ambiguity), or (b) continuing free-text matching against whatever identifier-shaped substring the model returns — which reintroduces exactly the kind of model-dependent parsing faithful extraction is meant to eliminate. This is a direct dependency: the completeness-check rework cannot reach feature granularity without the schema change in Deliverable 3 landing first (or concurrently).
- **The check still would not catch cross-boundary attribution on its own.** `FindMissingStandards` only asks "is this identifier present somewhere in this principle segment's output," never "does the requirement claiming to be feature `2.3.4` actually contain 2.3.4's text." A feature-level presence check closes the *coverage* gap (are all 151 there) but not the *fidelity* gap (does each one say what the source says) — the latter would need a separate verbatim-match check (e.g. comparing extracted text against the authoritative inventory's stored text for that identifier) that has no analogue in the current code at all.
- **Segmentation is per-principle only, not per-block.** Today one Claude call returns a flat list per principle; a feature-level check needs the extraction to either tag each returned item with its block (person/provider) or split the call itself into a person-call and a provider-call per principle (doubling the call count, mirroring the existing 4x-for-truncation trade-off `RequirementIngestionJob.cs:113-119` already accepts).

### C. Per-document structure map — what exists, what a second document would need

**Confirmed: no per-document dispatch exists anywhere in the current code**, and this is a stronger finding than "the map is HIQA-specific" (already known) — the segmentation scheme itself is HIQA-specific and hardcoded, unconditionally, regardless of which document is being ingested:
- `PrincipleNumbers = { 1, 2, 3, 4 }` (`:426`) is a `static readonly` array — every call to `ExecuteAsync`, for any `RegulatoryDocument`, segments into exactly four "principles" and builds a prompt saying "extract requirements under Principle {N}" (`:679`), regardless of whether the document being ingested has anything resembling HIQA's four-principle structure.
- `HiqaExpectedStandardsByPrinciple` (`:437-444`) is likewise unconditional — `FindMissingStandards` (`:576-596`) is called for every document's every segment; its only defensive behaviour for a document whose principle numbers aren't in the map is to return zero missing standards (`:578-579`, the `TryGetValue` fallback) — i.e. **the completeness check silently no-ops for any non-HIQA document**, it does not skip cleanly or fail loudly.
- `ExecuteAsync` never reads `document.RegulatoryBodyId`, `document.Title`, or any `Profiles[].Sector` value to select a different segmentation scheme, prompt template, or expected-structure map. The method signature and body treat `regulatoryDocumentId` purely as "which row to mark Ingesting/Failed/Success" — not as a signal for which document *structure* it's ingesting.

**What supplying a per-document structure map would entail**, based on what this recon's own HIQA read required:
1. **Manual document read** — reading the actual source PDF (as this recon did), since `PdfExtractionService` performs no heading/structure extraction (confirmed by the prior recon `docs/regulatory-extraction-rebuild-recon.md:216-225` and unchanged in current code — `FetchDocumentTextAsync:337-408` returns flat text only, no structure).
2. **Identifying that document's own numbering/heading conventions** — which, per this recon's Deliverable 1/2 findings, cannot be assumed regular even *within* one document (HIQA's own heading wording, numbering-gap, and count-asymmetry irregularities were all found within a single document). A structurally different instrument (numbered "Regulations" or "Articles," no person/provider dual-block framing at all) would need its own discovery pass from scratch, not an adapted version of HIQA's map.
3. **Hand-authoring the equivalent of `HiqaExpectedStandardsByPrinciple`'s feature-level successor** for that document, plus a dispatch mechanism keyed on something stable per document (e.g. `RegulatoryBody.Code`, or a new per-`RegulatoryDocument` structure-descriptor field) so `ExecuteAsync` can select the right map/prompt shape instead of assuming HIQA's shape unconditionally.

**Confirmed still true today:** `RegulatoryProfileSeedData.cs:67-73, 129-135` seeds four regulatory bodies/documents (HIQA, HSA, FSAI, RSA) — only HIQA has any `RegulatoryRequirement` rows or structure map. The other three's actual document structures are unknown from the codebase (no PDF for any of them is present in the repository); whether they even share HIQA's principle→standard→dual-feature-block shape, or something entirely different, has not been established by this recon or any prior one.

---

## Non-scope confirmation

No fix was designed or written. No prompt was rewritten. No migration was written (only specified, in Deliverable 3). The person-vs-provider representation question (68/83/151) was not decided — it is referenced from the prior recon and threaded through Deliverables 2 and 3 as an open dependency, not resolved here. No `RegulatoryRequirement`, `RegulatoryProfile`, `RegulatoryDocument`, or any other row was created, modified, or deleted. No ingestion was run. The document-identity/supersession/notification workstream and the UI-for-verbatim-prose rework were not addressed beyond noting the `Title`-as-display-label schema hook in Deliverable 3.
