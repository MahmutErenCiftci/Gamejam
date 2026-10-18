# Agent Playbook — Claude Code / Codex / Diğer AI'larla 4 Kişilik Ekip Çalışması

**Ana yasa:** `AGENTS.md` bu belgeden üstündür. Agent'lara ZIP'in içindeki bütün dosyaları tek seferde kopyalamak yerine repo içindeki `AGENTS.md` ve **kendi görev belgesini** okutun.

## Agent'a görev vermeden önce

1. Agent'ın **hangi ekip üyesi adına** çalıştığını belirtin: `owner=K1/K2/K3/K4`.
2. GitHub issue ID ve owner path'ini verin. `Biraz tüm oyunu toparla` gibi belirsiz komut **verilmez**.
3. `AGENTS.md`, `technical/INTERFACE_CONTRACTS.md` ve görevle ilgili bölüm dosyasını okutun.
4. Agent'ın değiştireceği dosyaları **önceden** söylemesini isteyin.
5. Çalışma sonunda gerçek test koşup koşmadığını raporlamasını isteyin.

## Tek görevlik standart prompt

```
You are an implementation agent on the 48-hour Unity 2D game jam project AFET UYANIŞI.
First read AGENTS.md, START_HERE.md, technical/INTERFACE_CONTRACTS.md, and the document relevant to your assigned task. Follow the rules exactly.

OWNER: K__
TASK ID: __
SCOPE: __
ALLOWED PATHS: __
DEADLINE: T+__
ACCEPTANCE CRITERIA: __

Before edits: list the files you will change, dependencies, and risks in 3 lines.
Implement a minimal, testable solution only in the allowed paths. Do not touch anyone else's .unity scene, ProjectSettings, .meta, packages, or unrelated files.
Afterwards report: changed files; what actually works; exact tests you ran (or UNTESTED); blockers; handoff to owner.
Do not claim successful build/test unless executed.
```

## Dört kişiye başlangıç promptları

**K1 / Menü & entegrasyon:**

```
OWNER K1 | TASK SETUP-01 + MENU-01
Read AGENTS.md, docs/GAME_DESIGN_DOCUMENT.md, technical/UNITY_PLAN.md.
Work ONLY in Assets/Scenes/Menu, Assets/Scripts/Core, Assets/Scripts/Menu, and owned ProjectSettings/Packages.
Implement MainMenu with working Flood/Earthquake scene selection; show Wildfire and Landslide as locked; add a 5-second skippable intro.
Publish the scene/router names as a contract for K2/K3 before changing shared interfaces.
Do not edit Flood/Earthquake scene files.
```

**K2 / Sel oynanışı:**

```
OWNER K2 | TASK FLOOD-01
Read AGENTS.md, story/FLOOD_CHAPTER.md, technical/INTERFACE_CONTRACTS.md.
Work ONLY in Assets/Scenes/Chapters/Flood and Assets/Scripts/Chapters/Flood.
Build a playable 7-state flood chapter with interaction, news, mother, rain, rumor, 2 decisions and educational result.
Use temporary sprite references if art not yet delivered. Do not edit MainMenu or Earthquake or shared/router files. Make a runnable scene and report test.
```

**K3 / Görseller + Deprem:**

```
OWNER K3 | TASK ART-01 + QUAKE-01
Read AGENTS.md, docs/ART_BIBLE.md, story/EARTHQUAKE_CHAPTER.md.
Allowed paths: Assets/Art, Assets/Animations, Assets/Prefabs/Visual, Assets/Scenes/Chapters/Earthquake, Assets/Scripts/Chapters/Earthquake.
Import and prepare owned PNGs; build EarthquakeChapter with a 5-second countdown, five choices and correct feedback, a minimal shake and a result screen.
Deliver art to K1/K2 by referencing files and import settings, not editing their scenes.
```

**K4 / Hikâye, QA, ses:**

```
OWNER K4 | TASK STORY-01 + QA-01
Read AGENTS.md, story/FLOOD_CHAPTER.md, story/EARTHQUAKE_CHAPTER.md, qa/TEST_PLAN.md.
Only change Assets/Data, Assets/Audio, story, qa and your own planning documents.
Write clear Turkish narrative/dialogue data with correct disaster-safety feedback. Test both chapters independently, log steps and result, prepare 2–3 minute jury demonstration.
Do not change C# scripts or other owners' .unity scenes.
```

## Agent raporu örneği

```
TASK: QUAKE-02  OWNER: K3
FILES: Assets/Scenes/Chapters/Earthquake/EarthquakeChapter.unity; Assets/Scripts/Chapters/Earthquake/EarthquakeChoiceController.cs
IMPLEMENTED: 5 choices, 5-second timer, feedback / reset
TEST: Unity Play Mode tested buttons 1-5 and timeout — PASS (Unity version/commit: ...)
BLOCKER: Waiting for K4 content JSON (temporary placeholder)
HANDOFF: K1 needs to add scene to build list; no K1-owned file edited
```

## Multi-agent çakışmayı önleme

- Bir agent = bir owner branch + sınırlı path.
- Aynı owner branch'inde bile iki agent **aynı dosyayı** paralel düzenlemez.
- Önce kabul edilmiş interface, sonra paralel geliştirme.
- Diğer agent çıktılarını doğrudan `main`'e taşımayın; K1 entegre etsin.
- GitHub PR/CI otomasyonu varsa faydalıdır; yoksa K1 manuel smoke test yapar.

**Agent kullanım hedefi:** daha fazla kod üretmek değil, sahneyi **çalışır ve entegre** teslim etmek.
