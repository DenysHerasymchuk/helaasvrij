#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import * as coach from "./coach.js";

// stdout carries the MCP protocol: never console.log here.
const client = new coach.CoachClient(
  (process.env.COACH_API_URL ?? "http://localhost:5080").replace(/\/$/, ""),
  process.env.COACH_EMAIL,
  process.env.COACH_PASSWORD,
);

const server = new McpServer({ name: "helaasvrij", version: "0.1.0" });

async function run(work: () => Promise<coach.Result>) {
  try {
    const { text, data } = await work();
    return { content: [{ type: "text" as const, text }], structuredContent: data };
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e);
    return { content: [{ type: "text" as const, text: `Internship Coach error: ${message}` }], isError: true };
  }
}

server.registerTool(
  "list_postings",
  {
    description: "List the student's saved internship postings with match score and current application stage (or 'not on board').",
    inputSchema: {},
  },
  () => run(() => coach.listPostings(client)),
);

server.registerTool(
  "search_jobs",
  {
    description: "Search Belgian internship ads from the Adzuna snapshot. Returns jobIds you can pass to save_job.",
    inputSchema: {
      what: z.string().optional().describe("Keywords, e.g. 'react developer'"),
      where: z.string().optional().describe("City, e.g. 'Gent'"),
    },
  },
  ({ what, where }) => run(() => coach.searchJobs(client, what, where)),
);

server.registerTool(
  "save_job",
  {
    description: "Save an Adzuna job (from search_jobs) to the student's postings. Uses one AI call to read the ad.",
    inputSchema: { jobId: z.string().min(1).describe("jobId from search_jobs") },
  },
  ({ jobId }) => run(() => coach.saveJob(client, jobId)),
);

server.registerTool(
  "add_posting",
  {
    description: "Add a posting by pasting its text, or import it from a URL (not LinkedIn, Student.be or JobTeaser). Uses one AI call.",
    inputSchema: {
      text: z.string().optional().describe("Full posting text"),
      url: z.string().url().optional().describe("Posting URL; imported when no text is given, kept as the link otherwise"),
    },
  },
  ({ text, url }) => run(() => coach.addPosting(client, text, url)),
);

server.registerTool(
  "list_applications",
  { description: "List the applications board (Saved, Applied, Interview, Offer, Rejected).", inputSchema: {} },
  () => run(() => coach.listApplications(client)),
);

server.registerTool(
  "set_application_stage",
  {
    description:
      "Move a posting on the applications board, e.g. mark it Applied or Interview. Puts the posting on the board first if needed. " +
      "Identify it by applicationId, postingId, or a query matched against company and title; an ambiguous query changes nothing and returns the candidates.",
    inputSchema: {
      postingId: z.string().optional(),
      applicationId: z.string().optional(),
      query: z.string().optional().describe("Company and/or title words, e.g. 'Brightlane'"),
      stage: z.enum(["Saved", "Applied", "Interview", "Offer", "Rejected"]),
      notes: z.string().optional().describe("Replaces the card's notes"),
    },
  },
  (input) => run(() => coach.setApplicationStage(client, input)),
);

server.registerTool(
  "get_posting",
  {
    description: "Get one saved posting with its requirements, coverage, match score and gap actions.",
    inputSchema: { postingId: z.string().min(1) },
  },
  ({ postingId }) => run(() => coach.getPosting(client, postingId)),
);

server.registerTool(
  "list_gaps",
  { description: "List the open gaps across all postings (the gap plan), highest impact first, with the action for each.", inputSchema: {} },
  () => run(() => coach.listGaps(client)),
);

await server.connect(new StdioServerTransport());
