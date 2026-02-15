# Prompt for your coding agent (Pico 4 Ultra integration fix)

Use this exact prompt with your coding agent:

---

You are a senior XR/Unity engineer. Fix my Pico image viewer app so it works properly on **Pico 4 Ultra** with controllers and has a clean first-run UX.

## Context / current problems
- On first launch, Android/Pico permission popup appears (All files access), and overall flow feels broken.
- After closing permission popup, the image list panel appears at top-right, off-center, and looks broken.
- UI is not responsive to Pico controllers (cannot point/select/move panel).
- First scene is visually unfriendly (mostly white/blue, no intentional environment/UI layout).
- App cannot be functionally tested because controller interaction seems non-functional.

## Goals
1. Reliable Pico 4 Ultra controller interaction (ray + click/select + scroll/drag where needed).
2. Correct and comfortable UI placement in front of the user, not top-right.
3. Robust first-run permission flow that does not leave app in unusable state.
4. Better initial scene composition and visual polish for VR comfort/readability.
5. Produce reproducible test steps and a short troubleshooting section.

## Hard requirements
- Keep compatibility with current Pico SDK + Unity XR stack used in the repo.
- Do not "mock" fixes; implement real interaction wiring.
- Avoid breaking existing image loading logic unless necessary.
- If architecture changes are required, explain why briefly in PR notes.

## Implementation checklist

### A) XR input and controller interaction
- Audit XR plugins/packages and ensure a single consistent input path (OpenXR + Pico provider or project’s intended stack).
- Ensure EventSystem exists and is configured for XR UI interaction (e.g., XR UI Input Module / Input System UI module as appropriate).
- Add/verify XR Ray Interactors for both controllers and valid interaction layers.
- Ensure controller select/activate actions are mapped and enabled in the active Input Action Asset.
- Make world-space canvases interactable by XR rays:
  - GraphicRaycaster present
  - TrackedDeviceGraphicRaycaster (if required by stack)
  - Correct sorting/layer masks
- Confirm hover, click, and scroll behavior using controller input.

### B) UI panel positioning and usability
- On app start, place the main panel in front of HMD at comfortable distance (roughly 1.2–2.0m) and eye-level offset.
- Prevent accidental spawn at world origin/top-right due to stale transforms.
- If panel is movable, implement explicit grab/drag behavior with bounds and reset option.
- Add "Recenter UI" action/button that repositions panel in front of user at runtime.

### C) Permission flow (Android/Pico)
- Review Android manifest + runtime permission requests for media/files.
- For Android 13+, prefer granular media permissions when possible (READ_MEDIA_IMAGES) instead of broad all-files access unless absolutely necessary.
- If MANAGE_EXTERNAL_STORAGE is required, gate it cleanly:
  - Show in-app pre-permission explanation panel first.
  - Trigger settings/permission flow once.
  - Detect result on return and refresh UI state.
- Ensure app remains interactive while/after permission flow and never strands UI off-screen.
- Add clear state messaging: "permission needed", "granted", "denied with retry".

### D) First scene UX polish
- Improve default environment:
  - Neutral gradient/skybox and comfortable floor reference.
  - Better panel styling, spacing, readable text size/contrast.
  - Optional subtle header with app title + status.
- Ensure panel scale is VR-appropriate and readable without strain.

### E) Debugging and reliability
- Add lightweight runtime logs for:
  - Active XR input modules
  - Permission state transitions
  - Panel spawn/recenter coordinates
  - Controller interaction events (hover/select)
- Include a small in-app debug overlay (toggleable) for build verification on headset.

## Deliverables
1. Code changes across scene/prefab/scripts/input config/manifest as needed.
2. A concise CHANGELOG of what was fixed.
3. "How to test on Pico 4 Ultra" checklist:
   - Fresh install first-run
   - Permission grant path
   - Permission deny/retry path
   - Controller hover/select on list items
   - Recenter UI works
4. Known limitations (if any) and next-step recommendations.

## Acceptance criteria
- On clean install, user can complete permission flow and return to fully interactive app.
- Main image list panel appears centered in front of user and is reachable with controllers.
- Controller ray can hover + select UI reliably.
- User can recenter UI anytime.
- First scene looks intentionally designed (not raw default).

Now implement the fix, then provide:
- Summary of root causes found
- File-by-file changes
- Test evidence and exact verification steps
- Any follow-up tasks

---

Tip: Ask the agent to include short video or screenshots from headset/emulator after fixes.
