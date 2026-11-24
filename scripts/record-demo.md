# Record the backup demo video (90 seconds)

Goal: a screen recording of the full demo from `CLAUDE.md`, in case the network or the gateway fails on stage.
Use the synthetic persona or a consenting student. Never record a real student's CV on screen without consent.

## 1. Prepare

1. Start the stack the usual way (`docker compose up`) with the real `.env` (never shown on screen).
2. Warm the replay cache so the live demo also works offline. In `.env` set:
   ```
   COACH_Demo__Replay=true
   COACH_Demo__Record=true
   ```
   Recordings go to `demo-recordings/llm-replay.json` (git-ignored, contains model output about the persona; do not commit or share it).
3. Walk through the whole demo once with the demo user so every LLM call is stored. Check the file grew.
   Analyse every demo posting once while route33 is up. In Development, a later re-analyse of a demo posting that
   fails or takes longer than 20 seconds (`COACH_Demo__TimeoutSeconds`) keeps that stored analysis and says so on screen.
4. Set `COACH_Demo__Record=false` and restart the api. Replay stays on: identical inputs are now answered from the file with no network.
5. Reset the demo user's data so the score starts low (about 62%) and the demo posting is unscanned.
6. Run `dotnet run --project backend/tests/InternshipCoach.Evaluation` once and check: quote-check failures 0.

## 2. Set up the screen

- Browser at 1920x1080, light mode, zoom 110%, bookmarks bar hidden, notifications off (Do Not Disturb).
- Side-by-side shot: tick "Compare with a generic answer" on the posting page (stored generic answer for demo postings). A second tab with plain ChatGPT answering "What do I need to get this internship?" is optional.
- macOS: Cmd+Shift+5, "Record Entire Screen" or selected window, microphone on if narrating, save to Desktop.

## 3. Take (target 90 seconds, rehearse once first)

1. 0:00 Sign in with GitHub. Sources fill the profile; point at one item with its source link.
2. 0:15 Postings list: a judge's company posting is there. Open it.
3. 0:25 Gap matrix with score (e.g. 62%). Show one evidence quote and one action naming a project.
4. 0:40 Tick "Compare with a generic answer": generic advice on the right, no evidence, no project names, no score.
5. 0:50 Back in the app: tick a gap, re-scan. Score rises (e.g. 62% to 81%).
6. 1:05 Documents: download the CV in Dutch.
7. 1:15 Interview prep: tailored questions from gaps and repos.
8. 1:25 End on the dashboard. Stop recording.

## 4. After

- Trim to 90 seconds, export as MP4 (H.264), keep a copy on a USB stick and in the team drive.
- Watch it once at full screen: no keys, emails or real CV text visible. Re-record if anything leaks.
- On demo day: play the video if the live run stalls for more than 10 seconds.
