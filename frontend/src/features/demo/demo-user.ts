// shortcut: synthetic demo student in the frontend, replace with GET /api/dashboard once the backend model exists.

export type Importance = "required" | "nice";
export type Coverage = "strong" | "partial" | "none";

export type Requirement = {
    text: string;
    importance: Importance;
    coverage: Coverage;
    action?: string;
    project?: string;
    due?: string;
};

export type Posting = {
    id: string;
    company: string;
    title: string;
    city: string;
    language: "NL" | "FR" | "EN";
    deadline: string;
    interviewAt?: string;
    requirements: Requirement[];
};

const weight: Record<Importance, number> = { required: 2, nice: 1 };
const value: Record<Coverage, number> = { strong: 1, partial: 0.5, none: 0 };

/** Match score in code: sum(weight x coverage) / sum(weight). Returns 0..100. */
export function score(requirements: Requirement[]): number {
    const total = requirements.reduce((sum, r) => sum + weight[r.importance], 0);
    if (total === 0) return 0;
    const got = requirements.reduce((sum, r) => sum + weight[r.importance] * value[r.coverage], 0);
    return Math.round((got / total) * 100);
}

export const impact = (r: Requirement) => weight[r.importance] * (1 - value[r.coverage]);

export const demoUser = {
    name: "Lotte Peeters",
    initials: "LP",
    github: "lotte-dev",
    school: "Thomas More, Applied Computer Science",
    stage: { start: "2027-02-01", end: "2027-05-31" },
    sources: [
        { kind: "GitHub", label: "lotte-dev, 3 repos", synced: "2026-11-24" },
        { kind: "CV", label: "cv-lotte.pdf", synced: "2026-11-22" },
        { kind: "Site", label: "Not added yet", synced: null },
    ],
    profile: { confirmed: 9, total: 12 },
};

export const postings: Posting[] = [
    {
        id: "p1",
        company: "Brightlane",
        title: "Full-stack .NET stagiair",
        city: "Mechelen",
        language: "NL",
        deadline: "2026-12-12",
        interviewAt: "2026-12-03T10:00",
        requirements: [
            { text: "C# and ASP.NET Core", importance: "required", coverage: "strong" },
            { text: "React with TypeScript", importance: "required", coverage: "strong" },
            { text: "Docker", importance: "required", coverage: "partial", action: "Add a Dockerfile and compose file", project: "budget-buddy", due: "2026-11-28" },
            { text: "Automated tests", importance: "required", coverage: "partial", action: "Add xUnit tests for the expense parser", project: "budget-buddy", due: "2026-11-30" },
            { text: "CI pipeline", importance: "nice", coverage: "none", action: "Add a GitHub Actions build", project: "weather-app", due: "2026-12-02" },
        ],
    },
    {
        id: "p2",
        company: "Northwind Data",
        title: "Data engineering intern",
        city: "Brussels",
        language: "EN",
        deadline: "2026-12-20",
        requirements: [
            { text: "Python", importance: "required", coverage: "partial", action: "Port the weather fetcher to a Python script", project: "weather-app", due: "2026-12-05" },
            { text: "SQL", importance: "required", coverage: "strong" },
            { text: "Cloud basics (Azure)", importance: "nice", coverage: "none", action: "Deploy the API to Azure App Service", project: "budget-buddy", due: "2026-12-09" },
            { text: "English C1", importance: "required", coverage: "strong" },
        ],
    },
    {
        id: "p3",
        company: "Atelier Lumen",
        title: "Stagiaire développeur mobile",
        city: "Liège",
        language: "FR",
        deadline: "2027-01-10",
        requirements: [
            { text: "Kotlin / Android", importance: "required", coverage: "partial", action: "Add Room persistence to the quiz", project: "kotlin-quiz", due: "2026-12-14" },
            { text: "French B2", importance: "required", coverage: "partial" },
            { text: "UI testing", importance: "nice", coverage: "none", action: "Add an Espresso test for the score screen", project: "kotlin-quiz", due: "2026-12-16" },
        ],
    },
    {
        id: "p4",
        company: "Havenstad Logistics",
        title: "Software stagiair",
        city: "Antwerpen",
        language: "NL",
        deadline: "2027-01-24",
        requirements: [
            { text: "Java or C#", importance: "required", coverage: "strong" },
            { text: "REST APIs", importance: "required", coverage: "strong" },
            { text: "Git workflow", importance: "nice", coverage: "strong" },
        ],
    },
];

/** Score of the Brightlane posting after each re-scan. */
export const trend = [
    { date: "Nov 10", score: 48 },
    { date: "Nov 14", score: 55 },
    { date: "Nov 18", score: 62 },
    { date: "Nov 21", score: 66 },
    { date: "Nov 24", score: score(postings[0].requirements) },
];
